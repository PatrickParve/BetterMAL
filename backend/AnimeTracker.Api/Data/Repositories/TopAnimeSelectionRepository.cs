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

    public async Task ReplaceOrderAsync(IReadOnlyList<int> editedIds, CancellationToken ct = default)
    {
        var distinctEditedIds = editedIds.Distinct().ToList();

        var knownIds = await db.AnimeMetadata
            .Where(a => distinctEditedIds.Contains(a.Id))
            .Select(a => a.Id)
            .ToListAsync(ct);
        var unknownIds = distinctEditedIds.Except(knownIds).ToList();
        if (unknownIds.Count > 0)
            throw new UnknownAnimeIdsException(unknownIds);

        var existing = await db.TopAnimeSelections.ToListAsync(ct);
        var existingOrder = existing.OrderBy(s => s.Position).ThenBy(s => s.AnimeId).Select(s => s.AnimeId).ToList();

        // design.md D5: one pass over the existing order. Each slot whose id
        // is edited is refilled from the queue (in the caller's chosen
        // order); every other slot is left untouched. Slot assignment only
        // ever advances, so each edited tier's own relative order survives
        // regardless of how tiers interleave in the existing order.
        var editedIdSet = distinctEditedIds.ToHashSet();
        var editedQueue = new Queue<int>(distinctEditedIds);

        var finalOrder = new List<int>(existingOrder.Count + distinctEditedIds.Count);
        foreach (var animeId in existingOrder)
            finalOrder.Add(editedIdSet.Contains(animeId) ? editedQueue.Dequeue() : animeId);

        // Anything left in the queue never had a slot — a newly scored or
        // never-placed anime — and is appended, in the order left once every
        // existing slot is filled (design.md D6).
        finalOrder.AddRange(editedQueue);

        db.TopAnimeSelections.RemoveRange(existing);

        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < finalOrder.Count; i++)
            db.TopAnimeSelections.Add(new TopAnimeSelection { AnimeId = finalOrder[i], SelectedAt = now, Position = i });

        await db.SaveChangesAsync(ct);
    }
}
