namespace AnimeTracker.Api.Models;

/// <summary>One MyAnimeList score the system has detected for one anime, and
/// the moment it detected it (add-score-history-graph design.md D1). Written
/// only when the score differs from the anime's latest record, so the table
/// holds an anime's first score and its changes, not one row per fetch. MAL
/// publishes only the current score, so nothing earlier than the first record
/// can ever be shown. The series-level line is never stored: it is worked out
/// on read from these rows, so a rebuild corrects the whole line at once.</summary>
public class AnimeScoreRecord
{
    public long Id { get; set; }
    public int AnimeId { get; set; } // MAL id
    public double Score { get; set; } // like AnimeMetadata.MalScore
    public DateTimeOffset DetectedAt { get; set; }
}
