using AnimeTracker.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Sync;

public class StartupPendingSyncHold(AnimeTrackerDbContext db, ILogger<StartupPendingSyncHold> logger) : IStartupPendingSyncHold
{
    public async Task ApplyAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        var entriesToHold = await db.UserAnimeEntries
            .Where(e => e.PendingSync && e.HeldForReviewAt == null)
            .ToListAsync(ct);
        foreach (var entry in entriesToHold)
            entry.HeldForReviewAt = now;

        var removalsToHold = await db.PendingEntryDeletions
            .Where(d => d.HeldForReviewAt == null)
            .ToListAsync(ct);
        foreach (var removal in removalsToHold)
            removal.HeldForReviewAt = now;

        await db.SaveChangesAsync(ct);

        if (entriesToHold.Count > 0 || removalsToHold.Count > 0)
            logger.LogInformation(
                "Held {EntryCount} pending entr{EntrySuffix} and {RemovalCount} pending removal{RemovalSuffix} for review at startup.",
                entriesToHold.Count, entriesToHold.Count == 1 ? "y" : "ies", removalsToHold.Count, removalsToHold.Count == 1 ? "" : "s");
    }
}
