using AnimeTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Data.Repositories;

public class UserAnimeEntryRepository(AnimeTrackerDbContext db) : IUserAnimeEntryRepository
{
    public Task<UserAnimeEntry?> GetByAnimeIdAsync(int animeId, CancellationToken ct = default) =>
        db.UserAnimeEntries.AsNoTracking()
            .Include(e => e.Anime)
            .FirstOrDefaultAsync(e => e.AnimeId == animeId, ct);

    public Task<List<UserAnimeEntry>> GetAllAsync(CancellationToken ct = default) =>
        db.UserAnimeEntries.AsNoTracking()
            .Include(e => e.Anime)
            .ToListAsync(ct);

    public async Task<(int PendingCount, DateTimeOffset? LastSyncedAt)> GetSyncStatusAsync(CancellationToken ct = default)
    {
        var pendingCount = await db.UserAnimeEntries.AsNoTracking().CountAsync(e => e.PendingSync, ct);
        var lastSyncedAt = await db.UserAnimeEntries.AsNoTracking().MaxAsync(e => (DateTimeOffset?)e.LastSyncedAt, ct);
        return (pendingCount, lastSyncedAt);
    }
}
