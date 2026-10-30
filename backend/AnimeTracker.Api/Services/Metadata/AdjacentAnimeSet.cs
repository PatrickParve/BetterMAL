using System.Linq.Expressions;
using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Series;

namespace AnimeTracker.Api.Services.Metadata;

/// <summary>Who the tiered refresh reaches: my list, plus two carve-outs for
/// anime that have no list entry of their own.
///
/// <para><b>Adjacent</b> (design.md D10; spec "Scheduled refresh of unaired
/// list-adjacent anime"): lets an announcement mature into an episode count or
/// a premiere date instead of freezing at whatever its one resolving fetch
/// saw. An anime qualifies while MyAnimeList reports it as not yet aired, or
/// reports no status for it at all, and it is connected — in either direction —
/// by a same-story or version relation
/// (<see cref="SeriesRelations.TraversalOrVersionRelations"/>) to at least one
/// non-Dropped list entry. Deliberately wider than a series' own
/// story-component traversal (<see cref="SeriesRelations.TraversalSet"/>) so
/// narrowing that set doesn't silently stop refreshing a listed anime's
/// alternative versions (rebuild-series-by-story-component design.md
/// Risks/Trade-offs). The set drains itself as members premiere: once
/// <c>AiringStatus</c> flips, an anime with no list entry of its own leaves
/// it immediately.</para>
///
/// <para><b>My series' main lines</b> (add-score-history-graph design.md D8;
/// spec "Scheduled refresh of my series' main lines"): a main-line member of a
/// series where any member — main line or extra — is a non-Dropped list entry.
/// Without it an entry outside my list stops being refreshed the day it
/// premieres, when it leaves the adjacent set, so a new season's score could
/// never reach its series line. Extras don't qualify by sitting in such a
/// series, and neither does a series with no entry of mine (one that only
/// exists because it was visited) or whose entries are all Dropped.</para></summary>
public static class AdjacentAnimeSet
{
    /// <summary>All three arms in one predicate (a list entry, adjacent, a
    /// main line of mine), so the caller composes nothing by hand. Expressed
    /// against <paramref name="db"/> directly rather than as a bare expression
    /// tree, since the arms correlate against <c>AnimeRelatedAnime</c>,
    /// <c>SeriesMembers</c> and <c>UserAnimeEntries</c> — EF Core translates a
    /// DbSet captured this way exactly as it would one passed by parameter, so
    /// this composes into a larger query the same way
    /// <see cref="RefreshTiers.IsDue"/> does.</summary>
    public static Expression<Func<AnimeMetadata, bool>> IsRefreshCandidate(AnimeTrackerDbContext db) =>
        a => a.UserEntry != null ||
             ((a.AiringStatus == "not_yet_aired" || a.AiringStatus == null) &&
              db.AnimeRelatedAnime.Any(r =>
                  SeriesRelations.TraversalOrVersionRelations.Contains(r.RelationType) &&
                  ((r.AnimeId == a.Id &&
                      db.UserAnimeEntries.Any(e => e.AnimeId == r.RelatedAnimeId && e.Status != WatchStatus.Dropped)) ||
                   (r.RelatedAnimeId == a.Id &&
                      db.UserAnimeEntries.Any(e => e.AnimeId == r.AnimeId && e.Status != WatchStatus.Dropped))))) ||
             db.SeriesMembers.Any(m =>
                 m.AnimeId == a.Id && m.IsMainLine &&
                 db.SeriesMembers.Any(s =>
                     s.SeriesId == m.SeriesId &&
                     db.UserAnimeEntries.Any(e => e.AnimeId == s.AnimeId && e.Status != WatchStatus.Dropped)));
}
