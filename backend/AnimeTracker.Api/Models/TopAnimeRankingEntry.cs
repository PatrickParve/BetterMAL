namespace AnimeTracker.Api.Models;

/// <summary>One row of the cached Top Anime ranking snapshot. The whole set is
/// kept in sync (ranks updated in place, stale rows removed, new rows added)
/// on each visit-triggered refresh, since MAL's ranking order shifts over
/// time.</summary>
public class TopAnimeRankingEntry
{
    public int AnimeId { get; set; }
    public AnimeMetadata Anime { get; set; } = null!;
    public int Rank { get; set; }
}
