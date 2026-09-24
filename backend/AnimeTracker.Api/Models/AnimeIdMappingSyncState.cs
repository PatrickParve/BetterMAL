namespace AnimeTracker.Api.Models;

/// <summary>Singleton bookkeeping row (one ever) for the weekly id-mapping
/// sync. Kept apart from <see cref="AiringRefreshState"/>, which stays the
/// airing pipeline's own. <c>LastSyncedAt</c> is the last successful sync,
/// the weekly clock that survives a restart. <c>LastAttemptAt</c> is the last
/// try, successful or not, which spaces out retries after a failure.</summary>
public class AnimeIdMappingSyncState
{
    public int Id { get; set; }
    public DateTimeOffset? LastSyncedAt { get; set; }
    public DateTimeOffset? LastAttemptAt { get; set; }
}
