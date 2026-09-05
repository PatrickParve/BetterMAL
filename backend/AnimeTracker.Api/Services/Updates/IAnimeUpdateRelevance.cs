using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Relations;

namespace AnimeTracker.Api.Services.Updates;

/// <summary>The single implementation of the anime-updates eligibility rule —
/// an anime is eligible when it is either a non-Dropped list entry of the
/// user's own, or connected by one relation step, either direction, to such
/// an entry by an edge nothing has contradicted. Both the recording gate
/// ("Nothing is recorded for an anime outside my list and its direct
/// relations") and the display filter ("Updates are shown for my own entries
/// and for their franchises") read this same rule; two separately maintained
/// copies of it would drift silently, so this is the only place it is
/// expressed.</summary>
public interface IAnimeUpdateRelevance
{
    /// <summary>Write side. Whether <paramref name="animeId"/> currently
    /// qualifies, loading whatever state it needs from the id alone.</summary>
    Task<bool> IsRelevantAsync(int animeId, CancellationToken ct = default);

    /// <summary>Read side. The qualifying affiliation edge for
    /// <paramref name="anime"/>, or null when it has none — the reason text
    /// <see cref="AnimeUpdateService"/> renders is built on top of this.</summary>
    Task<ResolvedRelationEdge?> FindAffiliateAsync(AnimeMetadata anime, CancellationToken ct = default);
}
