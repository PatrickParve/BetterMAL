using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Series;

namespace AnimeTracker.Api.Services.Updates;

/// <summary>The moment-in-time snapshot every writer of <see cref="AnimeMetadata"/>
/// must take before calling <c>ApplyTo</c>, so <see cref="IAnimeMetadataChangeDetector"/>
/// has something to diff the post-<c>ApplyTo</c> state against. Relations and
/// the update-relevant fields only — everything else (score, rank, synopsis,
/// ...) is metadata drift the anime-updates feed deliberately ignores.</summary>
public readonly record struct AnimeMetadataSnapshot(
    HashSet<(int RelatedAnimeId, string RelationType)> Relations,
    int? TotalEpisodes,
    DateOnly? AiredFrom,
    string? BroadcastDayOfWeek,
    TimeOnly? BroadcastTime);

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

    /// <summary>Diffs <paramref name="before"/> against <paramref name="anime"/>'s
    /// state after <c>ApplyTo</c> replaced it, and queues whichever
    /// <see cref="RelationDiscovery"/>/<see cref="AnimeUpdate"/> rows the diff
    /// produced. Adds to the tracked context only — the caller owns
    /// <c>SaveChangesAsync</c>. Only call this for an anime that already had a
    /// cached row: a first observation is not a reveal and not a move, so
    /// callers must skip this entirely on their insert branch.</summary>
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
        anime.BroadcastTime);

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
        AnimeMetadata anime, HashSet<(int RelatedAnimeId, string RelationType)> before, DateTimeOffset now)
    {
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
        const AnimeUpdateKinds scheduleChangeKinds = AnimeUpdateKinds.StartDateChanged | AnimeUpdateKinds.BroadcastSlotChanged;
        if ((kinds & scheduleChangeKinds) != 0 && anime.AiringStatus is not ("not_yet_aired" or "currently_airing"))
            kinds &= ~scheduleChangeKinds;

        if (kinds != 0)
            await updateRecorder.RecordAsync(anime, kinds, moves, now, ct);
    }
}
