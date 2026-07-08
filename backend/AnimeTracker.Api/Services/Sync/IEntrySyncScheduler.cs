namespace AnimeTracker.Api.Services.Sync;

/// <summary>Schedules a debounced push of a changed entry back to MAL. Called
/// by the entry-edit service after any tracked field change (design.md's
/// "Write-sync: per-entry debounce with durable fallback" decision — the
/// pending_sync flag is the durability source of truth, this timer is just the
/// coalescing mechanism). The retry-on-failure queue, manual "sync now", and
/// full reconciliation are a separate, later concern layered on the same flag.</summary>
public interface IEntrySyncScheduler
{
    void ScheduleSync(int animeId);
}
