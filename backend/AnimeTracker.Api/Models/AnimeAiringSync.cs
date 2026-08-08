namespace AnimeTracker.Api.Models;

/// <summary>Per-anime AniList sync bookkeeping — kept separate from
/// AnimeMetadata (the MAL mirror, overwritten wholesale on every MAL refresh)
/// so refreshing MAL metadata never clobbers AniList sync state.</summary>
public class AnimeAiringSync
{
    public int AnimeId { get; set; } // MAL id

    /// <summary>AniList's Media id for this MAL id. Null with a non-null
    /// LastFetchedAt records "AniList has no entry for this anime" (looked up
    /// and confirmed absent), distinct from "never looked up".</summary>
    public int? AniListId { get; set; }

    public DateTimeOffset? LastFetchedAt { get; set; }

    /// <summary>AniList's most recently reported nextAiringEpisode.airingAt.</summary>
    public DateTimeOffset? NextAiringEpisodeAtUtc { get; set; }

    /// <summary>False when the show is still airing but AniList reports no
    /// nextAiringEpisode, or the last reported one has passed with no newer
    /// data — drives the 3-day out-of-band recheck cadence.</summary>
    public bool HasCompleteData { get; set; }

    /// <summary>When this anime is next due for an out-of-band recheck; null
    /// means never due (covered by the daily pass, or finished airing).</summary>
    public DateTimeOffset? NextRecheckAtUtc { get; set; }
}
