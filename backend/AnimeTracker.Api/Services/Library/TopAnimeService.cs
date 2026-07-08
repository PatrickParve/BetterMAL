using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Entries;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Library;

public class TopAnimeService(
    AnimeTrackerDbContext db,
    IMalClient malClient,
    ITopAnimeRepository topAnimeRepository,
    IBroadcastLocalTimeConverter broadcastConverter,
    ILogger<TopAnimeService> logger) : ITopAnimeService
{
    private const int RankingSize = 100;

    public async Task<List<TopAnimeItemDto>> GetRankingAsync(CancellationToken ct = default)
    {
        await EnsureFreshAsync(ct);

        var rows = await topAnimeRepository.GetRankingAsync(ct);
        return rows
            .Select(r => new TopAnimeItemDto(
                r.Rank,
                r.AnimeId,
                r.Anime.Title,
                r.Anime.PictureUrl,
                r.Anime.TotalEpisodes,
                r.Anime.MalScore,
                r.Anime.UserEntry is null ? null : UserAnimeEntryDto.FromEntity(r.Anime.UserEntry)))
            .ToList();
    }

    // This method IS the visit path (nothing else calls it), so a ranking
    // that's never visited is never fetched. Once visited, it refreshes again
    // at most once per local calendar day.
    private async Task EnsureFreshAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var todayLocalDate = broadcastConverter.GetLocalDate(now);
        var lastFetched = await topAnimeRepository.GetLastFetchedAsync(ct);

        if (lastFetched is { } fetchedAt && broadcastConverter.GetLocalDate(fetchedAt) == todayLocalDate)
            return;

        try
        {
            await FetchAndCacheAsync(now, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to live-fetch the Top Anime ranking; serving whatever is already cached.");
        }
    }

    private async Task FetchAndCacheAsync(DateTimeOffset now, CancellationToken ct)
    {
        var response = await malClient.GetRankingAsync(limit: RankingSize, ct: ct);
        var edges = response.Data;
        var animeIds = edges.Select(e => e.Node.Id).Distinct().ToList();

        var existingAnime = await db.AnimeMetadata
            .Where(a => animeIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, ct);

        foreach (var edge in edges)
        {
            if (existingAnime.TryGetValue(edge.Node.Id, out var tracked))
            {
                edge.Node.ApplyLeanTo(tracked, now);
            }
            else
            {
                var created = edge.Node.ToLeanAnimeMetadata(now);
                db.AnimeMetadata.Add(created);
                existingAnime[edge.Node.Id] = created;
            }
        }

        // Update ranks in place rather than delete-then-recreate, so an anime
        // that stays in the ranking keeps the same tracked row.
        var existingRanking = await db.TopAnimeRankingEntries.ToDictionaryAsync(r => r.AnimeId, ct);
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
                db.TopAnimeRankingEntries.Add(new TopAnimeRankingEntry { AnimeId = animeId, Rank = rank });
        }

        var stale = existingRanking.Values.Where(e => !seenIds.Contains(e.AnimeId));
        db.TopAnimeRankingEntries.RemoveRange(stale);

        var fetchLog = await db.TopAnimeFetchLogs.FirstOrDefaultAsync(ct);
        if (fetchLog is null)
            db.TopAnimeFetchLogs.Add(new TopAnimeFetchLog { LastFetchedAt = now });
        else
            fetchLog.LastFetchedAt = now;

        await db.SaveChangesAsync(ct);
    }
}
