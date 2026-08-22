using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Services.Entries;

/// <summary>design.md D0: Rewatching is reachable only for an anime that has
/// finished airing, and only for an entry with durable evidence of having
/// been finished at least once. Tests the entry's history rather than its
/// present status, so a rewatch parked under Watching/OnHold/Dropped can be
/// resumed directly, without a detour through Completed.</summary>
public static class RewatchingEligibility
{
    public static bool IsEligible(AnimeMetadata anime, UserAnimeEntry entry) =>
        AnimeHasFinishedAiring(anime) && HasFinishedOnce(entry);

    private static bool AnimeHasFinishedAiring(AnimeMetadata anime) =>
        anime.AiringStatus is null or "finished_airing";

    private static bool HasFinishedOnce(UserAnimeEntry entry) =>
        entry.CompletedAt is not null || entry.RewatchCount > 0 || entry.Status == WatchStatus.Completed;

    /// <summary>Only meaningful when <see cref="IsEligible"/> is false —
    /// names which of the two D0 conditions failed, for the rejection message.</summary>
    public static string IneligibilityReason(AnimeMetadata anime, UserAnimeEntry entry) =>
        !AnimeHasFinishedAiring(anime)
            ? "the anime has not finished airing"
            : "the anime has never been finished";
}
