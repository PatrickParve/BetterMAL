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
    // Calendar order, not MAL/DB order — RefreshYearAsync walks the year's
    // four seasons in this order (design D2).
    private static readonly string[] SeasonsInYearOrder = ["winter", "spring", "summer", "fall"];

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

    // Fetched at most once per interval set by the season's own age, measured
    // from the start of its quarter (SeasonRefreshCadence) — a never-fetched
    // season always fetches, at any age. A failed fetch never counts, because
    // only FetchAndCacheAsync writes the stamp and it isn't reached on the
    // failure path. Single-flight via RefreshGate: a waiter re-checks
    // LastFetchedAt inside the lock, so it asks the same cadence question and
    // sees the first refresh's stamp as fresh, skipping a second MAL fetch
    // instead of racing it.
    public async Task<SeasonRefreshResultDto> RefreshAsync(int year, string season, CancellationToken ct = default)
    {
        using (await refreshGate.LockAsync($"season:{year}:{season}", ct))
        {
            var now = DateTimeOffset.UtcNow;
            var todayLocalDate = broadcastConverter.GetLocalDate(now);
            var lastFetched = await seasonRepository.GetLastFetchedAsync(year, season, ct);

            if (lastFetched is { } fetchedAt &&
                SeasonRefreshCadence.IsFresh(year, season, broadcastConverter.GetLocalDate(fetchedAt), todayLocalDate))
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

    public async Task<YearPageDto> GetYearPageAsync(int year, string sortKey, bool includeMyList, bool hideHentai, IReadOnlyCollection<string>? types, int offset, int limit, CancellationToken ct = default)
    {
        var points = SeasonsInYearOrder.Select(season => (year, season)).ToList();

        var (items, totalCount) = await seasonRepository.GetPageAsync(points, ParseSort(sortKey), includeMyList, hideHentai, types, offset, limit, ct);
        var dtoItems = items
            .Select(i => new AnimeBrowseItemDto(i.AnimeId, i.Title, i.EnglishTitle, i.PictureUrl, i.TotalEpisodes, i.MediaType, i.MalScore, i.PopularityRank, i.MyScore, i.InMyList))
            .ToList();

        var hasListing = await seasonRepository.HasListingAsync(points, ct);
        var lastFetchedAt = await GetLatestFetchedAtAsync(points, ct);

        return new YearPageDto(year, dtoItems, offset, limit, totalCount, lastFetchedAt, hasListing);
    }

    // A year's own DbContext is scoped per request and not thread-safe, so
    // the four season refreshes below must run one after another rather than
    // concurrently (design D2) — that also means most visits do nothing more
    // than four cheap freshness checks, since RefreshAsync's own cadence gate
    // short-circuits before touching MAL. Looping RefreshAsync over the four
    // seasons is also what gives a year its per-season intervals with no new
    // code: each season answers the freshness question on its own age, so a
    // year straddling an age boundary may fetch some seasons and skip others.
    public async Task<YearRefreshResultDto> RefreshYearAsync(int year, CancellationToken ct = default)
    {
        var outcomes = new List<SeasonRefreshOutcome>();
        foreach (var season in SeasonsInYearOrder)
        {
            var result = await RefreshAsync(year, season, ct);
            outcomes.Add(result.Outcome);
        }

        return new YearRefreshResultDto(FoldOutcomes(outcomes));
    }

    // The precedence that makes a year outcome mean for a year what the
    // corresponding season outcome means for a season (design D2): any
    // Fetched outranks everything since new data always warrants a re-read;
    // Skipped outranks NotListed/Failed since a year with even one season
    // already current today is current, not unlisted or broken; NotListed
    // outranks Failed so a genuinely unopened year reports the honest
    // "not listed" rather than a retry message; only a year where every
    // season failed is reported as failed.
    private static SeasonRefreshOutcome FoldOutcomes(IReadOnlyCollection<SeasonRefreshOutcome> outcomes)
    {
        if (outcomes.Contains(SeasonRefreshOutcome.Fetched)) return SeasonRefreshOutcome.Fetched;
        if (outcomes.Contains(SeasonRefreshOutcome.Skipped)) return SeasonRefreshOutcome.Skipped;
        if (outcomes.Contains(SeasonRefreshOutcome.NotListed)) return SeasonRefreshOutcome.NotListed;
        return SeasonRefreshOutcome.Failed;
    }

    // The most recent of the four seasons' fetch stamps, or null if none of
    // them has ever been fetched — what lets the client's never-cached
    // loading state key off a year exactly as it does off a single season.
    private async Task<DateTimeOffset?> GetLatestFetchedAtAsync(IReadOnlyCollection<(int Year, string Season)> points, CancellationToken ct)
    {
        DateTimeOffset? latest = null;
        foreach (var (pointYear, pointSeason) in points)
        {
            var fetchedAt = await seasonRepository.GetLastFetchedAsync(pointYear, pointSeason, ct);
            if (fetchedAt is { } value && (latest is null || value > latest))
                latest = value;
        }
        return latest;
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
