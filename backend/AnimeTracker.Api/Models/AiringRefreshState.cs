namespace AnimeTracker.Api.Models;

/// <summary>Singleton bookkeeping row (one ever) for the airing-refresh
/// pipeline: when the last daily pass succeeded, and which viewing season the
/// last pass ran in (so a season-quarter boundary can force an extra pass).
/// Whether an anime still needs its airing history is not recorded here: it is
/// that anime's own airing-fetched mark (AnimeAiringSync.LastFetchedAt), so a
/// once-ever flag can never decide it.</summary>
public class AiringRefreshState
{
    public int Id { get; set; }
    public DateTimeOffset? LastSuccessfulPassAtUtc { get; set; }

    /// <summary>e.g. "2026-summer" — SeasonCalendar's (Year, Season) as one string.</summary>
    public string? LastPassSeason { get; set; }
}
