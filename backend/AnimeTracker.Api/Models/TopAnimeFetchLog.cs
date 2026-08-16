namespace AnimeTracker.Api.Models;

/// <summary>Tracks when a Top Anime ranking list was last live-fetched from
/// MAL. Keyed by ranking list (see
/// <see cref="Services.Library.TopAnimeRankingType"/>) — at most one row per
/// list ever exists.</summary>
public class TopAnimeFetchLog
{
    public required string RankingType { get; set; }
    public DateTimeOffset LastFetchedAt { get; set; }
}
