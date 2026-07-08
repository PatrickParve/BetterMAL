using AnimeTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Data.Repositories;

public class UnknownAnimeIdsException(IReadOnlyCollection<int> animeIds)
    : Exception($"Unknown anime id(s): {string.Join(", ", animeIds)}");

public class TopAnimeSelectionRepository(AnimeTrackerDbContext db) : ITopAnimeSelectionRepository
{
    public Task<List<int>> GetSelectedAnimeIdsAsync(CancellationToken ct = default) =>
        db.TopAnimeSelections.AsNoTracking().Select(s => s.AnimeId).ToListAsync(ct);

    public async Task ReplaceSelectionAsync(IReadOnlyCollection<int> animeIds, CancellationToken ct = default)
    {
        var distinctIds = animeIds.Distinct().ToList();

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
        foreach (var animeId in distinctIds)
            db.TopAnimeSelections.Add(new TopAnimeSelection { AnimeId = animeId, SelectedAt = now });

        await db.SaveChangesAsync(ct);
    }
}
