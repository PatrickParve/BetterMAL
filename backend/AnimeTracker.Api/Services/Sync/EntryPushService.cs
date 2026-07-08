using AnimeTracker.Api.Data;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Mal.Dto;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Sync;

public class EntryPushService(
    AnimeTrackerDbContext db,
    IMalClient malClient,
    ILogger<EntryPushService> logger) : IEntryPushService
{
    public async Task<bool> PushIfPendingAsync(int animeId, CancellationToken ct = default)
    {
        var entry = await db.UserAnimeEntries.FirstOrDefaultAsync(e => e.AnimeId == animeId, ct);
        if (entry is null || !entry.PendingSync)
            return false;

        try
        {
            var update = new MalListStatusUpdate
            {
                Status = entry.Status.ToMalStatusString(),
                NumWatchedEpisodes = entry.EpisodesWatched,
                Score = entry.MyScore ?? 0,
                NumTimesRewatched = entry.RewatchCount,
                StartDate = entry.StartedAt,
                FinishDate = entry.CompletedAt,
            };

            await malClient.UpdateMyListStatusAsync(animeId, update, ct);

            entry.PendingSync = false;
            entry.LastSyncedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to push pending sync for anime {AnimeId}; it remains pending.", animeId);
            return false;
        }
    }

    public async Task<int> DrainPendingAsync(CancellationToken ct = default)
    {
        var pendingIds = await db.UserAnimeEntries.AsNoTracking()
            .Where(e => e.PendingSync)
            .Select(e => e.AnimeId)
            .ToListAsync(ct);

        var pushed = 0;
        foreach (var animeId in pendingIds)
        {
            if (await PushIfPendingAsync(animeId, ct))
                pushed++;
        }

        return pushed;
    }
}
