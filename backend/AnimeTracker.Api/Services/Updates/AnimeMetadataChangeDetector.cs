using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Series;

namespace AnimeTracker.Api.Services.Updates;

/// <summary>The moment-in-time snapshot every writer of <see cref="AnimeMetadata"/>
/// must take before calling <c>ApplyTo</c>, so <see cref="IAnimeMetadataChangeDetector"/>
/// has something to diff the post-<c>ApplyTo</c> state against. Relations and
/// the update-relevant fields only — everything else (score, rank, synopsis,
/// ...) is metadata drift the anime-updates feed deliberately ignores.
///
/// <para><paramref name="Relations"/> being <c>null</c> means <em>relations were
/// not observed by this write</em> — the lean-listing case, see
/// <see cref="IAnimeMetadataChangeDetector.SnapshotListing"/> — and never that
/// the anime has no relations, which an empty set says. A null set diffs to
/// nothing at all rather than to "every edge is new".</para>
///
/// <para><paramref name="HadFullDetail"/> is whether the anime had <em>ever</em>
/// been fully fetched before this write. It rides the snapshot for the same
/// reason the rest of it does: <c>ApplyTo</c> stamps <c>LastSyncedAt</c>, so it
/// is unreadable by the time the diff runs.</para></summary>
public readonly record struct AnimeMetadataSnapshot(
    HashSet<(int RelatedAnimeId, string RelationType)>? Relations,
    int? TotalEpisodes,
    DateOnly? AiredFrom,
    string? BroadcastDayOfWeek,
    TimeOnly? BroadcastTime,
    bool HadFullDetail);

/// <summary>The one place that turns a before/after <see cref="AnimeMetadata"/>
/// pair into <see cref="RelationDiscovery"/> and <see cref="AnimeUpdate"/> rows
/// (spec "Every path that writes anime data detects the updates it can").
/// Every writer of cached anime metadata snapshots via <see cref="Snapshot"/>
/// before calling <c>ApplyTo</c>, then hands both to <see cref="RecordAsync"/>
/// after, so no future write path can refresh metadata without also feeding
/// the updates feed — originally <c>MetadataRefreshService</c>'s own private
/// methods, extracted here once <c>ResyncService</c> needed the same
/// detection on its own full-detail fetch.</summary>
public interface IAnimeMetadataChangeDetector
{
    /// <summary>Captures <paramref name="anime"/>'s update-relevant state.
    /// <paramref name="anime"/>'s <c>RelatedAnime</c> must already be loaded —
    /// callers already need that tracked collection for <c>ApplyTo</c> itself
    /// to diff correctly.</summary>
    AnimeMetadataSnapshot Snapshot(AnimeMetadata anime);

    /// <summary>The <c>ApplyLeanTo</c> counterpart of <see cref="Snapshot"/>:
    /// same field capture, but <c>Relations</c> left null. A lean listing write
    /// neither touches relations nor <c>Include</c>s them, so diffing them
    /// would read an unloaded collection as an empty set and manufacture
    /// removals (and, on the other side, re-discover every edge). Callers still
    /// hand the result to <see cref="RecordAsync"/>; only the relation half of
    /// the diff falls away.</summary>
    AnimeMetadataSnapshot SnapshotListing(AnimeMetadata anime);

    /// <summary>Diffs <paramref name="before"/> against <paramref name="anime"/>'s
    /// state after <c>ApplyTo</c> replaced it, and queues whichever
    /// <see cref="RelationDiscovery"/>/<see cref="AnimeUpdate"/> rows the diff
    /// produced. Adds to the tracked context only — the caller owns
    /// <c>SaveChangesAsync</c>. Only call this for an anime that already had a
    /// cached row: a first observation is not a reveal and not a move, so
    /// callers must skip this entirely on their insert branch.
    ///
    /// <para>A row cached only by a lean listing write is likewise not a prior
    /// observation of the detail-only fields — it exists, but nobody ever
    /// fetched its premiere date or broadcast slot. That case the detector now
    /// enforces itself from <c>HadFullDetail</c> rather than trusting the
    /// caller to spot it, because the caller cannot: it looks exactly like the
    /// update branch.</para></summary>
    Task RecordAsync(AnimeMetadata anime, AnimeMetadataSnapshot before, DateTimeOffset now, CancellationToken ct = default);
}

public class AnimeMetadataChangeDetector(
    AnimeTrackerDbContext db, IAnimeUpdateRecorder updateRecorder, ISeriesBuildTrigger seriesBuildTrigger)
    : IAnimeMetadataChangeDetector
{
    public AnimeMetadataSnapshot Snapshot(AnimeMetadata anime) => new(
        anime.RelatedAnime.Select(r => (r.RelatedAnimeId, r.RelationType)).ToHashSet(),
        anime.TotalEpisodes,
        anime.AiredFrom,
        anime.BroadcastDayOfWeek,
        anime.BroadcastTime,
        HadFullDetail(anime));

    public AnimeMetadataSnapshot SnapshotListing(AnimeMetadata anime) => new(
        null,
        anime.TotalEpisodes,
        anime.AiredFrom,
        anime.BroadcastDayOfWeek,
        anime.BroadcastTime,
        HadFullDetail(anime));

    /// <summary>Whether this anime had ever had a full-detail fetch before the
    /// write being detected. Must be read here, in the snapshot, and not in
    /// <see cref="RecordAsync"/>: <c>ApplyTo</c> sets <c>LastSyncedAt = now</c>,
    /// so by record time every row looks fully fetched.
    ///
    /// <para><c>LastSyncedAt == default</c> is the same "never fully fetched"
    /// test <c>RefreshTiers</c>, <c>AnimeDetailService</c> and
    /// <c>RelationResolver</c> already read this row-level field for. It is
    /// deliberately not a per-field check ("was <c>AiredFrom</c> ever set?"),
    /// which cannot tell an anime whose premiere date we never fetched from one
    /// MAL genuinely has no date for — the inference <c>metadata-refresh</c>
    /// forbids: "SHALL NOT infer detail-completeness from the presence of any
    /// particular field".</para></summary>
    private static bool HadFullDetail(AnimeMetadata anime) => anime.LastSyncedAt != default;

    public async Task RecordAsync(AnimeMetadata anime, AnimeMetadataSnapshot before, DateTimeOffset now, CancellationToken ct = default)
    {
        var discoveredRelation = RecordDiscoveries(anime, before.Relations, now);
        await RecordFieldUpdates(anime, before, now, ct);

        // split-series-by-version tasks 9.1/9.2: a newly discovered relation
        // enqueues this anime's series for a background rebuild, so a new
        // entry (or a newly discovered alternative_version that splits a
        // franchise) reaches the series page without waiting for a visit or
        // the 30-day staleness window. Enqueue is synchronous and
        // non-blocking, and ISeriesBuildTrigger already dedupes against an id
        // already queued, so one refresh pass discovering several edges on
        // the same anime still enqueues it once.
        if (discoveredRelation)
            seriesBuildTrigger.Enqueue(anime.Id);
    }

    /// <summary>Writes one <see cref="RelationDiscovery"/> per edge present in
    /// <paramref name="anime"/>'s relation set after <c>ApplyTo</c> that wasn't
    /// in <paramref name="before"/>. Removed and unchanged edges are
    /// deliberately not events (design.md decision 10). Returns whether any
    /// edge was newly discovered, so the caller knows whether to enqueue a
    /// series build.</summary>
    private bool RecordDiscoveries(
        AnimeMetadata anime, HashSet<(int RelatedAnimeId, string RelationType)>? before, DateTimeOffset now)
    {
        // A listing snapshot did not observe relations (and did not load
        // them), so there is nothing here to diff against: no discovery row,
        // and no series build enqueued off a write that never saw an edge.
        if (before is null)
            return false;

        var discoveredAny = false;

        foreach (var edge in anime.RelatedAnime)
        {
            if (before.Contains((edge.RelatedAnimeId, edge.RelationType)))
                continue;

            db.RelationDiscoveries.Add(new RelationDiscovery
            {
                AnimeId = anime.Id,
                RelatedAnimeId = edge.RelatedAnimeId,
                RelationType = edge.RelationType,
                DiscoveredAt = now,
            });
            discoveredAny = true;
        }

        return discoveredAny;
    }

    /// <summary>Records whichever of the becoming-known/schedule-change kinds
    /// the diff produced (spec "Every path that writes anime data detects the
    /// updates it can"). A total episode count moving between two known
    /// values is deliberately not a kind at all (design.md D15) — only its
    /// null-to-value reveal is.</summary>
    private async Task RecordFieldUpdates(AnimeMetadata anime, AnimeMetadataSnapshot before, DateTimeOffset now, CancellationToken ct)
    {
        // The first *full-detail* fetch is the first observation, whatever else
        // was already cached (spec "For anime metadata, 'the first time' SHALL
        // mean the anime's first full-detail fetch, not the first time a row
        // existed for it"). A row cached leanly by season browsing, Top-Anime
        // or reconciliation exists with its premiere date and broadcast slot
        // null — but that null was never an observation of anything, so
        // diffing against it reports the cache catching up as though MAL had
        // just revealed the date. Same silence the insert branch keeps.
        // Relation discovery above is deliberately outside this gate (design
        // D3): a lean row's first full fetch still discovers all its edges.
        if (!before.HadFullDetail)
            return;

        var kinds = (AnimeUpdateKinds)0;
        var moves = new ScheduleMoveDetails();

        if (before.TotalEpisodes is null && anime.TotalEpisodes is not null)
            kinds |= AnimeUpdateKinds.EpisodeCountReleased;

        if (before.AiredFrom is null && anime.AiredFrom is not null)
        {
            kinds |= AnimeUpdateKinds.StartDateReleased;
        }
        else if (before.AiredFrom is not null && anime.AiredFrom is not null && before.AiredFrom != anime.AiredFrom)
        {
            kinds |= AnimeUpdateKinds.StartDateChanged;
            moves = moves with { PreviousStartDate = before.AiredFrom };
        }

        var slotKnownBefore = before.BroadcastDayOfWeek is not null || before.BroadcastTime is not null;
        var slotKnownAfter = anime.BroadcastDayOfWeek is not null || anime.BroadcastTime is not null;
        if (slotKnownBefore && slotKnownAfter &&
            (before.BroadcastDayOfWeek != anime.BroadcastDayOfWeek || before.BroadcastTime != anime.BroadcastTime))
        {
            kinds |= AnimeUpdateKinds.BroadcastSlotChanged;
            moves = moves with
            {
                PreviousBroadcastDayOfWeek = before.BroadcastDayOfWeek,
                PreviousBroadcastTime = before.BroadcastTime,
            };
        }

        // Schedule changes are news only while the anime hasn't finished
        // airing (spec "Schedule changes are recorded only while an anime has
        // not finished airing") — MAL tidying a finished show's dates is a
        // correction, not news.
        //
        // StartDateReleased is gated the same way and for the same reason
        // (spec "A premiere date released is news only while an anime has not
        // finished airing"): a premiere date *arriving* for a show that
        // finished years ago is MAL's records reaching us, not a date anyone
        // is waiting for. Only a show still ahead of, or in the middle of, its
        // broadcast has a premiere anyone can act on.
        //
        // EpisodeCountReleased is deliberately NOT in the mask. A premiere
        // date for a finished show reports an event already in the past that
        // the anime's own record already displays; an episode count is a fact
        // about what there is to watch, and `anime-updates` specifies the
        // AniList-supplied total precisely for the finished anime MyAnimeList
        // publishes no count for.
        //
        // anime.AiringStatus is the status ApplyTo just wrote, so the gate is
        // evaluated once, here at write time, against the same fetch that
        // produced the kinds — the same treatment AnnouncementResolutionService
        // gives the announcement gate. It is never re-evaluated on read: a
        // reveal recorded while a show had not finished airing survives that
        // show finishing.
        const AnimeUpdateKinds airingGatedKinds =
            AnimeUpdateKinds.StartDateReleased | AnimeUpdateKinds.StartDateChanged | AnimeUpdateKinds.BroadcastSlotChanged;
        if ((kinds & airingGatedKinds) != 0 && anime.AiringStatus is not ("not_yet_aired" or "currently_airing"))
            kinds &= ~airingGatedKinds;

        if (kinds != 0)
            await updateRecorder.RecordAsync(anime, kinds, moves, now, ct);
    }
}
