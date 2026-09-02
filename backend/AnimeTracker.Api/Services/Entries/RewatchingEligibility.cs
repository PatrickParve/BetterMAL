using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Services.Entries;

/// <summary>design.md D0: Rewatching is reachable only for an anime that has
/// finished airing, and only for an entry with durable evidence of having
/// been finished at least once. Tests the entry's history rather than its
/// present status, so a rewatch parked under Watching/OnHold/Dropped can be
/// resumed directly, without a detour through Completed.</summary>
public static class RewatchingEligibility
{
    public static bool IsEligible(AnimeMetadata anime, UserAnimeEntry entry, int? episodesAired) =>
        AnimeHasFinishedAiring(anime, episodesAired) && HasFinishedOnce(entry);

    // design.md D3: the shared "every episode has aired" predicate, so a
    // stale `currently_airing` status no longer blocks a rewatch of a show
    // that is actually over.
    private static bool AnimeHasFinishedAiring(AnimeMetadata anime, int? episodesAired) =>
        AiredEpisodeGate.EverythingHasAired(anime, episodesAired);

    private static bool HasFinishedOnce(UserAnimeEntry entry) =>
        entry.CompletedAt is not null || entry.RewatchCount > 0 || entry.Status == WatchStatus.Completed;

    /// <summary>Only meaningful when <see cref="IsEligible"/> is false —
    /// names which of the two D0 conditions failed, for the rejection message.</summary>
    public static string IneligibilityReason(AnimeMetadata anime, UserAnimeEntry entry, int? episodesAired) =>
        !AnimeHasFinishedAiring(anime, episodesAired)
            ? "the anime has not aired in full"
            : "the anime has never been finished";
}
