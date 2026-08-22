using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Services.Entries;

/// <summary>design.md D1: whether any episode of an anime has aired, the
/// single answer every editing and display rule in this capability reads.
/// Counterpart of the frontend's hasAiredEpisodes in utils/anime.ts (task
/// 6.1) — kept honest by a comment on both sides rather than a shared
/// implementation.</summary>
public static class AiredEpisodeGate
{
    public static bool HasAired(AnimeMetadata anime, int? episodesAired) =>
        episodesAired is { } aired ? aired >= 1 : anime.AiringStatus != "not_yet_aired";
}
