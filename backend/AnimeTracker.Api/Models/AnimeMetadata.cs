namespace AnimeTracker.Api.Models;

/// <summary>Cached copy of a MAL anime record. Raw MAL vocabulary fields (MediaType,
/// AiringStatus, BroadcastDayOfWeek) are kept as strings rather than enums so an
/// unrecognized upstream value doesn't break ingestion.</summary>
public class AnimeMetadata
{
    public int Id { get; set; } // MAL anime id

    public required string Title { get; set; }
    public string? EnglishTitle { get; set; }
    public string? PictureUrl { get; set; }
    public double? MalScore { get; set; }
    public string? MediaType { get; set; } // tv, movie, ova, ona, special, music, unknown
    public string? AiringStatus { get; set; } // currently_airing, finished_airing, not_yet_aired
    public string? Rating { get; set; } // g, pg, pg_13, r, r+, rx — rx means Hentai
    public int? TotalEpisodes { get; set; } // null = unknown/still airing
    public DateOnly? AiredFrom { get; set; }
    public DateOnly? AiredTo { get; set; }
    public string? Studio { get; set; }
    public string? BroadcastDayOfWeek { get; set; } // JST, e.g. "mondays"
    public TimeOnly? BroadcastTime { get; set; } // JST
    public int? PopularityRank { get; set; }
    public int? Rank { get; set; }
    public DateTimeOffset LastSyncedAt { get; set; }
    public DateTimeOffset? LastScoreSyncedAt { get; set; }

    // Detail-page fields (rich/full detail only — never touched by a lean upsert).
    public List<string>? Genres { get; set; }
    public string? Synopsis { get; set; }
    public string? Background { get; set; }
    public int? AverageEpisodeDurationSeconds { get; set; }
    public string? Source { get; set; } // e.g. manga, original, light_novel

    // Related-anime links (rich/full detail only — never touched by a lean upsert).
    public List<AnimeRelatedAnime> RelatedAnime { get; set; } = [];

    public UserAnimeEntry? UserEntry { get; set; }
}
