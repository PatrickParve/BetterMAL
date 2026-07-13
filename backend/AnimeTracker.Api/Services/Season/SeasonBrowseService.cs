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
    ILogger<SeasonBrowseService> logger) : ISeasonBrowseService
{
    public async Task<SeasonPageDto> GetPageAsync(int year, string season, string sortKey, int offset, int limit, CancellationToken ct = default)
    {
        await EnsureFreshAsync(year, season, ct);

        var (items, totalCount) = await seasonRepository.GetPageAsync(year, season, ParseSort(sortKey), offset, limit, ct);
        var dtoItems = items
            .Select(i => new AnimeBrowseItemDto(i.AnimeId, i.Title, i.EnglishTitle, i.PictureUrl, i.TotalEpisodes, i.MediaType, i.MalScore, i.PopularityRank, i.MyScore))
            .ToList();

        return new SeasonPageDto(year, season, dtoItems, offset, limit, totalCount);
    }

    // First visit of any season triggers a live fetch. After that, only the
    // current and immediately-upcoming season ever refresh again, and only
    // once per local calendar day — every other season (past or further out)
    // is served from cache indefinitely once cached.
    private async Task EnsureFreshAsync(int year, string season, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var todayLocalDate = broadcastConverter.GetLocalDate(now);
        var lastFetched = await seasonRepository.GetLastFetchedAsync(year, season, ct);

        if (lastFetched is { } fetchedAt)
        {
            if (!IsCurrentOrUpcoming(year, season, todayLocalDate))
                return;

            if (broadcastConverter.GetLocalDate(fetchedAt) == todayLocalDate)
                return;
        }

        try
        {
            await FetchAndCacheAsync(year, season, now, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to live-fetch season {Year}/{Season}; serving whatever is already cached.", year, season);
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

    private static bool IsCurrentOrUpcoming(int year, string season, DateOnly todayLocalDate)
    {
        var (currentYear, currentSeason) = SeasonCalendar.GetSeasonFor(todayLocalDate);
        if (year == currentYear && season == currentSeason)
            return true;

        var (upcomingYear, upcomingSeason) = SeasonCalendar.GetNextSeason(currentYear, currentSeason);
        return year == upcomingYear && season == upcomingSeason;
    }

    private static SeasonSortKey ParseSort(string sortKey) => sortKey switch
    {
        "malScore" => SeasonSortKey.MalScore,
        "myScore" => SeasonSortKey.MyScore,
        "alphabetical" => SeasonSortKey.Alphabetical,
        _ => SeasonSortKey.Popularity,
    };
}
