using AnimeTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Data.Repositories;

public class UnknownAnimeIdsException(IReadOnlyCollection<int> animeIds)
    : Exception($"Unknown anime id(s): {string.Join(", ", animeIds)}");

public class TopAnimeSelectionRepository(AnimeTrackerDbContext db) : ITopAnimeSelectionRepository
{
    public Task<List<int>> GetOrderedAnimeIdsAsync(CancellationToken ct = default) =>
        db.TopAnimeSelections.AsNoTracking()
            .OrderBy(s => s.Position)
            .ThenBy(s => s.AnimeId)
            .Select(s => s.AnimeId)
            .ToListAsync(ct);

    public async Task ReplaceOrderAsync(IReadOnlyList<int> orderedAnimeIds, CancellationToken ct = default)
    {
        var distinctIds = orderedAnimeIds.Distinct().ToList();

        var knownIds = await db.AnimeMetadata
            .Where(a => distinctIds.Contains(a.Id))
            .Select(a => a.Id)
            .ToListAsync(ct);
        var unknownIds = distinctIds.Except(knownIds).ToList();
        if (unknownIds.Count > 0)
            throw new UnknownAnimeIdsException(unknownIds);

        var existing = await db.TopAnimeSelections.ToListAsync(ct);
        db.TopAnimeSelections.RemoveRange(existing);

        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < distinctIds.Count; i++)
            db.TopAnimeSelections.Add(new TopAnimeSelection { AnimeId = distinctIds[i], SelectedAt = now, Position = i });

        await db.SaveChangesAsync(ct);
    }
}
