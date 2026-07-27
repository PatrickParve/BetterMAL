namespace AnimeTracker.Api.Models;

/// <summary>One anime's place in the user's ordered "My top anime" preference
/// list. This is not a set of selected anime — every row carries a
/// <see cref="Position"/>, and the list is an ordered preference over all
/// scored anime the user has ever explicitly arranged. An anime's score tier
/// is always derived from its current score, not stored here, so re-scoring
/// an anime moves it between tiers without touching its stored order.
/// Anime with no row here fall back to alphabetical order within their tier,
/// after any explicitly ordered members.</summary>
public class TopAnimeSelection
{
    public int AnimeId { get; set; }
    public AnimeMetadata Anime { get; set; } = null!;
    public DateTimeOffset SelectedAt { get; set; }
    public int Position { get; set; }
}
