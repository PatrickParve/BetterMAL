using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Entries;
using AnimeTracker.Api.Services.Infrastructure;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Scheduling;
using AnimeTracker.Api.Services.Updates;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Library;

public class TopAnimeService(
    AnimeTrackerDbContext db,
    IMalClient malClient,
    IAnimeMetadataChangeDetector changeDetector,
    ITopAnimeRepository topAnimeRepository,
    IEpisodeScheduleService scheduleService,
    IBroadcastLocalTimeConverter broadcastConverter,
    RefreshGate refreshGate,
    ILogger<TopAnimeService> logger) : ITopAnimeService
{
    private const int RankingSize = 500;

    public async Task<List<TopAnimeItemDto>> GetRankingAsync(string rankingType, CancellationToken ct = default)
    {
        await EnsureFreshAsync(rankingType, ct);

        var rows = await topAnimeRepository.GetRankingAsync(rankingType, ct);

        // Resolved for the whole ranking in one bulk query rather than one
        // per row (task 4.2) — the ranking runs up to 500 rows.
        var airedSoFarByAnimeId = await scheduleService.EpisodesAiredAsOfAsync(
            rows.Select(r => r.Anime).ToList(), DateTimeOffset.UtcNow, ct);

        return rows
            .Select(r => new TopAnimeItemDto(
                r.Rank,
                r.AnimeId,
                r.Anime.Title,
                r.Anime.EnglishTitle,
                r.Anime.PictureUrl,
                r.Anime.TotalEpisodes,
                r.Anime.MalScore,
                r.Anime.UserEntry is null ? null : UserAnimeEntryDto.FromEntity(r.Anime.UserEntry),
                r.Anime.AiringStatus,
                airedSoFarByAnimeId.TryGetValue(r.AnimeId, out var aired) ? aired : null))
            .ToList();
    }

    // This method IS the visit path (nothing else calls it), so a list
    // that's never visited is never fetched. Once visited, it refreshes again
    // at most once per local calendar day. Single-flight via RefreshGate: a
    // waiter re-checks IsFreshAsync inside the lock, so it sees the first
    // refresh's stamp and skips a second MAL fetch instead of racing it. The
    // gate key is per ranking type, so different lists refresh in parallel.
    private async Task EnsureFreshAsync(string rankingType, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var todayLocalDate = broadcastConverter.GetLocalDate(now);

        if (await IsFreshAsync(rankingType, todayLocalDate, ct))
            return;

        using (await refreshGate.LockAsync($"top-anime:{rankingType}", ct))
        {
            if (await IsFreshAsync(rankingType, todayLocalDate, ct))
                return;

            try
            {
                await FetchAndCacheAsync(rankingType, now, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to live-fetch the {RankingType} Top Anime ranking; serving whatever is already cached.", rankingType);
            }
        }
    }

    private async Task<bool> IsFreshAsync(string rankingType, DateOnly todayLocalDate, CancellationToken ct)
    {
        var lastFetched = await topAnimeRepository.GetLastFetchedAsync(rankingType, ct);
        return lastFetched is { } fetchedAt && broadcastConverter.GetLocalDate(fetchedAt) == todayLocalDate;
    }

    private async Task FetchAndCacheAsync(string rankingType, DateTimeOffset now, CancellationToken ct)
    {
        var response = await malClient.GetRankingAsync(rankingType: rankingType, limit: RankingSize, ct: ct);
        var edges = response.Data;
        var animeIds = edges.Select(e => e.Node.Id).Distinct().ToList();

        var existingAnime = await db.AnimeMetadata
            .Where(a => animeIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, ct);

        foreach (var edge in edges)
        {
            if (existingAnime.TryGetValue(edge.Node.Id, out var tracked))
            {
                // A lean write to an existing row detects the kinds its own
                // fields can produce (spec "Every path that writes anime data
                // detects the updates it can") — here that is the episode
                // count alone, the only detected field a ranking listing
                // writes. SnapshotListing, not Snapshot: nothing here Includes
                // RelatedAnime, and a lean write never touches it. A row that
                // has never been fully fetched records nothing; the detector
                // enforces that itself, from the snapshot.
                var before = changeDetector.SnapshotListing(tracked);
                edge.Node.ApplyLeanTo(tracked, now);
                await changeDetector.RecordAsync(tracked, before, now, ct);
            }
            else
            {
                // Created rows are a first observation, not a reveal, so they
                // stay outside detection — the same skip every other insert
                // branch makes.
                var created = edge.Node.ToLeanAnimeMetadata(now);
                db.AnimeMetadata.Add(created);
                existingAnime[edge.Node.Id] = created;
            }
        }

        // Update ranks in place rather than delete-then-recreate, so an anime
        // that stays in the ranking keeps the same tracked row. Scoped to this
        // ranking type's own rows, so refreshing one list never touches another.
        var existingRanking = await db.TopAnimeRankingEntries
            .Where(r => r.RankingType == rankingType)
            .ToDictionaryAsync(r => r.AnimeId, ct);
        var seenIds = new HashSet<int>();
        var position = 0;

        foreach (var edge in edges)
        {
            position++;
            var animeId = edge.Node.Id;
            seenIds.Add(animeId);
            var rank = edge.Ranking?.Rank ?? position;

            if (existingRanking.TryGetValue(animeId, out var existingEntry))
                existingEntry.Rank = rank;
            else
                db.TopAnimeRankingEntries.Add(new TopAnimeRankingEntry { RankingType = rankingType, AnimeId = animeId, Rank = rank });
        }

        var stale = existingRanking.Values.Where(e => !seenIds.Contains(e.AnimeId));
        db.TopAnimeRankingEntries.RemoveRange(stale);

        var fetchLog = await db.TopAnimeFetchLogs.FirstOrDefaultAsync(f => f.RankingType == rankingType, ct);
        if (fetchLog is null)
            db.TopAnimeFetchLogs.Add(new TopAnimeFetchLog { RankingType = rankingType, LastFetchedAt = now });
        else
            fetchLog.LastFetchedAt = now;

        await db.SaveChangesAsync(ct);
    }
}
