using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Scheduling;
using AnimeTracker.Api.Services.Season;

namespace AnimeTracker.Api.Services.Recap;

/// <summary>The recap picker's whole-list summary (design.md decision 2):
/// per-year counts under both filters, and per-season "aired" counts. Reads
/// inclusion through <see cref="RecapEntrySelector"/> so this can never
/// disagree with the recap itself about which entries qualify — including
/// the "watched" filter's logged-progress arm (design.md decision 3), so a
/// year holding only logged progress is offered rather than reported
/// empty.</summary>
public class RecapAvailabilityService(
    IUserAnimeEntryRepository entryRepository,
    IActivityLogRepository activityLogRepository,
    IBroadcastLocalTimeConverter localTimeConverter) : IRecapAvailabilityService
{
    public async Task<RecapAvailabilityDto> GetAvailabilityAsync(CancellationToken ct = default)
    {
        var entries = await entryRepository.GetAllAsync(ct);

        // The whole log, bucketed by the local calendar year each row falls
        // in — a year with no completion or air-start date at all can still
        // hold nothing but logged progress, so this can't be derived from
        // `entries` alone (design.md decision 3).
        var allLogRows = await activityLogRepository.GetEpisodeProgressInRangeAsync(DateTimeOffset.MinValue, DateTimeOffset.MaxValue, ct);
        var watchedAnimeIdsByYear = allLogRows
            .GroupBy(row => localTimeConverter.GetLocalDate(row.Timestamp).Year)
            .Select(g => (Year: g.Key, Ids: (IReadOnlySet<int>)RecapWatchLog.BuildMap(g.ToList()).Keys.ToHashSet()))
            // A year whose only rows are logged decreases has no survivor
            // once RecapWatchLog discards them — it must not surface as a
            // year candidate with a phantom zero count.
            .Where(x => x.Ids.Count > 0)
            .ToDictionary(x => x.Year, x => x.Ids);

        var watchedByYear = CountWatchedByYear(entries, watchedAnimeIdsByYear);
        var airedByYear = CountByYear(entries, RecapTimeFilter.Aired);

        var years = watchedByYear.Keys
            .Union(airedByYear.Keys)
            .OrderBy(year => year)
            .Select(year => new RecapYearAvailabilityDto(year, watchedByYear.GetValueOrDefault(year), airedByYear.GetValueOrDefault(year)))
            .ToList();

        var seasons = entries
            .Select(e => RecapEntrySelector.AttributionDate(e, RecapTimeFilter.Aired))
            .Where(date => date is not null)
            .Select(date => SeasonCalendar.GetSeasonFor(date!.Value))
            .GroupBy(point => point)
            .OrderBy(g => SeasonCalendar.GetSeasonPointIndex(g.Key.Year, g.Key.Season))
            .Select(g => new RecapSeasonAvailabilityDto(g.Key.Year, g.Key.Season, g.Count()))
            .ToList();

        return new RecapAvailabilityDto(years, seasons);
    }

    private static Dictionary<int, int> CountByYear(List<UserAnimeEntry> entries, string filter) =>
        entries
            .Select(e => RecapEntrySelector.AttributionDate(e, filter))
            .Where(date => date is not null)
            .GroupBy(date => date!.Value.Year)
            .ToDictionary(g => g.Key, g => g.Count());

    // Every year either a completion date or logged progress could place an
    // entry in — not just the years derived from completion dates — each
    // resolved through RecapEntrySelector.Select so this agrees with the
    // recap itself about which entries a "watched" year holds.
    private static Dictionary<int, int> CountWatchedByYear(
        List<UserAnimeEntry> entries, Dictionary<int, IReadOnlySet<int>> watchedAnimeIdsByYear)
    {
        var completionYears = entries
            .Select(e => RecapEntrySelector.AttributionDate(e, RecapTimeFilter.Watched))
            .Where(date => date is not null)
            .Select(date => date!.Value.Year);

        return completionYears.Union(watchedAnimeIdsByYear.Keys)
            .Distinct()
            .ToDictionary(
                year => year,
                year => RecapEntrySelector.Select(
                    entries, RecapPeriod.Yearly(year), RecapTimeFilter.Watched,
                    watchedAnimeIdsByYear.GetValueOrDefault(year, EmptyAnimeIds)).Count);
    }

    private static readonly HashSet<int> EmptyAnimeIds = [];
}
