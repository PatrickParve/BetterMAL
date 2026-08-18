using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Services.Recap;

/// <summary>The recap's two time filters, as plain validated strings (like
/// <see cref="RecapMode"/>).</summary>
public static class RecapTimeFilter
{
    public const string Watched = "watched";
    public const string Aired = "aired";

    public static readonly IReadOnlyList<string> SupportedValues = [Watched, Aired];

    public static bool IsSupported(string? filter) => filter is not null && SupportedValues.Contains(filter);
}

/// <summary>The single home of the recap's inclusion rules (design.md
/// Goals), so the recap itself and the my-list scope it hands off to
/// (design.md decision 9) can never disagree about which anime a period
/// holds.</summary>
public static class RecapEntrySelector
{
    private static readonly HashSet<WatchStatus> WatchedStatuses = [WatchStatus.Completed, WatchStatus.Dropped];
    private static readonly HashSet<WatchStatus> AiredStatuses =
        [WatchStatus.Completed, WatchStatus.Dropped, WatchStatus.OnHold, WatchStatus.Watching];

    /// <summary>The date an entry is placed on the calendar under the given
    /// filter — its completion date for "watched", its anime's air-start
    /// date for "aired" — or null when the entry doesn't qualify under that
    /// filter at all: wrong status (Plan-to-watch never qualifies for
    /// either), or the relevant date is unknown (design.md decision 5: no
    /// <c>AiredFrom</c> excludes an entry from "aired" but not from
    /// "watched"). The one place both the period selection below and
    /// <c>RecapAvailabilityService</c>'s whole-list counts read this rule
    /// from.</summary>
    public static DateOnly? AttributionDate(UserAnimeEntry entry, string filter) => filter switch
    {
        RecapTimeFilter.Watched => WatchedStatuses.Contains(entry.Status) ? entry.CompletedAt : null,
        RecapTimeFilter.Aired => AiredStatuses.Contains(entry.Status) ? entry.Anime.AiredFrom : null,
        _ => throw new ArgumentOutOfRangeException(nameof(filter), filter, "Unknown recap time filter."),
    };

    public static List<UserAnimeEntry> Select(List<UserAnimeEntry> entries, RecapPeriod period, string filter)
    {
        var (start, end) = period.DateRange;
        return entries.Where(e => AttributionDate(e, filter) is { } date && date >= start && date <= end).ToList();
    }
}
