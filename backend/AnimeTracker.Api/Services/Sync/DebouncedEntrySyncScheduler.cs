using System.Collections.Concurrent;

namespace AnimeTracker.Api.Services.Sync;

/// <summary>Per-anime debounce timer (fixed at 8s per design.md's finalized
/// decision). Timers are independent per anime and reset on every edit to
/// that anime. On expiry, delegates to IEntryPushService (shared with the
/// retry job and manual "sync now") so there is one place that builds the
/// MAL update and clears pending_sync; a failed push just leaves pending_sync
/// set for the retry job to pick up later.</summary>
public class DebouncedEntrySyncScheduler(
    IServiceScopeFactory scopeFactory,
    ILogger<DebouncedEntrySyncScheduler> logger) : IEntrySyncScheduler, IDisposable
{
    private static readonly TimeSpan DebounceWindow = TimeSpan.FromSeconds(8);
    private readonly ConcurrentDictionary<int, Timer> _timers = new();

    public void ScheduleSync(int animeId)
    {
        _timers.AddOrUpdate(
            animeId,
            _ => new Timer(FireCallback, animeId, DebounceWindow, Timeout.InfiniteTimeSpan),
            (_, existing) =>
            {
                existing.Change(DebounceWindow, Timeout.InfiniteTimeSpan);
                return existing;
            });
    }

    private void FireCallback(object? state) => _ = PushAsync((int)state!);

    private async Task PushAsync(int animeId)
    {
        // Remove before pushing (not after): an edit arriving mid-push should
        // schedule a fresh timer rather than mutate one that already fired.
        if (_timers.TryRemove(animeId, out var timer))
            timer.Dispose();

        try
        {
            using var scope = scopeFactory.CreateScope();
            var pushService = scope.ServiceProvider.GetRequiredService<IEntryPushService>();
            await pushService.PushIfPendingAsync(animeId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Debounced push failed for anime {AnimeId}; it remains pending.", animeId);
        }
    }

    public void Dispose()
    {
        foreach (var timer in _timers.Values)
            timer.Dispose();
    }
}
