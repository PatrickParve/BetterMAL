namespace AnimeTracker.Api.Models;

/// <summary>One row of a cached Top Anime ranking list's snapshot. Each ranking
/// list (see <see cref="Services.Library.TopAnimeRankingType"/>) is kept in
/// sync independently (ranks updated in place, stale rows removed, new rows
/// added) on each visit-triggered refresh, since MAL's ranking order shifts
/// over time.</summary>
public class TopAnimeRankingEntry
{
    public required string RankingType { get; set; }
    public int AnimeId { get; set; }
    public AnimeMetadata Anime { get; set; } = null!;
    public int Rank { get; set; }
}
