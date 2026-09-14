namespace AnimeTracker.Api.Models;

/// <summary>Cached copy of a MAL anime record. Raw MAL vocabulary fields (MediaType,
/// AiringStatus, BroadcastDayOfWeek) are kept as strings rather than enums so an
/// unrecognized upstream value doesn't break ingestion. Not strictly a MAL mirror
/// throughout: PictureUrl and TotalEpisodes are each an effective value beside the
/// source value(s) they derive from — the chosen picture and MAL's for the former,
/// MAL's total when MAL has one, else AniList's, for the latter.</summary>
public class AnimeMetadata
{
    public int Id { get; set; } // MAL anime id

    public required string Title { get; set; }
    public string? EnglishTitle { get; set; }

    // The chosen picture, null meaning "no choice; follow MAL". Written only
    // by ArtworkSelectionService, which also stamps SelectedPictureModifiedAt
    // in the same write.
    public string? SelectedPictureUrl { get; set; }
    // When SelectedPictureUrl was last set or cleared; null means neither has
    // ever happened for this anime.
    public DateTimeOffset? SelectedPictureModifiedAt { get; set; }

    // The picture to display, derived from the pair above: the chosen
    // picture where there is one, MAL's main picture otherwise.
    // ResolvePictureUrl() is the only writer — same shape as the
    // TotalEpisodes trio below.
    public string? PictureUrl { get; set; }
    public string? MalPictureUrl { get; set; }
    public List<string>? PictureUrls { get; set; } // every picture MAL publishes, MAL's order, my-list anime only

    // Null until PictureUrls has been fetched at least once. Kept apart from
    // LastSyncedAt because "never asked for pictures" and "asked, MAL has
    // one" both leave PictureUrls looking the same otherwise.
    public DateTimeOffset? PicturesSyncedAt { get; set; }

    public double? MalScore { get; set; }
    public string? MediaType { get; set; } // tv, movie, ova, ona, special, music, unknown
    public string? AiringStatus { get; set; } // currently_airing, finished_airing, not_yet_aired
    public string? Rating { get; set; } // g, pg, pg_13, r, r+, rx — rx means Hentai
    // The episode total to display: MAL's own reported total when MAL has
    // one, AniList's otherwise (Services/Mal/MalMappingExtensions,
    // Services/Airing/EpisodeScheduleRefreshService). MalTotalEpisodes is
    // what MAL says; the two differ exactly when MAL reports none and
    // AniList fills the gap. ResolveTotalEpisodes() is the only writer of
    // TotalEpisodes — same shape as the PictureUrl/MalPictureUrl pair above.
    public int? TotalEpisodes { get; set; } // null = unknown/still airing
    public int? MalTotalEpisodes { get; set; } // MAL's own reported total, null = MAL doesn't know
    public int? AniListTotalEpisodes { get; set; } // AniList's reported total, null = not fetched or unknown
    public DateOnly? AiredFrom { get; set; }
    public DateOnly? AiredTo { get; set; }
    public string? Studio { get; set; }
    public string? BroadcastDayOfWeek { get; set; } // JST, e.g. "mondays"
    public TimeOnly? BroadcastTime { get; set; } // JST
    public int? PopularityRank { get; set; }
    public int? Rank { get; set; }
    public DateTimeOffset LastSyncedAt { get; set; } // last full-detail fetch; drives RefreshTiers

    // Set when the scheduled refresh job's last attempt got a 404 from MAL;
    // ApplyTo clears it on the next successful full-detail fetch, by any
    // path. Only RefreshTiers.IsDue and the job's ordering read it. Kept
    // apart from LastSyncedAt, which means "has had a full fetch" — a 404
    // is not a fetch.
    public DateTimeOffset? LastRefreshFailedAt { get; set; }

    // Write-only marker of the last *lean listing* refresh (Season/Top-Anime
    // browsing) — nothing reads it since the tier ladder moved onto
    // LastSyncedAt. Deliberately not collapsed into LastSyncedAt: doing so
    // would make a lean browse stamp the full-detail timestamp and thereby
    // suppress both the tiered refresh and the detail page's TTL fetch for
    // every anime a season/top-anime page happens to touch.
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

    /// <summary>Re-derives <see cref="TotalEpisodes"/> from the two source
    /// columns — the only writer of that field. Called by whichever writer
    /// just touched a source column (MAL's own upsert, or the AniList
    /// refresh); MAL wins when it has a figure.</summary>
    public void ResolveTotalEpisodes() => TotalEpisodes = MalTotalEpisodes ?? AniListTotalEpisodes;

    /// <summary>Re-derives <see cref="PictureUrl"/> from the two source
    /// columns — the only writer of that field. Called by whichever writer
    /// just touched <see cref="SelectedPictureUrl"/> or <see cref="MalPictureUrl"/>;
    /// the choice wins when there is one.</summary>
    public void ResolvePictureUrl() => PictureUrl = SelectedPictureUrl ?? MalPictureUrl;
}
