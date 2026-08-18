using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Infrastructure;
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
    RefreshGate refreshGate,
    ILogger<SeasonBrowseService> logger) : ISeasonBrowseService
{
    public async Task<SeasonPageDto> GetPageAsync(int year, string season, string sortKey, bool includeMyList, bool hideHentai, IReadOnlyCollection<string>? types, int offset, int limit, CancellationToken ct = default)
    {
        var (items, totalCount) = await seasonRepository.GetPageAsync(year, season, ParseSort(sortKey), includeMyList, hideHentai, types, offset, limit, ct);
        var dtoItems = items
            .Select(i => new AnimeBrowseItemDto(i.AnimeId, i.Title, i.EnglishTitle, i.PictureUrl, i.TotalEpisodes, i.MediaType, i.MalScore, i.PopularityRank, i.MyScore, i.InMyList))
            .ToList();

        var lastFetchedAt = await seasonRepository.GetLastFetchedAsync(year, season, ct);
        var hasListing = await seasonRepository.HasListingAsync(year, season, ct);

        return new SeasonPageDto(year, season, dtoItems, offset, limit, totalCount, lastFetchedAt, hasListing);
    }

    // Fetches at most once per local calendar day, for any season — past,
    // current, or upcoming alike. Single-flight via RefreshGate: a waiter
    // re-checks LastFetchedAt inside the lock, so it sees the first refresh's
    // stamp and skips a second MAL fetch instead of racing it.
    public async Task<SeasonRefreshResultDto> RefreshAsync(int year, string season, CancellationToken ct = default)
    {
        using (await refreshGate.LockAsync($"season:{year}:{season}", ct))
        {
            var now = DateTimeOffset.UtcNow;
            var todayLocalDate = broadcastConverter.GetLocalDate(now);
            var lastFetched = await seasonRepository.GetLastFetchedAsync(year, season, ct);

            if (lastFetched is { } fetchedAt && broadcastConverter.GetLocalDate(fetchedAt) == todayLocalDate)
                return new SeasonRefreshResultDto(SeasonRefreshOutcome.Skipped);

            try
            {
                var outcome = await FetchAndCacheAsync(year, season, now, ct);
                return new SeasonRefreshResultDto(outcome);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to refresh season {Year}/{Season}; serving whatever is already cached.", year, season);
                return new SeasonRefreshResultDto(SeasonRefreshOutcome.Failed);
            }
        }
    }

    public async Task<SeasonBoundsDto> GetBoundsAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var todayLocalDate = broadcastConverter.GetLocalDate(now);
        var current = SeasonCalendar.GetSeasonFor(todayLocalDate);

        // The candidate points the resolver can possibly need: the current
        // season plus MAL's default forward window. A season beyond that
        // window only ever raises the ceiling (via LatestCachedSeason, which
        // already carries its own "has a listing" fact) — it can never be the
        // reason the ceiling retreats, so it doesn't need its own point here.
        var candidatePoints = Enumerable.Range(0, SeasonHorizon.FutureSeasonWindow + 1)
            .Select(i => SeasonCalendar.Shift(current.Year, current.Season, i))
            .ToList();

        var inputs = await seasonRepository.GetHorizonInputsAsync(candidatePoints, ct);

        bool NotListedToday((int Year, string Season) point)
        {
            var match = inputs.Points.FirstOrDefault(p => p.Year == point.Year && p.Season == point.Season);
            return match is not null
                && match.LastFetchedAt is { } fetchedAt
                && broadcastConverter.GetLocalDate(fetchedAt) == todayLocalDate
                && !match.HasListings;
        }

        var ceiling = SeasonHorizon.Resolve(current, inputs.LatestCachedSeason, NotListedToday);
        return new SeasonBoundsDto(ceiling.Year, ceiling.Season);
    }

    private async Task<SeasonRefreshOutcome> FetchAndCacheAsync(int year, string season, DateTimeOffset now, CancellationToken ct)
    {
        var edges = await malClient.GetFullSeasonAsync(year, season, ct: ct);
        if (edges is null)
        {
            // MAL has no listing for this season (404) — not a failure. Still
            // stamp the fetch log: that's what puts an unopened season under
            // the once-per-day rule (so the next visit costs no MAL request)
            // and what gives the client a non-null LastFetchedAt, which is
            // what lets the page leave its never-cached loading state.
            await StampFetchLogAsync(year, season, now, ct);
            return SeasonRefreshOutcome.NotListed;
        }

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

        await StampFetchLogAsync(year, season, now, ct);
        return SeasonRefreshOutcome.Fetched;
    }

    private async Task StampFetchLogAsync(int year, string season, DateTimeOffset now, CancellationToken ct)
    {
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
