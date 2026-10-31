namespace AnimeTracker.Api.Models;

/// <summary>Singleton bookkeeping row (one ever) for the weekly AnimeThemes
/// catalogue sync, kept apart from <see cref="AnimeIdMappingSyncState"/> because
/// the two syncs run on their own schedules. <c>LastSyncedAt</c> is the last
/// successful refresh, the weekly clock that survives a restart.
/// <c>LastAttemptAt</c> is the last try, successful or not, which spaces out
/// retries after a failure.</summary>
public class AnimeThemesSyncState
{
    public int Id { get; set; }
    public DateTimeOffset? LastSyncedAt { get; set; }
    public DateTimeOffset? LastAttemptAt { get; set; }
}
