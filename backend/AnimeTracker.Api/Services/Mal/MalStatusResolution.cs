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
}
