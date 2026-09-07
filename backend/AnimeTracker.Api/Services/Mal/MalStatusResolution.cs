using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Services.Mal;

/// <summary>design.md D1: the single rewatch-preserving rule every path that
/// reads a MAL list status back onto a local entry must apply. MAL has no
/// Rewatching status, so a rewatch is pushed as `watching`
/// (see <see cref="MalMappingExtensions.ToMalStatusString"/>) — without this,
/// reading that `watching` back would demote the rewatch to Watching.</summary>
public static class MalStatusResolution
{
    public static WatchStatus ResolveAgainstLocal(WatchStatus? local, WatchStatus remote) =>
        local == WatchStatus.Rewatching && remote == WatchStatus.Watching ? WatchStatus.Rewatching : remote;

    /// <summary>design.md D8a: whether a local entry's six pushed fields
    /// already match what MyAnimeList reports back for it — the same
    /// rewatch-preserving rule as <see cref="ResolveAgainstLocal"/> applied to
    /// status, so a local Rewatching pushed to MAL as `watching` reads back as
    /// agreeing rather than as a difference. Lifted out of
    /// ReconciliationService so the read-back rule is stated once for every
    /// caller that needs it.</summary>
    public static bool MatchesRemote(UserAnimeEntry local, UserAnimeEntry remote)
    {
        var statusMatches = local.Status == remote.Status
            || (local.Status == WatchStatus.Rewatching && remote.Status == WatchStatus.Watching);

        return statusMatches
            && local.EpisodesWatched == remote.EpisodesWatched
            && local.MyScore == remote.MyScore
            && local.RewatchCount == remote.RewatchCount
            && local.StartedAt == remote.StartedAt
            && local.CompletedAt == remote.CompletedAt;
    }
}
