namespace AnimeTracker.Api.Services.AnimeThemes;

/// <summary>What one <see cref="IAnimeThemesCatalogueSyncService.SyncIfDueAsync"/>
/// call did. A refresh abandoned by the sanity guard is <c>Failed</c> too: both
/// are recorded as a failed attempt and leave the stored catalogue
/// untouched.</summary>
public enum AnimeThemesCatalogueSyncOutcome
{
    NotDue,
    Synced,
    Failed,
}

/// <summary>Keeps the stored catalogue of openings and endings in step with
/// AnimeThemes, weekly (spec `anime-themes`).</summary>
public interface IAnimeThemesCatalogueSyncService
{
    /// <summary>Refreshes the catalogue when it is due: never refreshed, or last
    /// refreshed more than 7 days ago, and not within 6 hours of a failed attempt.
    /// Never throws except on cancellation. Any failure is logged, recorded as a
    /// failed attempt and returned as <see cref="AnimeThemesCatalogueSyncOutcome.Failed"/>,
    /// so nothing that calls it can be stopped by it (design.md D3).</summary>
    Task<AnimeThemesCatalogueSyncOutcome> SyncIfDueAsync(CancellationToken ct = default);
}
