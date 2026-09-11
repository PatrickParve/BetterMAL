namespace AnimeTracker.Api.Models;

// Appended only, never reordered or renumbered — existing rows already carry
// these values in the database, the same rule ActivityChangeType follows.
// Two families with opposite duplication rules (design.md D2): Announced,
// EpisodeCountReleased and StartDateReleased are recorded at most once per
// anime, ever; StartDateChanged, BroadcastSlotChanged and EpisodesMoved carry
// no such limit and are recorded every time they happen.
[Flags]
public enum AnimeUpdateKinds
{
    Announced = 1,
    EpisodeCountReleased = 2,
    StartDateReleased = 4,
    StartDateChanged = 8,
    BroadcastSlotChanged = 16,
    EpisodesMoved = 32,
}

/// <summary>One card of news about an anime — every <see cref="AnimeUpdateKinds"/>
/// noticed for it in a single detection pass, merged into one row rather than
/// one row per kind (design.md D1). Current facts (episode count, premiere
/// date) are deliberately not stored here and are read live from
/// <see cref="AnimeMetadata"/>, so a later correction is reflected on an old
/// card instead of leaving it stating a superseded value (design.md D3). Only
/// the schedule-change kinds store anything, because for those the news *is*
/// the movement and the anime's current record holds only where it landed.</summary>
public class AnimeUpdate
{
    public long Id { get; set; }
    public int AnimeId { get; set; }
    public AnimeMetadata Anime { get; set; } = null!;
    public DateTimeOffset DetectedAt { get; set; }
    public AnimeUpdateKinds Kinds { get; set; }

    // Moved-from values for the schedule-change kinds (design.md D3) — never
    // re-derived from the anime's current record, which by then holds only
    // the value it moved to. The broadcast pair mirrors AnimeMetadata's own
    // JST day/time columns and is converted for display, the same as every
    // other broadcast time in the app.
    public DateOnly? PreviousStartDate { get; set; }
    public string? PreviousBroadcastDayOfWeek { get; set; } // JST, e.g. "mondays"
    public TimeOnly? PreviousBroadcastTime { get; set; } // JST

    // The earliest episode that moved, when Kinds includes EpisodesMoved, with
    // the local calendar dates it moved between (design.md D16).
    public int? MovedEpisode { get; set; }
    public DateOnly? PreviousEpisodeDate { get; set; }
    public DateOnly? NewEpisodeDate { get; set; }

    // A plain flag — when it was seen is not stored (store-seen-updates-on-server
    // design.md D1). New rows start unseen through the CLR default, so
    // AnimeUpdateRecorder needs no change.
    public bool Seen { get; set; }
}
