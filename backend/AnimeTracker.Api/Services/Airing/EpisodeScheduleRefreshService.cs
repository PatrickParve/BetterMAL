using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing.AniList;
using AnimeTracker.Api.Services.Relations;
using AnimeTracker.Api.Services.Scheduling;
using AnimeTracker.Api.Services.Updates;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Airing;

/// <summary>The airing refresh. A single anime is fetched with single requests
/// (<see cref="RefreshOneAsync"/>); a pass over many is batched
/// (<see cref="RefreshBatchAsync"/>, add-first-run-setup design D10). Both write
/// through <see cref="SaveFetchedAsync"/>, so a batched pass stores exactly what
/// fetching each anime on its own would.</summary>
public class EpisodeScheduleRefreshService(
    AnimeTrackerDbContext db,
    IUserAnimeEntryRepository entryRepository,
    IEpisodeAiringRepository episodeAiringRepository,
    IAniListClient aniList,
    AniListRelationStore relationStore,
    IBroadcastLocalTimeConverter localTimeConverter,
    IAnimeUpdateRecorder updateRecorder,
    ILogger<EpisodeScheduleRefreshService> logger) : IEpisodeScheduleRefreshService
{
    /// <summary>How old the record of an anime AniList doesn't know must be before an
    /// automatic pass looks it up again. A refresh the user starts by hand ignores
    /// the age.</summary>
    public static readonly TimeSpan AbsentRecheckAfter = TimeSpan.FromDays(90);

    /// <summary>What AniList reported about one anime, before it is saved.
    /// <paramref name="Relations"/> is non-null exactly when a lookup was made for
    /// it (a lookup carries relations; a read by stored id doesn't).</summary>
    private sealed record AniListFacts(
        int? AniListId, string? Status, DateTimeOffset? NextAiringAtUtc, int? Episodes,
        IReadOnlyList<AniListRelationEdge>? Relations);

    private enum LookupPlan { Lookup, UseStoredId, SkipKnownAbsent }

    public async Task<List<int>> GetTrackedAnimeIdsAsync(CancellationToken ct = default)
    {
        var entries = await entryRepository.GetAllAsync(ct);
        return entries
            .Select(e => e.Anime)
            .Where(anime => anime.AiringStatus is "currently_airing" or "not_yet_aired")
            .Select(anime => anime.Id)
            .Distinct()
            .ToList();
    }

    public Task RefreshOneAsync(int animeId, bool relookupAbsent = false, CancellationToken ct = default) =>
        RefreshOneCoreAsync(animeId, relookupAbsent, ct);

    /// <summary>Whether an anime needs a lookup by MAL id, can read its stored AniList
    /// id, or is recorded as unknown to AniList and not due another look. An anime
    /// whose <c>LastFetchedAt</c> is set has been looked up, so an unknown one
    /// (no AniList id) is told apart from a never-looked-up one by that mark alone.</summary>
    private static LookupPlan PlanFor(AnimeAiringSync? sync, bool relookupAbsent, DateTimeOffset now)
    {
        if (sync is null || sync.LastFetchedAt is null)
            return LookupPlan.Lookup;

        if (sync.AniListId is not null)
            return LookupPlan.UseStoredId;

        return relookupAbsent || now - sync.LastFetchedAt.Value >= AbsentRecheckAfter
            ? LookupPlan.Lookup
            : LookupPlan.SkipKnownAbsent;
    }

    public async Task<RefreshBatchResult> RefreshBatchAsync(
        IReadOnlyList<int> animeIds, RefreshBatchOptions? options = null, CancellationToken ct = default)
    {
        options ??= new RefreshBatchOptions();
        var ids = animeIds.Distinct().ToList();
        var counts = new Dictionary<AiringRefreshOutcome, int>();
        var reported = new HashSet<int>();

        // Once per anime, whichever path reaches it first: a request that fails
        // after some of its anime were already saved reports the rest, and must
        // not report those twice.
        void Report(int animeId, AiringRefreshOutcome outcome)
        {
            if (!reported.Add(animeId))
                return;

            counts[outcome] = counts.GetValueOrDefault(outcome) + 1;
            options.OnAnimeDone?.Invoke(animeId, outcome);
        }

        RefreshBatchResult Result() => new(
            counts.GetValueOrDefault(AiringRefreshOutcome.Fetched),
            counts.GetValueOrDefault(AiringRefreshOutcome.NoData),
            counts.GetValueOrDefault(AiringRefreshOutcome.Failed));

        if (ids.Count == 0)
            return Result();

        var titles = await db.AnimeMetadata.AsNoTracking()
            .Where(a => ids.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.Title, ct);
        var syncs = await db.AnimeAiringSyncs.AsNoTracking()
            .Where(s => ids.Contains(s.AnimeId))
            .ToDictionaryAsync(s => s.AnimeId, ct);
        var now = DateTimeOffset.UtcNow;

        var toLookUp = new List<int>();
        var withStoredId = new List<int>();
        foreach (var id in ids)
        {
            if (!titles.ContainsKey(id))
            {
                Report(id, AiringRefreshOutcome.NoData); // the anime itself is gone — nothing to refresh
                continue;
            }

            switch (PlanFor(syncs.GetValueOrDefault(id), options.RelookupAbsent, now))
            {
                case LookupPlan.Lookup: toLookUp.Add(id); break;
                case LookupPlan.UseStoredId: withStoredId.Add(id); break;
                default: Report(id, AiringRefreshOutcome.NoData); break; // unknown to AniList, and not due another look
            }
        }

        // What AniList reported for each anime that still needs its schedule, in
        // the order the anime were asked for.
        var facts = new Dictionary<int, AniListFacts>();
        var resolved = new List<int>();

        foreach (var chunk in toLookUp.Chunk(IAniListClient.MaxLookupBatch))
        {
            ct.ThrowIfCancellationRequested();

            IReadOnlyDictionary<int, AniListMediaLookup> found;
            try
            {
                found = await aniList.LookupBatchByMalIdsAsync(chunk, ct);
            }
            // Filtered on the token, not the exception type: an HttpClient
            // timeout is a TaskCanceledException with ct still live, and fails the
            // anime of this request alone rather than ending the whole pass.
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.LogWarning(ex, "AniList lookup failed for {Count} anime.", chunk.Length);
                foreach (var id in chunk)
                    Report(id, AiringRefreshOutcome.Failed);
                continue;
            }

            foreach (var id in chunk)
            {
                if (found.TryGetValue(id, out var lookup))
                {
                    facts[id] = FactsFrom(lookup);
                    resolved.Add(id);
                    continue;
                }

                // A MAL id missing from the answer is what a single lookup's
                // "not found" is: AniList has no entry for it.
                var outcome = await GuardedAsync(id, async () =>
                {
                    await RecordUnknownAsync(id, ct);
                    logger.LogInformation("AniList has no entry for anime {AnimeId} ({Title}).", id, titles[id]);
                    return AiringRefreshOutcome.NoData;
                }, ct);
                Report(id, outcome);
            }
        }

        foreach (var chunk in withStoredId.Chunk(IAniListClient.MaxMediaBatch))
        {
            ct.ThrowIfCancellationRequested();

            IReadOnlyDictionary<int, AniListMediaState> found;
            try
            {
                found = await aniList.GetMediaBatchAsync(chunk.Select(id => syncs[id].AniListId!.Value).Distinct().ToList(), ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.LogWarning(ex, "AniList media read failed for {Count} anime.", chunk.Length);
                foreach (var id in chunk)
                    Report(id, AiringRefreshOutcome.Failed);
                continue;
            }

            foreach (var id in chunk)
            {
                var aniListId = syncs[id].AniListId!.Value;
                // An id AniList no longer has reads as the single schedule query's
                // 404 does: no status, no next episode, no total.
                var state = found.GetValueOrDefault(aniListId);
                facts[id] = new AniListFacts(aniListId, state?.Status, state?.NextAiringEpisodeAtUtc, state?.Episodes, Relations: null);
                resolved.Add(id);
            }
        }

        foreach (var chunk in resolved.Chunk(IAniListClient.MaxMediaBatch))
        {
            ct.ThrowIfCancellationRequested();
            await FetchSchedulesAsync(chunk, facts, titles, Report, ct);
        }

        return Result();
    }

    /// <summary>Reads the schedules of <paramref name="animeIds"/> and saves each anime
    /// as its rows complete. Anime the read couldn't finish (AniList allows 100
    /// pages a read) are read again in a smaller batch, down to one at a time.</summary>
    private async Task FetchSchedulesAsync(
        IReadOnlyList<int> animeIds,
        IReadOnlyDictionary<int, AniListFacts> facts,
        IReadOnlyDictionary<int, string> titles,
        Action<int, AiringRefreshOutcome> report,
        CancellationToken ct)
    {
        var animeByAniListId = animeIds.ToLookup(id => facts[id].AniListId!.Value);

        IReadOnlyList<int> incomplete;
        try
        {
            incomplete = await aniList.GetAiringSchedulesAsync(
                animeByAniListId.Select(g => g.Key).ToList(),
                async (aniListId, episodes) =>
                {
                    foreach (var animeId in animeByAniListId[aniListId])
                    {
                        var outcome = await GuardedAsync(animeId, async () =>
                        {
                            await SaveFetchedAsync(animeId, facts[animeId], episodes, ct);
                            return episodes.Count > 0 ? AiringRefreshOutcome.Fetched : AiringRefreshOutcome.NoData;
                        }, ct);
                        report(animeId, outcome);
                    }
                },
                ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // What was called back before the request failed is saved, and
            // reported already; report only ignores it a second time.
            logger.LogWarning(ex, "AniList schedule read failed for {Count} anime.", animeIds.Count);
            foreach (var animeId in animeIds)
                report(animeId, AiringRefreshOutcome.Failed);
            return;
        }

        if (incomplete.Count == 0)
            return;

        var open = animeIds.Where(id => incomplete.Contains(facts[id].AniListId!.Value)).ToList();
        if (open.Count == 0)
            return;

        if (animeIds.Count == 1)
        {
            // Alone and still more rows than AniList will page through: reading
            // it again would fail the same way.
            logger.LogError(
                "AniList's schedule for anime {AnimeId} ({Title}) is longer than a read can page through; it stays without airing data.",
                open[0], titles.GetValueOrDefault(open[0], "unknown"));
            report(open[0], AiringRefreshOutcome.Failed);
            return;
        }

        // Some of the group were saved, so what is left is a smaller batch already.
        // When nothing was, the group is cut in halves, down to one at a time.
        var batches = open.Count < animeIds.Count ? [open] : open.Chunk((open.Count + 1) / 2).Select(c => c.ToList()).ToList();
        foreach (var batch in batches)
            await FetchSchedulesAsync(batch, facts, titles, report, ct);
    }

    /// <summary>Runs one anime's save. A failure that isn't the pass being cancelled
    /// fails that anime alone; the tracker is cleared so its half-applied changes
    /// can't ride along in the next anime's save.</summary>
    private async Task<AiringRefreshOutcome> GuardedAsync(int animeId, Func<Task<AiringRefreshOutcome>> save, CancellationToken ct)
    {
        try
        {
            return await save();
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "AniList airing refresh failed for anime {AnimeId}.", animeId);
            db.ChangeTracker.Clear();
            return AiringRefreshOutcome.Failed;
        }
    }

    public async Task CatchUpAsync(CancellationToken ct = default)
    {
        var listAnimeIds = await db.UserAnimeEntries.AsNoTracking().Select(e => e.AnimeId).Distinct().ToListAsync(ct);
        if (listAnimeIds.Count == 0)
            return;

        var syncs = await db.AnimeAiringSyncs.AsNoTracking()
            .Where(s => listAnimeIds.Contains(s.AnimeId))
            .ToDictionaryAsync(s => s.AnimeId, ct);
        var now = DateTimeOffset.UtcNow;

        // No mark, or recorded as unknown to AniList 90 days ago: exactly the
        // anime an automatic pass would look up.
        var targets = listAnimeIds
            .Where(id => PlanFor(syncs.GetValueOrDefault(id), relookupAbsent: false, now) == LookupPlan.Lookup)
            .ToList();
        if (targets.Count == 0)
            return;

        var titles = await db.AnimeMetadata.AsNoTracking()
            .Where(a => targets.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.Title, ct);

        await RefreshBatchAsync(
            targets,
            new RefreshBatchOptions(OnAnimeDone: (animeId, outcome) =>
            {
                if (outcome == AiringRefreshOutcome.NoData)
                    logger.LogInformation(
                        "Catch-up: AniList returned no airing data for anime {AnimeId} ({Title}).",
                        animeId, titles.GetValueOrDefault(animeId, "unknown"));
            }),
            ct);
    }

    public async Task<List<int>> GetFullRefreshTargetsAsync(bool force, CancellationToken ct = default)
    {
        var entries = await entryRepository.GetAllAsync(ct);
        var distinctAnime = entries.Select(e => e.Anime).DistinctBy(a => a.Id).ToList();
        if (force)
            return distinctAnime.Select(a => a.Id).ToList();

        var animeIds = distinctAnime.Select(a => a.Id).ToList();
        var syncs = await db.AnimeAiringSyncs.AsNoTracking()
            .Where(s => animeIds.Contains(s.AnimeId))
            .ToDictionaryAsync(s => s.AnimeId, ct);

        // One grouped read for the highest stored episode of every anime, never a
        // read of each anime's rows.
        var highestEpisode = await db.EpisodeAirings.AsNoTracking()
            .Where(e => animeIds.Contains(e.AnimeId))
            .GroupBy(e => e.AnimeId)
            .Select(g => new { AnimeId = g.Key, Highest = g.Max(e => e.Episode) })
            .ToDictionaryAsync(x => x.AnimeId, x => x.Highest, ct);

        return distinctAnime
            .Where(a => !IsHistoryComplete(a, syncs.GetValueOrDefault(a.Id), highestEpisode.GetValueOrDefault(a.Id)))
            .Select(a => a.Id)
            .ToList();
    }

    /// <summary>The highest stored episode, not the row count, against the known total:
    /// an anime whose first episodes premiered together has fewer rows than episodes.
    /// No stored row reads as 0 here, which is below any known total.</summary>
    private static bool IsHistoryComplete(AnimeMetadata anime, AnimeAiringSync? sync, int highestStoredEpisode) =>
        anime.AiringStatus == "finished_airing"
        && sync is { AniListId: not null, LastFetchedAt: not null }
        && anime.TotalEpisodes is { } total
        && highestStoredEpisode >= total;

    private async Task<int> RefreshOneCoreAsync(int animeId, bool relookupAbsent, CancellationToken ct)
    {
        var title = await db.AnimeMetadata.AsNoTracking()
            .Where(a => a.Id == animeId)
            .Select(a => a.Title)
            .FirstOrDefaultAsync(ct);
        if (title is null)
            return 0; // the anime itself is gone — nothing to refresh

        var sync = await db.AnimeAiringSyncs.AsNoTracking().FirstOrDefaultAsync(s => s.AnimeId == animeId, ct);
        var plan = PlanFor(sync, relookupAbsent, DateTimeOffset.UtcNow);
        if (plan == LookupPlan.SkipKnownAbsent)
            return 0; // recorded as unknown to AniList, and not due another look

        // Resolve the AniList id once and cache it. A prior lookup that came
        // back "AniList doesn't know this anime" (AniListId null, LastFetchedAt
        // set) is not retried until it is 90 days old or a refresh started by hand
        // asks (PlanFor) — sync.LastFetchedAt already being set is what
        // distinguishes that from "never looked up".
        AniListFacts facts;
        if (plan == LookupPlan.Lookup)
        {
            var lookup = await aniList.LookupByMalIdAsync(animeId, ct);
            if (lookup is null)
            {
                await RecordUnknownAsync(animeId, ct);
                logger.LogInformation("AniList has no entry for anime {AnimeId} ({Title}).", animeId, title);
                return 0;
            }

            facts = FactsFrom(lookup);
        }
        else
        {
            facts = new AniListFacts(sync!.AniListId, Status: null, NextAiringAtUtc: null, Episodes: null, Relations: null);
        }

        var schedule = await aniList.GetAiringScheduleAsync(facts.AniListId!.Value, ct);

        // Schedule first, matching how status/nextAiringAt are merged: the schedule
        // fetch's own Media(id:) selection is the fresher of the two when both ran
        // this call.
        facts = facts with
        {
            Status = schedule.Status ?? facts.Status,
            NextAiringAtUtc = schedule.NextAiringEpisodeAtUtc ?? facts.NextAiringAtUtc,
            Episodes = schedule.TotalEpisodes ?? facts.Episodes,
        };

        await SaveFetchedAsync(animeId, facts, schedule.Episodes, ct);
        return schedule.Episodes.Count;
    }

    private static AniListFacts FactsFrom(AniListMediaLookup lookup) =>
        new(lookup.AniListId, lookup.Status, lookup.NextAiringEpisodeAtUtc, lookup.Episodes, lookup.Relations);

    /// <summary>Records that AniList has no entry for an anime, with the time of the
    /// look: the airing-fetched mark that keeps automatic passes from asking again
    /// for 90 days.</summary>
    private async Task RecordUnknownAsync(int animeId, CancellationToken ct)
    {
        var sync = await db.AnimeAiringSyncs.FirstOrDefaultAsync(s => s.AnimeId == animeId, ct);
        var now = DateTimeOffset.UtcNow;
        await UpsertSyncAsync(sync, animeId, aniListId: null, now, nextAiringEpisodeAtUtc: null, hasCompleteData: false, nextRecheckAtUtc: null, relationsFetchedAt: now, ct);
    }

    /// <summary>Saves what AniList reported for one anime, in one transaction: its
    /// rows, its bookkeeping, its AniList total and, where a lookup was made, its
    /// relations. A restart leaves it saved in full or not at all. Both the single
    /// and the batched path end here, which is what keeps them equivalent.</summary>
    private async Task SaveFetchedAsync(int animeId, AniListFacts facts, IReadOnlyList<AniListEpisode> episodes, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var anime = await db.AnimeMetadata.FirstOrDefaultAsync(a => a.Id == animeId, ct);
        if (anime is null)
            return; // deleted since it was read — nothing left to save

        var sync = await db.AnimeAiringSyncs.FirstOrDefaultAsync(s => s.AnimeId == animeId, ct);
        var now = DateTimeOffset.UtcNow;

        // Whether this is the anime's first airing fetch, read before anything
        // below stamps the mark. A first observation is not a value becoming known
        // or moving (anime-updates spec), so it records neither. An anime AniList
        // was recorded as not knowing has the mark too, so a later lookup that
        // finds it is not a first fetch.
        var hadAiringMark = sync?.LastFetchedAt is not null;

        // Preserved unless this call made a fresh lookup — a refresh that skips
        // straight to the schedule fetch (aniListId already cached) didn't just
        // re-ask about relations, so it shouldn't bump this timestamp forward.
        var relationsFetchedAt = sync?.RelationsFetchedAt;
        if (facts.Relations is not null)
        {
            relationsFetchedAt = now;
            // Relations ride along on the lookup (relation-confidence spec) — free,
            // no extra AniList request.
            await relationStore.ReplaceAsync(animeId, facts.Relations, ct);
        }

        var previousTotalEpisodes = anime.TotalEpisodes;
        anime.AniListTotalEpisodes = facts.Episodes;
        anime.ResolveTotalEpisodes();
        if (hadAiringMark && previousTotalEpisodes is null && anime.TotalEpisodes is not null)
            await updateRecorder.RecordAsync(anime, AnimeUpdateKinds.EpisodeCountReleased, default, now, ct);

        var rows = episodes
            .Select(e => new EpisodeAiring { AnimeId = animeId, Episode = e.Episode, AirsAtUtc = e.AirsAtUtc, FetchedAt = now })
            .ToList();

        // Read before replacing — ReplaceForAnimeAsync's delete+insert leaves
        // nothing to diff against afterwards. A no-op replace (rows empty)
        // leaves the stored set untouched, so there is nothing to compare either.
        if (rows.Count > 0)
        {
            var existingRows = await db.EpisodeAirings.AsNoTracking()
                .Where(e => e.AnimeId == animeId).ToListAsync(ct);
            await episodeAiringRepository.ReplaceForAnimeAsync(animeId, rows, ct);
            if (hadAiringMark)
                await RecordEpisodeMoves(anime, existingRows, rows, now, ct);
        }
        else
        {
            await episodeAiringRepository.ReplaceForAnimeAsync(animeId, rows, ct);
        }

        var (hasCompleteData, nextRecheckAtUtc) = ComputeRecheckState(anime.AiringStatus, facts.NextAiringAtUtc, now);
        if (!hasCompleteData)
            logger.LogInformation(
                "Anime {AnimeId} ({Title}) has incomplete AniList airing data (AniList status: {Status}); next recheck at {NextRecheckAtUtc}.",
                animeId, anime.Title, facts.Status ?? "unknown", nextRecheckAtUtc);

        await UpsertSyncAsync(sync, animeId, facts.AniListId, now, facts.NextAiringAtUtc, hasCompleteData, nextRecheckAtUtc, relationsFetchedAt, ct);
        await transaction.CommitAsync(ct);
    }

    /// <summary>Diffs <paramref name="before"/> (the anime's stored rows just
    /// before <c>ReplaceForAnimeAsync</c> replaced them) against
    /// <paramref name="after"/> (the incoming set) and records one
    /// EpisodesMoved update naming the earliest episode whose local calendar
    /// air date moved (design.md D16). Skipped entirely when there were no
    /// prior rows at all — a first fetch is not a move — or when the anime has
    /// finished airing (spec "Schedule changes are recorded only while an
    /// anime has not finished airing"). Only episodes that had not yet aired,
    /// by the stored (pre-refresh) date, are considered: a past episode's date
    /// changing is AniList correcting history, not a schedule moving.</summary>
    private async Task RecordEpisodeMoves(
        AnimeMetadata anime, List<EpisodeAiring> before, List<EpisodeAiring> after, DateTimeOffset now, CancellationToken ct)
    {
        if (before.Count == 0)
            return;
        if (anime.AiringStatus == "finished_airing")
            return;

        var previousAirsAtByEpisode = before.ToDictionary(e => e.Episode, e => e.AirsAtUtc);

        int? earliestEpisode = null;
        DateOnly earliestFrom = default, earliestTo = default;

        foreach (var episode in after)
        {
            if (!previousAirsAtByEpisode.TryGetValue(episode.Episode, out var previousAirsAtUtc))
                continue; // no prior row for this episode — nothing it moved from

            if (previousAirsAtUtc <= now)
                continue; // already aired as of the stored date — a history correction, not a move

            var previousLocalDate = localTimeConverter.GetLocalDate(previousAirsAtUtc);
            var newLocalDate = localTimeConverter.GetLocalDate(episode.AirsAtUtc);
            if (previousLocalDate == newLocalDate)
                continue; // same local calendar day — not a move (design.md D16)

            if (earliestEpisode is null || episode.Episode < earliestEpisode)
            {
                earliestEpisode = episode.Episode;
                earliestFrom = previousLocalDate;
                earliestTo = newLocalDate;
            }
        }

        if (earliestEpisode is not { } movedEpisode)
            return;

        var moves = new ScheduleMoveDetails(
            MovedEpisode: movedEpisode, PreviousEpisodeDate: earliestFrom, NewEpisodeDate: earliestTo);
        await updateRecorder.RecordAsync(anime, AnimeUpdateKinds.EpisodesMoved, moves, now, ct);
    }

    // Checkpoints at T-30d/T-7d/T for a known next episode; every 3 days once
    // that instant has passed with no newer data, or when the show is still
    // airing with no next episode reported at all; never for a finished show
    // (design decision 8).
    private static (bool HasCompleteData, DateTimeOffset? NextRecheckAtUtc) ComputeRecheckState(
        string? malAiringStatus, DateTimeOffset? nextAiringAtUtc, DateTimeOffset now)
    {
        if (malAiringStatus == "finished_airing")
            return (true, null);

        if (nextAiringAtUtc is not { } next || next <= now)
            return (false, now.AddDays(3));

        DateTimeOffset[] checkpoints = [next.AddDays(-30), next.AddDays(-7), next];
        return (true, checkpoints.First(c => c > now));
    }

    private async Task UpsertSyncAsync(
        AnimeAiringSync? sync, int animeId, int? aniListId, DateTimeOffset fetchedAt,
        DateTimeOffset? nextAiringEpisodeAtUtc, bool hasCompleteData, DateTimeOffset? nextRecheckAtUtc,
        DateTimeOffset? relationsFetchedAt, CancellationToken ct)
    {
        if (sync is null)
        {
            sync = new AnimeAiringSync { AnimeId = animeId };
            db.AnimeAiringSyncs.Add(sync);
        }

        sync.AniListId = aniListId;
        sync.LastFetchedAt = fetchedAt;
        sync.NextAiringEpisodeAtUtc = nextAiringEpisodeAtUtc;
        sync.HasCompleteData = hasCompleteData;
        sync.NextRecheckAtUtc = nextRecheckAtUtc;
        sync.RelationsFetchedAt = relationsFetchedAt;

        await db.SaveChangesAsync(ct);
    }
}
