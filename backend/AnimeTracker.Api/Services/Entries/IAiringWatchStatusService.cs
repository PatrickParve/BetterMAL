using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Services.Entries;

/// <summary>design.md D5/D6: keeps an entry's Watching/Completed status in
/// step with its anime's episode counts on every read, rather than waiting on
/// a scheduled pass. Two independent directions, both keyed on counts rather
/// than MyAnimeList's own (routinely stale) airing status:
///
/// - Re-open: a Completed entry on a currently-airing anime that hasn't
///   watched the published total returns to Watching — an episode became
///   aired since the entry completed.
/// - Complete: a Watching entry that has already watched what turns out to be
///   the full run completes — the case where the total arrives after the
///   viewing (the AniList fallback in episode-airing-data makes this
///   common).</summary>
public interface IAiringWatchStatusService
{
    /// <summary>Applies both directions to every entry among
    /// <paramref name="entries"/> whose condition matches; a no-op for every
    /// other entry, so passing the full read-path list is always safe.
    /// However many entries qualify, this costs one tracked read and one
    /// save, and nothing when none qualify. A conflicting concurrent change
    /// to one entry drops only that entry; the rest still save.
    ///
    /// Mutates each settled entry's Status in place — even though the
    /// entries a read path passes in are typically untracked — so a caller
    /// that already resolved its own DTOs from these same object references
    /// sees the flip without a second read. A completion's CompletedAt is
    /// not written back onto the caller's object; only the stored entry
    /// gets it (design.md Open Questions).</summary>
    Task SettleAsync(
        IReadOnlyCollection<UserAnimeEntry> entries,
        IReadOnlyDictionary<int, int> airedSoFarByAnimeId,
        CancellationToken ct = default);
}
