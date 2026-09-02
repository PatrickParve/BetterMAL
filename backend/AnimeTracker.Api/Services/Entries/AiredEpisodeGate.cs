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

    /// <summary>design.md D3: whether every episode of an anime has aired — the
    /// fact "Completed" and rewatching eligibility are decided on. A union, not
    /// a replacement of the status-only test it supersedes: the status arm
    /// covers a finished show with incomplete or missing AniList airing rows,
    /// which the aired-count arm alone would wrongly call "not yet finished";
    /// the aired-count arm covers a show MAL still lists as `currently_airing`
    /// weeks after it genuinely ended.</summary>
    public static bool EverythingHasAired(AnimeMetadata anime, int? episodesAired) =>
        anime.AiringStatus != "currently_airing"
        || (anime.TotalEpisodes is { } total && episodesAired is { } aired && aired >= total);
}
