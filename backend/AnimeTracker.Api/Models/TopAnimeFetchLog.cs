namespace AnimeTracker.Api.Models;

/// <summary>Tracks when the Top Anime ranking was last live-fetched from MAL.
/// Unlike SeasonFetchLog there is no year/season dimension — at most one row
/// ever exists.</summary>
public class TopAnimeFetchLog
{
    public int Id { get; set; }
    public DateTimeOffset LastFetchedAt { get; set; }
}
