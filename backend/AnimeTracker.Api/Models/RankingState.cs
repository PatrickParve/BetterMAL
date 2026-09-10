namespace AnimeTracker.Api.Models;

/// <summary>Singleton bookkeeping row (one ever) for the ranking's stored
/// order: when it was last written. An absent row, or a null
/// <see cref="ModifiedAt"/>, means the ranking has never been arranged on
/// this database.</summary>
public class RankingState
{
    public int Id { get; set; }
    public DateTimeOffset? ModifiedAt { get; set; }
}
