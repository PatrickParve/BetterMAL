namespace AnimeTracker.Api.Models;

/// <summary>Tracks when a season's listing was last live-fetched from MAL, so
/// the season browser can serve from cache except on first visit or (for the
/// current/upcoming season only) the first visit of a new local calendar
/// day.</summary>
public class SeasonFetchLog
{
    public int Year { get; set; }
    public required string Season { get; set; } // winter, spring, summer, fall
    public DateTimeOffset LastFetchedAt { get; set; }
}
