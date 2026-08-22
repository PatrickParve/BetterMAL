using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Services.Entries;

/// <summary>design.md D6: returns a Completed entry to Watching the moment a
/// new episode of its still-airing anime has aired past what it watched — an
/// episode becomes aired purely by its stored air instant passing, so this is
/// evaluated wherever an entry is read, not waited on a scheduled pass.</summary>
public interface ICompletedEntryReopenService
{
    /// <summary>Re-opens every entry among <paramref name="entries"/> that is
    /// Completed, whose anime is currently airing, and whose resolved
    /// aired-so-far count (looked up in <paramref name="airedSoFarByAnimeId"/>
    /// by AnimeId) exceeds its episodes watched. A no-op for every other
    /// entry, so passing the full read-path list is always safe.
    ///
    /// Mutates each reopened entry's Status in place — even though the
    /// entries a read path passes in are typically untracked — so a caller
    /// that already resolved its own DTOs from these same object references
    /// sees the flip without a second read.</summary>
    Task ReopenAsync(
        IReadOnlyCollection<UserAnimeEntry> entries,
        IReadOnlyDictionary<int, int> airedSoFarByAnimeId,
        CancellationToken ct = default);
}
