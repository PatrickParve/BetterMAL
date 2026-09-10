namespace AnimeTracker.Api.Data.Repositories;

/// <summary>Persistence for the ranking's stored preference list — an anime
/// id's position in the list, not membership in a set. Read by
/// <c>Services/Ranking</c> to break ties within a score; the flat list is
/// otherwise opaque storage, not itself the ranking.</summary>
public interface ITopAnimeSelectionRepository
{
    /// <summary>All ordered anime ids, sorted by <c>(Position, AnimeId)</c> so a
    /// duplicate or backfilled tie is still deterministic.</summary>
    Task<List<int>> GetOrderedAnimeIdsAsync(CancellationToken ct = default);

    /// <summary>Slot-preserving merge (design.md D5): <paramref name="editedIds"/>
    /// is every edited tier's full new membership, concatenated in the order
    /// the caller wants them consumed. Walks the existing stored order;
    /// whenever a slot's id is one of <paramref name="editedIds"/>, that slot
    /// is refilled with the next id from <paramref name="editedIds"/> instead
    /// of its own; every other slot's id is left exactly where it is; any id
    /// from <paramref name="editedIds"/> that held no slot yet is appended at
    /// the end, in the order left once every existing slot is filled. Each
    /// tier's own relative order survives this regardless of how tiers
    /// interleave in the existing stored order, since slot assignment only
    /// ever advances forward through both the walk and the queue.</summary>
    Task ReplaceOrderAsync(IReadOnlyList<int> editedIds, CancellationToken ct = default);

    /// <summary>Replaces the stored order outright with exactly
    /// <paramref name="animeIds"/>, unlike <see cref="ReplaceOrderAsync"/>'s
    /// slot-preserving merge: an id missing from the list holds no stored
    /// position afterwards. Records <paramref name="modifiedAt"/> as the
    /// ranking's last-modified time rather than the time the call runs —
    /// adopting another device's ranking is not arranging one, and stamping
    /// "now" on adoption would let a stale ranking outrank a newer edit made
    /// on the other device meanwhile (design.md D3). Unknown ids are
    /// rejected before any row is touched, leaving the stored order and its
    /// time unchanged; a repeated id takes its first occurrence's
    /// position.</summary>
    Task ReplaceAllAsync(IReadOnlyList<int> animeIds, DateTimeOffset modifiedAt, CancellationToken ct = default);
}
