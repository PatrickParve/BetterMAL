using AnimeTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Data.Repositories;

public class EpisodeAiringRepository(AnimeTrackerDbContext db) : IEpisodeAiringRepository
{
    public Task<int?> GetMaxAiredEpisodeAsync(int animeId, DateTimeOffset asOfUtc, CancellationToken ct = default) =>
        db.EpisodeAirings.AsNoTracking()
            .Where(e => e.AnimeId == animeId && e.AirsAtUtc <= asOfUtc)
            .Select(e => (int?)e.Episode)
            .MaxAsync(ct);

    public async Task<Dictionary<int, int>> GetMaxAiredEpisodesAsync(IReadOnlyCollection<int> animeIds, DateTimeOffset asOfUtc, CancellationToken ct = default)
    {
        if (animeIds.Count == 0)
            return [];

        return await db.EpisodeAirings.AsNoTracking()
            .Where(e => animeIds.Contains(e.AnimeId) && e.AirsAtUtc <= asOfUtc)
            .GroupBy(e => e.AnimeId)
            .Select(g => new { AnimeId = g.Key, MaxEpisode = g.Max(e => e.Episode) })
            .ToDictionaryAsync(x => x.AnimeId, x => x.MaxEpisode, ct);
    }

    public Task<DateTimeOffset?> GetNextAiringInstantAsync(int animeId, DateTimeOffset afterUtc, CancellationToken ct = default) =>
        db.EpisodeAirings.AsNoTracking()
            .Where(e => e.AnimeId == animeId && e.AirsAtUtc > afterUtc)
            .Select(e => (DateTimeOffset?)e.AirsAtUtc)
            .MinAsync(ct);

    public Task<List<EpisodeAiring>> GetRowsInRangeAsync(IReadOnlyCollection<int> animeIds, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct = default) =>
        db.EpisodeAirings.AsNoTracking()
            .Where(e => animeIds.Contains(e.AnimeId) && e.AirsAtUtc >= fromUtc && e.AirsAtUtc < toUtc)
            .OrderBy(e => e.AirsAtUtc)
            .ToListAsync(ct);

    public async Task ReplaceForAnimeAsync(int animeId, IReadOnlyList<EpisodeAiring> rows, CancellationToken ct = default)
    {
        if (rows.Count == 0)
            return; // guard: a failed or empty fetch must never wipe existing rows (design decision 4)

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.EpisodeAirings.Where(e => e.AnimeId == animeId).ExecuteDeleteAsync(ct);
        db.EpisodeAirings.AddRange(rows);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
