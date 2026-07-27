namespace AnimeTracker.Api.Data.Repositories;

/// <summary>Persistence for the user's ordered "My top anime" preference
/// list — an anime id's position in the list, not membership in a set.</summary>
public interface ITopAnimeSelectionRepository
{
    /// <summary>All ordered anime ids, sorted by <c>(Position, AnimeId)</c> so a
    /// duplicate or backfilled tie is still deterministic.</summary>
    Task<List<int>> GetOrderedAnimeIdsAsync(CancellationToken ct = default);

    /// <summary>Replaces the entire ordering with the given anime ids, assigning
    /// <c>Position</c> sequentially from their order in the list. An empty
    /// collection clears the ordering, reverting every tier to the alphabetical
    /// default.</summary>
    Task ReplaceOrderAsync(IReadOnlyList<int> orderedAnimeIds, CancellationToken ct = default);
}
