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
        await EnsureKnownIdsAsync(distinctEditedIds, ct);

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

        await WriteOrderAsync(existing, finalOrder, DateTimeOffset.UtcNow, ct);
    }

    public async Task ReplaceAllAsync(IReadOnlyList<int> animeIds, DateTimeOffset modifiedAt, CancellationToken ct = default)
    {
        // Distinct() keeps each id's first occurrence, the same rule
        // ReplaceOrderAsync's merge applies to its own edited ids.
        var distinctIds = animeIds.Distinct().ToList();
        await EnsureKnownIdsAsync(distinctIds, ct);

        var existing = await db.TopAnimeSelections.ToListAsync(ct);
        await WriteOrderAsync(existing, distinctIds, modifiedAt, ct);
    }

    public Task<DateTimeOffset?> GetModifiedAtAsync(CancellationToken ct = default) =>
        db.RankingStates.AsNoTracking()
            .Select(s => s.ModifiedAt)
            .FirstOrDefaultAsync(ct);

    private async Task EnsureKnownIdsAsync(IReadOnlyList<int> distinctIds, CancellationToken ct)
    {
        var knownIds = await db.AnimeMetadata
            .Where(a => distinctIds.Contains(a.Id))
            .Select(a => a.Id)
            .ToListAsync(ct);
        var unknownIds = distinctIds.Except(knownIds).ToList();
        if (unknownIds.Count > 0)
            throw new UnknownAnimeIdsException(unknownIds);
    }

    /// <summary>The one place every write of the stored order passes through,
    /// so "every write sets the ranking's last-modified time" is structural
    /// rather than a convention each caller must remember (design.md D2).
    /// Replaces <paramref name="existingRows"/> outright with
    /// <paramref name="finalOrder"/> at positions <c>0..n-1</c>, and records
    /// <paramref name="modifiedAt"/> on the singleton <see cref="RankingState"/>
    /// row, all in one save.</summary>
    private async Task WriteOrderAsync(
        IReadOnlyList<TopAnimeSelection> existingRows,
        IReadOnlyList<int> finalOrder,
        DateTimeOffset modifiedAt,
        CancellationToken ct)
    {
        db.TopAnimeSelections.RemoveRange(existingRows);

        for (var i = 0; i < finalOrder.Count; i++)
            db.TopAnimeSelections.Add(new TopAnimeSelection { AnimeId = finalOrder[i], Position = i });

        var state = await db.RankingStates.FirstOrDefaultAsync(ct);
        if (state is null)
        {
            state = new RankingState();
            db.RankingStates.Add(state);
        }
        state.ModifiedAt = modifiedAt;

        await db.SaveChangesAsync(ct);
    }
}
