using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Relations;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Updates;

public class AnimeUpdateRelevance(AnimeTrackerDbContext db, IRelationResolver relationResolver) : IAnimeUpdateRelevance
{
    // A fixed tie-break order for when an anime qualifies through more than
    // one affiliate: sequel/prequel is the most specific news an affiliation
    // can carry. Eligibility itself is not restricted to
    // SeriesRelations.TraversalSet — any relation MAL reports counts here —
    // but the precedence list below still favors the story-relation types
    // first, since they're the most specific kind of news, with the looser
    // universe/cast/media links appended after them.
    private static readonly string[] RelationPrecedence =
    [
        "sequel", "prequel", "side_story", "parent_story", "summary", "full_story", "spin_off", "alternative_version",
        "adaptation", "alternative_setting", "other", "character",
    ];

    // design.md D3: the write-side test works from an anime id and queries
    // the database, not from the caller's entity. IRelationResolver.GetEdgesAsync
    // requires RelatedAnime to be loaded, but the lean listing writers
    // (SeasonBrowseService, TopAnimeService) and EpisodeScheduleRefreshService
    // neither Include it nor touch it — trusting a caller's entity would read
    // an unloaded collection as "no outgoing edges" and judge a genuinely
    // linked anime irrelevant. Loading fresh state from the id sidesteps that
    // entirely.
    public async Task<bool> IsRelevantAsync(int animeId, CancellationToken ct = default)
    {
        // Step (a): the anime's own non-Dropped list entry — one indexed
        // lookup, and the common relevant case.
        var isOwnEntry = await db.UserAnimeEntries.AsNoTracking()
            .AnyAsync(e => e.AnimeId == animeId && e.Status != WatchStatus.Dropped, ct);
        if (isOwnEntry)
            return true;

        // Step (b): a single both-directions existence query over
        // AnimeRelatedAnime joined to non-Dropped UserAnimeEntries, before the
        // resolver is ever touched. This is the common IRRELEVANT case — every
        // stranger a season browse writes — and it must not cost a resolver
        // call, since a resolver call here means a handful of extra queries
        // (far-end metadata, AniList edges, airing syncs) for every one of the
        // thousands of anime cached but never linked to the list.
        var hasCandidateEdge = await db.AnimeRelatedAnime.AsNoTracking()
            .AnyAsync(r =>
                (r.AnimeId == animeId && db.UserAnimeEntries.Any(e => e.AnimeId == r.RelatedAnimeId && e.Status != WatchStatus.Dropped)) ||
                (r.RelatedAnimeId == animeId && db.UserAnimeEntries.Any(e => e.AnimeId == r.AnimeId && e.Status != WatchStatus.Dropped)),
                ct);
        if (!hasCandidateEdge)
            return false;

        // Step (c): only now load the anime and run the full resolver, to
        // apply the Contradicted rule the cheap existence check above can't
        // see. AsNoTracking is deliberate: it reads committed state, matching
        // the read side exactly, and the only edges it cannot see are ones
        // written in the same unsaved unit of work — an anime's own fetch,
        // whose field updates the first-full-fetch rule already silences when
        // it is that anime's first fetch.
        var anime = await db.AnimeMetadata.AsNoTracking()
            .Include(a => a.RelatedAnime)
            .FirstOrDefaultAsync(a => a.Id == animeId, ct);
        if (anime is null)
            return false;

        return await FindAffiliateAsync(anime, ct) is not null;
    }

    public async Task<ResolvedRelationEdge?> FindAffiliateAsync(AnimeMetadata anime, CancellationToken ct = default)
    {
        // Every relation edge counts here, not just SeriesRelations.TraversalSet
        // (widened per user request) — an anime related to my list any way MAL
        // reports (alternative setting, shared character, a promo/other link)
        // is still franchise-adjacent news, not just a same-story continuation.
        // Confidence != Contradicted is the one guard kept: AniList actively
        // disputing an edge is evidence it isn't real, not a matter of how
        // narrowly "related" is defined.
        var edges = await relationResolver.GetEdgesAsync(anime, ct);
        var candidates = edges.Where(e => e.Confidence != RelationConfidence.Contradicted).ToList();
        if (candidates.Count == 0)
            return null;

        var farEndIds = candidates.Select(e => e.AnimeId).Distinct().ToList();
        var nonDroppedFarEndIds = await db.UserAnimeEntries.AsNoTracking()
            .Where(ue => farEndIds.Contains(ue.AnimeId) && ue.Status != WatchStatus.Dropped)
            .Select(ue => ue.AnimeId)
            .ToListAsync(ct);
        if (nonDroppedFarEndIds.Count == 0)
            return null;

        var nonDroppedFarEndSet = nonDroppedFarEndIds.ToHashSet();
        var qualifying = candidates.Where(e => nonDroppedFarEndSet.Contains(e.AnimeId)).ToList();
        if (qualifying.Count == 0)
            return null;

        // The named affiliate follows the title shown (design D4): where two
        // candidates share the most specific relation, the tie-break must
        // match AnimeUpdateService's own English-preferred choice, or the
        // affiliate picked here could differ from the one the reason names.
        return qualifying
            .OrderBy(e => RelationPrecedenceOf(e.RelationType))
            .ThenBy(e => e.EnglishTitle ?? e.Title, StringComparer.OrdinalIgnoreCase)
            .First();
    }

    private static int RelationPrecedenceOf(string relationType)
    {
        var index = Array.IndexOf(RelationPrecedence, relationType);
        return index >= 0 ? index : int.MaxValue;
    }
}
