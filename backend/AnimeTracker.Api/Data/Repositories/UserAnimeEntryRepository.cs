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

    // GetAllAsync stays the full read for every other caller.
    public Task<List<UserAnimeEntry>> GetAllForListViewAsync(CancellationToken ct = default) =>
        db.UserAnimeEntries.AsNoTracking()
            .Select(e => new UserAnimeEntry
            {
                AnimeId = e.AnimeId,
                Status = e.Status,
                EpisodesWatched = e.EpisodesWatched,
                MyScore = e.MyScore,
                StartedAt = e.StartedAt,
                CompletedAt = e.CompletedAt,
                RewatchCount = e.RewatchCount,
                PendingSync = e.PendingSync,
                LastSyncedAt = e.LastSyncedAt,
                HeldForReviewAt = e.HeldForReviewAt,
                Anime = new AnimeMetadata
                {
                    Id = e.Anime.Id,
                    Title = e.Anime.Title,
                    EnglishTitle = e.Anime.EnglishTitle,
                    PictureUrl = e.Anime.PictureUrl,
                    MediaType = e.Anime.MediaType,
                    TotalEpisodes = e.Anime.TotalEpisodes,
                    AiringStatus = e.Anime.AiringStatus,
                    MalScore = e.Anime.MalScore,
                    PopularityRank = e.Anime.PopularityRank,
                    AiredFrom = e.Anime.AiredFrom,
                    AverageEpisodeDurationSeconds = e.Anime.AverageEpisodeDurationSeconds,
                },
            })
            .ToListAsync(ct);

    public async Task<(int PendingCount, int HeldCount, DateTimeOffset? LastSyncedAt)> GetSyncStatusAsync(CancellationToken ct = default)
    {
        var pendingCount = await db.UserAnimeEntries.AsNoTracking().CountAsync(e => e.PendingSync && e.HeldForReviewAt == null, ct);
        var heldEntryCount = await db.UserAnimeEntries.AsNoTracking().CountAsync(e => e.PendingSync && e.HeldForReviewAt != null, ct);
        var heldRemovalCount = await db.PendingEntryDeletions.AsNoTracking().CountAsync(d => d.HeldForReviewAt != null, ct);
        var lastSyncedAt = await db.UserAnimeEntries.AsNoTracking().MaxAsync(e => (DateTimeOffset?)e.LastSyncedAt, ct);
        return (pendingCount, heldEntryCount + heldRemovalCount, lastSyncedAt);
    }
}
