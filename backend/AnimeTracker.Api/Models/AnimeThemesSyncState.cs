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

    /// <summary>What the stored catalogue was made with: the value of
    /// <c>AnimeThemesCatalogueSyncService.CurrentCatalogueFormat</c> when the last
    /// refresh succeeded. A catalogue whose format is behind the current one was
    /// stored before the app began keeping something it now relies on (the anime's
    /// name was the first), so it is refreshed at the next start instead of waiting
    /// out its week (improve-search-page design.md D9). 0 for a row stored before this
    /// field existed, which is what the migration gives the existing row.</summary>
    public int CatalogueFormat { get; set; }
}
