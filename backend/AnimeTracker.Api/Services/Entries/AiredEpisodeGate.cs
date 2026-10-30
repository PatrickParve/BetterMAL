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

    /// <summary>add-score-history-graph design.md D3: whether an episode is
    /// <em>known</em> to have aired, the proof score recording needs. Stricter
    /// than <see cref="HasAired"/> in exactly one way: an unknown status is not
    /// aired. <see cref="HasAired"/> asks "can we prove nothing has aired?" and
    /// so lets an unknown status through, because the editing rules can't prove
    /// otherwise; recording asks "can we prove something has?", and using the
    /// lenient test would start histories for every listing-only row that
    /// happens to carry a score, anime that haven't aired included. A stored
    /// aired row still beats a late `not_yet_aired`.</summary>
    public static bool HasConfirmedAiring(AnimeMetadata anime, int? episodesAired) =>
        HasConfirmedAiring(anime.AiringStatus, episodesAired);

    /// <summary>The same test for a caller that has read only the status, so it
    /// need not load an entity to ask.</summary>
    public static bool HasConfirmedAiring(string? airingStatus, int? episodesAired) =>
        episodesAired is { } aired ? aired >= 1 : airingStatus is "currently_airing" or "finished_airing";

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
