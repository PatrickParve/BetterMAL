using System.Linq.Expressions;
using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Series;

namespace AnimeTracker.Api.Services.Metadata;

/// <summary>The narrow carve-out that lets the tiered refresh reach an
/// unaired anime that has no list entry of its own, so an announcement can
/// mature into an episode count or a premiere date instead of freezing at
/// whatever its one resolving fetch saw (design.md D10; spec "Scheduled
/// refresh of unaired list-adjacent anime"). An anime qualifies while
/// MyAnimeList reports it as not yet aired, or reports no status for it at
/// all, and it is connected — in either direction — by a same-story or
/// version relation (<see cref="SeriesRelations.TraversalOrVersionRelations"/>)
/// to at least one non-Dropped list entry. Deliberately wider than a series'
/// own story-component traversal (<see cref="SeriesRelations.TraversalSet"/>)
/// so narrowing that set doesn't silently stop refreshing a listed anime's
/// alternative versions (rebuild-series-by-story-component design.md
/// Risks/Trade-offs). The set drains itself as members premiere: once
/// <c>AiringStatus</c> flips, an anime with no list entry of its own leaves
/// it immediately.</summary>
public static class AdjacentAnimeSet
{
    /// <summary>Expressed against <paramref name="db"/> directly rather than
    /// as a bare expression tree, since the predicate correlates against
    /// <c>AnimeRelatedAnime</c> and <c>UserAnimeEntries</c> — EF Core
    /// translates a DbSet captured this way exactly as it would one passed by
    /// parameter, so this composes into a larger query the same way
    /// <see cref="RefreshTiers.IsDue"/> does.</summary>
    public static Expression<Func<AnimeMetadata, bool>> IsAdjacent(AnimeTrackerDbContext db) =>
        a => (a.AiringStatus == "not_yet_aired" || a.AiringStatus == null) &&
             db.AnimeRelatedAnime.Any(r =>
                 SeriesRelations.TraversalOrVersionRelations.Contains(r.RelationType) &&
                 ((r.AnimeId == a.Id &&
                     db.UserAnimeEntries.Any(e => e.AnimeId == r.RelatedAnimeId && e.Status != WatchStatus.Dropped)) ||
                  (r.RelatedAnimeId == a.Id &&
                     db.UserAnimeEntries.Any(e => e.AnimeId == r.AnimeId && e.Status != WatchStatus.Dropped))));
}
