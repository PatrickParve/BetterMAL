using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing.AniList;
using AnimeTracker.Api.Services.Relations;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Airing;

public class EpisodeScheduleRefreshService(
    AnimeTrackerDbContext db,
    IUserAnimeEntryRepository entryRepository,
    IEpisodeAiringRepository episodeAiringRepository,
    IAniListClient aniList,
    AniListRelationStore relationStore,
    ILogger<EpisodeScheduleRefreshService> logger) : IEpisodeScheduleRefreshService
{
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

    // Every my-list anime, regardless of airing status — the backfill target
    // (episode-airing-data spec: "every anime in my list"), unlike
    // GetTrackedAnimeIdsAsync's currently-airing/not-yet-aired subset used by
    // the elapsed-time daily pass. A finished show still needs its full
    // history fetched once so its aired count isn't permanently "unknown".
    private async Task<List<int>> GetAllMyListAnimeIdsAsync(CancellationToken ct)
    {
        var entries = await entryRepository.GetAllAsync(ct);
        return entries.Select(e => e.AnimeId).Distinct().ToList();
    }

    public Task RefreshOneAsync(int animeId, CancellationToken ct = default) => RefreshOneCoreAsync(animeId, ct);

    public async Task<int> RefreshManyAsync(IReadOnlyList<int> animeIds, CancellationToken ct = default, Action<int>? onProgress = null)
    {
        var zeroRows = 0;
        var processed = 0;
        foreach (var animeId in animeIds)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                if (await RefreshOneCoreAsync(animeId, ct) == 0)
                    zeroRows++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "AniList airing refresh failed for anime {AnimeId}.", animeId);
                zeroRows++;
            }

            processed++;
            onProgress?.Invoke(processed);
        }

        return zeroRows;
    }

    public async Task<List<int>> GetFullRefreshTargetsAsync(CancellationToken ct = default)
    {
        var entries = await entryRepository.GetAllAsync(ct);
        var distinctAnime = entries.Select(e => e.Anime).DistinctBy(a => a.Id).ToList();
        var animeIds = distinctAnime.Select(a => a.Id).ToList();

        var alreadyFetched = (await db.AnimeAiringSyncs.AsNoTracking()
            .Where(s => animeIds.Contains(s.AnimeId) && s.LastFetchedAt != null)
            .Select(s => s.AnimeId)
            .ToListAsync(ct))
            .ToHashSet();

        return distinctAnime
            .Where(a => !(a.AiringStatus == "finished_airing" && alreadyFetched.Contains(a.Id)))
            .Select(a => a.Id)
            .ToList();
    }

    public async Task BackfillAsync(CancellationToken ct = default)
    {
        var targets = await GetAllMyListAnimeIdsAsync(ct);
        if (targets.Count == 0)
        {
            await MarkBackfillCompleteAsync(ct);
            return;
        }

        // Resumable: an anime with a LastFetchedAt already set was fetched by
        // an earlier, interrupted run of this same backfill — skip it rather
        // than re-fetching. A prior fetch that threw leaves no such row, so it
        // is retried here.
        var alreadyFetched = await db.AnimeAiringSyncs.AsNoTracking()
            .Where(s => targets.Contains(s.AnimeId) && s.LastFetchedAt != null)
            .Select(s => s.AnimeId)
            .ToListAsync(ct);
        var remaining = targets.Except(alreadyFetched).ToList();

        var titleById = await db.AnimeMetadata.AsNoTracking()
            .Where(a => remaining.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.Title, ct);

        foreach (var animeId in remaining)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var count = await RefreshOneCoreAsync(animeId, ct);
                if (count == 0)
                    logger.LogInformation(
                        "Backfill: AniList returned no airing data for anime {AnimeId} ({Title}).",
                        animeId, titleById.GetValueOrDefault(animeId, "unknown"));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Backfill fetch failed for anime {AnimeId} ({Title}).", animeId, titleById.GetValueOrDefault(animeId, "unknown"));
            }
        }

        var fetchedCount = await db.AnimeAiringSyncs.AsNoTracking()
            .CountAsync(s => targets.Contains(s.AnimeId) && s.LastFetchedAt != null, ct);
        if (fetchedCount == targets.Count)
            await MarkBackfillCompleteAsync(ct);
    }

    private async Task MarkBackfillCompleteAsync(CancellationToken ct)
    {
        var state = await GetOrCreateStateAsync(ct);
        state.BackfillCompletedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private async Task<AiringRefreshState> GetOrCreateStateAsync(CancellationToken ct)
    {
        var state = await db.AiringRefreshStates.FirstOrDefaultAsync(ct);
        if (state is not null)
            return state;

        state = new AiringRefreshState();
        db.AiringRefreshStates.Add(state);
        return state;
    }

    private async Task<int> RefreshOneCoreAsync(int animeId, CancellationToken ct)
    {
        var anime = await db.AnimeMetadata.AsNoTracking().FirstOrDefaultAsync(a => a.Id == animeId, ct);
        if (anime is null)
            return 0; // the anime itself is gone — nothing to refresh

        var sync = await db.AnimeAiringSyncs.FirstOrDefaultAsync(s => s.AnimeId == animeId, ct);
        var now = DateTimeOffset.UtcNow;

        int? aniListId = sync?.AniListId;
        string? status = null;
        DateTimeOffset? nextAiringAt = null;
        // Preserved unless this call makes a fresh lookup below — a
        // subsequent refresh that skips straight to the schedule fetch
        // (aniListId already cached) didn't just re-ask about relations, so
        // it shouldn't bump this timestamp forward.
        var relationsFetchedAt = sync?.RelationsFetchedAt;

        // Resolve the AniList id once and cache it. A prior lookup that came
        // back "AniList doesn't know this anime" (AniListId null, LastFetchedAt
        // set) is not retried — sync.LastFetchedAt already being set is what
        // distinguishes that from "never looked up".
        if (sync is null || sync.LastFetchedAt is null)
        {
            var lookup = await aniList.LookupByMalIdAsync(animeId, ct);
            relationsFetchedAt = now;
            if (lookup is null)
            {
                await UpsertSyncAsync(sync, animeId, aniListId: null, now, nextAiringEpisodeAtUtc: null, hasCompleteData: false, nextRecheckAtUtc: null, relationsFetchedAt, ct);
                logger.LogInformation("AniList has no entry for anime {AnimeId} ({Title}).", animeId, anime.Title);
                return 0;
            }

            aniListId = lookup.AniListId;
            status = lookup.Status;
            nextAiringAt = lookup.NextAiringEpisodeAtUtc;
            // Relations ride along on this same lookup (relation-confidence
            // spec) — free, no extra AniList request.
            await relationStore.ReplaceAsync(animeId, lookup.Relations, ct);
        }

        if (aniListId is null)
            return 0; // known-absent from a prior lookup

        var schedule = await aniList.GetAiringScheduleAsync(aniListId.Value, ct);
        status = schedule.Status ?? status;
        nextAiringAt = schedule.NextAiringEpisodeAtUtc ?? nextAiringAt;

        var rows = schedule.Episodes
            .Select(e => new EpisodeAiring { AnimeId = animeId, Episode = e.Episode, AirsAtUtc = e.AirsAtUtc, FetchedAt = now })
            .ToList();
        await episodeAiringRepository.ReplaceForAnimeAsync(animeId, rows, ct);

        var (hasCompleteData, nextRecheckAtUtc) = ComputeRecheckState(anime.AiringStatus, nextAiringAt, now);
        if (!hasCompleteData)
            logger.LogInformation(
                "Anime {AnimeId} ({Title}) has incomplete AniList airing data (AniList status: {Status}); next recheck at {NextRecheckAtUtc}.",
                animeId, anime.Title, status ?? "unknown", nextRecheckAtUtc);

        await UpsertSyncAsync(sync, animeId, aniListId, now, nextAiringAt, hasCompleteData, nextRecheckAtUtc, relationsFetchedAt, ct);

        return schedule.Episodes.Count;
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
