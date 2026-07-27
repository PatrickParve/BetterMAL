using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Library;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Season;

public class SeasonBrowseService(
    AnimeTrackerDbContext db,
    IMalClient malClient,
    ISeasonRepository seasonRepository,
    IBroadcastLocalTimeConverter broadcastConverter,
    SeasonRefreshGate refreshGate,
    ILogger<SeasonBrowseService> logger) : ISeasonBrowseService
{
    public async Task<SeasonPageDto> GetPageAsync(int year, string season, string sortKey, bool includeMyList, int offset, int limit, CancellationToken ct = default)
    {
        var (items, totalCount) = await seasonRepository.GetPageAsync(year, season, ParseSort(sortKey), includeMyList, offset, limit, ct);
        var dtoItems = items
            .Select(i => new AnimeBrowseItemDto(i.AnimeId, i.Title, i.EnglishTitle, i.PictureUrl, i.TotalEpisodes, i.MediaType, i.MalScore, i.PopularityRank, i.MyScore, i.InMyList))
            .ToList();

        var lastFetchedAt = await seasonRepository.GetLastFetchedAsync(year, season, ct);

        return new SeasonPageDto(year, season, dtoItems, offset, limit, totalCount, lastFetchedAt);
    }

    // Fetches at most once per local calendar day, for any season — past,
    // current, or upcoming alike. Single-flight via SeasonRefreshGate: a
    // waiter re-checks LastFetchedAt inside the lock, so it sees the first
    // refresh's stamp and skips a second MAL fetch instead of racing it.
    public async Task<SeasonRefreshResultDto> RefreshAsync(int year, string season, CancellationToken ct = default)
    {
        using (await refreshGate.LockAsync(year, season, ct))
        {
            var now = DateTimeOffset.UtcNow;
            var todayLocalDate = broadcastConverter.GetLocalDate(now);
            var lastFetched = await seasonRepository.GetLastFetchedAsync(year, season, ct);

            if (lastFetched is { } fetchedAt && broadcastConverter.GetLocalDate(fetchedAt) == todayLocalDate)
                return new SeasonRefreshResultDto(false);

            try
            {
                await FetchAndCacheAsync(year, season, now, ct);
                return new SeasonRefreshResultDto(true);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to refresh season {Year}/{Season}; serving whatever is already cached.", year, season);
                return new SeasonRefreshResultDto(false);
            }
        }
    }

    private async Task FetchAndCacheAsync(int year, string season, DateTimeOffset now, CancellationToken ct)
    {
        var edges = await malClient.GetFullSeasonAsync(year, season, ct: ct);
        var animeIds = edges.Select(e => e.Node.Id).Distinct().ToList();
        var startSeasonById = edges
            .GroupBy(e => e.Node.Id)
            .ToDictionary(g => g.Key, g => g.First().Node.StartSeason);

        var existingAnime = await db.AnimeMetadata
            .Where(a => animeIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, ct);

        foreach (var edge in edges)
        {
            AnimeMetadata tracked;
            if (existingAnime.TryGetValue(edge.Node.Id, out var existing))
            {
                edge.Node.ApplyLeanTo(existing, now);
                tracked = existing;
            }
            else
            {
                tracked = edge.Node.ToLeanAnimeMetadata(now);
                db.AnimeMetadata.Add(tracked);
                existingAnime[edge.Node.Id] = tracked;
            }

            // ApplyLeanTo deliberately skips detail-page fields, but AiredFrom is
            // the same value regardless of source, so it's safe to set here —
            // required so a listing can be classified to its premiere season.
            tracked.AiredFrom = MalMappingExtensions.ParseMalDate(edge.Node.StartDate);
        }

        var existingListingIds = (await db.SeasonAnimeListings
            .Where(l => l.Year == year && l.Season == season)
            .Select(l => l.AnimeId)
            .ToListAsync(ct)).ToHashSet();

        foreach (var animeId in animeIds)
        {
            if (existingListingIds.Contains(animeId))
                continue;

            // MAL's own start_season is authoritative — an anime belongs to the
            // season MAL files it under, which can differ from the quarter its
            // start_date falls in (e.g. an early-June premiere MAL lists as summer).
            // Only exclude when MAL explicitly classifies it under a *different*
            // season (e.g. a continuing long-runner from a past season); a missing
            // start_season falls back to trusting the season endpoint that returned it.
            var startSeason = startSeasonById.GetValueOrDefault(animeId);
            if (startSeason is not null &&
                (startSeason.Year != year || !string.Equals(startSeason.Season, season, StringComparison.OrdinalIgnoreCase)))
                continue;

            db.SeasonAnimeListings.Add(new SeasonAnimeListing { Year = year, Season = season, AnimeId = animeId });
        }

        var fetchLog = await db.SeasonFetchLogs.FirstOrDefaultAsync(f => f.Year == year && f.Season == season, ct);
        if (fetchLog is null)
            db.SeasonFetchLogs.Add(new SeasonFetchLog { Year = year, Season = season, LastFetchedAt = now });
        else
            fetchLog.LastFetchedAt = now;

        await db.SaveChangesAsync(ct);
    }

    private static SeasonSortKey ParseSort(string sortKey) => sortKey switch
    {
        "malScore" => SeasonSortKey.MalScore,
        "myScore" => SeasonSortKey.MyScore,
        "alphabetical" => SeasonSortKey.Alphabetical,
        _ => SeasonSortKey.Popularity,
    };
}
