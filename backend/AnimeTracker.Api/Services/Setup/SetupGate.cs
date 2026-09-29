using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Setup;

/// <summary>Whether first-run setup has finished, and the moment it does.
/// <see cref="IsFinished"/> answers the request gate and the routes that must
/// wait; <see cref="WhenFinished"/> is what every background job that setup
/// holds back awaits at the top of its loop, so each starts, in this process
/// and without a restart, the moment setup finishes (design D2).
/// <para>Loaded once at startup (<see cref="LoadAsync"/>), right after the
/// migration and before anything can read it. Finishing is written once, as the
/// only value setup stores about itself (<see cref="SetupState"/>), and never
/// undone: a finished gate stays finished for the life of the process and of
/// the database.</para></summary>
public class SetupGate(IServiceScopeFactory scopeFactory)
{
    /// <summary>The singleton row's key (<see cref="SetupState"/>).</summary>
    private const int StateId = 1;

    // RunContinuationsAsynchronously, so the jobs awaiting WhenFinished resume
    // on the thread pool rather than inline inside MarkFinishedAsync — the
    // coordinator's own loop must not run other services' first passes.
    private readonly TaskCompletionSource _finished = new(TaskCreationOptions.RunContinuationsAsynchronously);

    // Serializes MarkFinishedAsync, so two concurrent calls can't both insert
    // the row.
    private readonly SemaphoreSlim _markLock = new(1, 1);

    public bool IsFinished => _finished.Task.IsCompletedSuccessfully;

    /// <summary>Pending until setup finishes; already complete on an install
    /// that has finished. Never faults or cancels.</summary>
    public Task WhenFinished => _finished.Task;

    /// <summary>Reads the stored state once. A stored finish completes
    /// <see cref="WhenFinished"/> at once; no row, or a null instant, leaves it
    /// pending.</summary>
    public async Task LoadAsync(CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AnimeTrackerDbContext>();

        var completedAt = await db.SetupStates.AsNoTracking()
            .Where(s => s.Id == StateId)
            .Select(s => s.CompletedAt)
            .FirstOrDefaultAsync(ct);
        if (completedAt is not null)
            _finished.TrySetResult();
    }

    /// <summary>Records that setup has finished, in a scope of its own, then
    /// completes <see cref="WhenFinished"/>. The first call writes the instant;
    /// any later call changes nothing.</summary>
    public async Task MarkFinishedAsync(CancellationToken ct = default)
    {
        await _markLock.WaitAsync(ct);
        try
        {
            if (IsFinished)
                return;

            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AnimeTrackerDbContext>();

            var state = await db.SetupStates.FirstOrDefaultAsync(s => s.Id == StateId, ct);
            if (state is null)
                db.SetupStates.Add(new SetupState { Id = StateId, CompletedAt = DateTimeOffset.UtcNow });
            else
                state.CompletedAt ??= DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);

            // Only after the save: a job released by this must never see a gate
            // that a restart would then find unfinished.
            _finished.TrySetResult();
        }
        finally
        {
            _markLock.Release();
        }
    }
}
