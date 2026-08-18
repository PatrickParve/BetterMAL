using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Season;

namespace AnimeTracker.Api.Services.Recap;

/// <summary>The recap picker's whole-list summary (design.md decision 2):
/// per-year counts under both filters, and per-season "aired" counts. Reads
/// inclusion through <see cref="RecapEntrySelector.AttributionDate"/> so
/// this can never disagree with the recap itself about which entries
/// qualify.</summary>
public class RecapAvailabilityService(IUserAnimeEntryRepository entryRepository) : IRecapAvailabilityService
{
    public async Task<RecapAvailabilityDto> GetAvailabilityAsync(CancellationToken ct = default)
    {
        var entries = await entryRepository.GetAllAsync(ct);

        var watchedByYear = CountByYear(entries, RecapTimeFilter.Watched);
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
}
