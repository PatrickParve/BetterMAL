namespace AnimeTracker.Api.Models;

/// <summary>Singleton bookkeeping row (one ever) for the airing-refresh
/// pipeline: when the last daily pass succeeded, whether the one-time
/// full-history backfill has completed, and which viewing season the last
/// pass ran in (so a season-quarter boundary can force an extra pass).</summary>
public class AiringRefreshState
{
    public int Id { get; set; }
    public DateTimeOffset? LastSuccessfulPassAtUtc { get; set; }
    public DateTimeOffset? BackfillCompletedAtUtc { get; set; }

    /// <summary>e.g. "2026-summer" — SeasonCalendar's (Year, Season) as one string.</summary>
    public string? LastPassSeason { get; set; }
}
