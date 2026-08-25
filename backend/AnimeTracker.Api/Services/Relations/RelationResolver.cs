using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Series;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Relations;

public class RelationResolver(AnimeTrackerDbContext db) : IRelationResolver
{
    private static readonly Dictionary<RelationConfidence, int> ConfidenceRank = new()
    {
        [RelationConfidence.Confirmed] = 0,
        [RelationConfidence.Corroborated] = 1,
        [RelationConfidence.Unconfirmed] = 2,
        [RelationConfidence.Unknown] = 3,
        // Contradicted never reaches the ranked pick — it's filtered out
        // before ranking runs — but needs a rank for completeness.
        [RelationConfidence.Contradicted] = 4,
    };

    public async Task<RelationResolution> ResolveAsync(AnimeMetadata anime, CancellationToken ct = default)
    {
        var edges = await GetEdgesAsync(anime, ct);

        var prequel = await ResolveDirectionalAsync(anime, edges, "prequel", ct);
        var sequel = await ResolveDirectionalAsync(anime, edges, "sequel", ct);
        var parentStory = ResolveParentStory(edges);

        return new RelationResolution(prequel, sequel, parentStory);
    }

    // --- Union + confidence (4.2-4.4, 5.7-5.8) ---

    public async Task<IReadOnlyList<ResolvedRelationEdge>> GetEdgesAsync(AnimeMetadata anime, CancellationToken ct = default)
    {
        var outgoingFarEndIds = anime.RelatedAnime.Select(r => r.RelatedAnimeId).ToHashSet();

        var incoming = await db.AnimeRelatedAnime.AsNoTracking()
            .Where(r => r.RelatedAnimeId == anime.Id)
            .ToListAsync(ct);

        // Where an outgoing and an inverted incoming edge name the same
        // anime, the outgoing edge wins entirely (its type, its confidence
        // basis) — the incoming row is dropped, not merged.
        var derivedIncoming = incoming
            .Where(r => !outgoingFarEndIds.Contains(r.AnimeId))
            .GroupBy(r => r.AnimeId)
            .Select(g => g.First())
            .OrderBy(r => r.AnimeId)
            .ToList();

        var farEndIds = outgoingFarEndIds.Concat(derivedIncoming.Select(r => r.AnimeId)).Distinct().ToList();

        var farEndInfo = farEndIds.Count > 0
            ? await db.AnimeMetadata.AsNoTracking()
                .Where(a => farEndIds.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id, ct)
            : [];

        // AniList adjudication data, batched over every candidate far end —
        // one query, not one per edge (relation-confidence spec: "Batch the
        // far-end lookup").
        var aniListEdges = farEndIds.Count > 0
            ? await db.AniListRelations.AsNoTracking()
                .Where(r => (r.AnimeId == anime.Id && farEndIds.Contains(r.RelatedAnimeId))
                         || (farEndIds.Contains(r.AnimeId) && r.RelatedAnimeId == anime.Id))
                .ToListAsync(ct)
            : [];

        var syncIds = farEndIds.Append(anime.Id).ToList();
        var syncByAnimeId = await db.AnimeAiringSyncs.AsNoTracking()
            .Where(s => syncIds.Contains(s.AnimeId))
            .ToDictionaryAsync(s => s.AnimeId, ct);
        var viewedSync = syncByAnimeId.GetValueOrDefault(anime.Id);

        var edges = new List<ResolvedRelationEdge>();

        foreach (var edge in anime.RelatedAnime)
        {
            var invertedType = RelationInverse.Invert(edge.RelationType);
            var farEndFetched = farEndInfo.TryGetValue(edge.RelatedAnimeId, out var farAnime) && farAnime.LastSyncedAt != default;

            var confidence = invertedType is not null && incoming.Any(r => r.AnimeId == edge.RelatedAnimeId && r.RelationType == invertedType)
                ? RelationConfidence.Confirmed
                : Adjudicate(anime.Id, edge.RelatedAnimeId, edge.RelationType, farEndFetched,
                    aniListEdges, viewedSync, syncByAnimeId.GetValueOrDefault(edge.RelatedAnimeId));

            // Prefer a cached far-end row's own (possibly chosen) picture over
            // the relation row's denormalized snapshot (design D12); fall back
            // to the snapshot when the far end has no metadata row at all.
            edges.Add(new ResolvedRelationEdge(
                edge.RelatedAnimeId, edge.RelationType, edge.Title, farAnime?.PictureUrl ?? edge.PictureUrl, edge.MediaType,
                farAnime?.AiredFrom, IsReverseDerived: false, HasConfidentInverse: invertedType is not null, confidence));
        }

        var viewedFetched = anime.LastSyncedAt != default;
        foreach (var row in derivedIncoming)
        {
            // Confirmed is impossible here by construction: this entry only
            // survives dedup because the viewed anime stores no outgoing
            // edge to this far end at all.
            var invertedForV = RelationInverse.Invert(row.RelationType);
            var displayType = invertedForV ?? row.RelationType;
            var info = farEndInfo[row.AnimeId];

            var confidence = Adjudicate(anime.Id, row.AnimeId, displayType, viewedFetched,
                aniListEdges, viewedSync, syncByAnimeId.GetValueOrDefault(row.AnimeId));

            edges.Add(new ResolvedRelationEdge(
                row.AnimeId, displayType, info.Title, info.PictureUrl, info.MediaType, info.AiredFrom,
                IsReverseDerived: true, HasConfidentInverse: invertedForV is not null, confidence));
        }

        return edges;
    }

    /// <summary>Settles an edge that MAL's own two sides don't already agree
    /// on. <paramref name="typeFromV"/> is the relation's meaning from the
    /// viewed anime's side; <paramref name="otherSideMalFetched"/> is whether
    /// the side that does NOT store this edge (the far end for an outgoing
    /// edge, the viewed anime itself for a reverse-derived one) has been
    /// full-fetched.</summary>
    private static RelationConfidence Adjudicate(
        int viewedAnimeId, int farEndId, string typeFromV, bool otherSideMalFetched,
        List<AniListRelation> aniListEdges, AnimeAiringSync? viewedSync, AnimeAiringSync? farSync)
    {
        var farSideType = RelationInverse.Invert(typeFromV);

        // No confident inverse: never MAL-Confirmed, but still eligible for
        // AniList to settle (relation-confidence spec). Otherwise, adjudicate
        // only when the far end's silence is meaningful (Unconfirmed) — a far
        // end that's never been fetched carries no information, MAL or AniList.
        var eligibleForAdjudication = farSideType is null || otherSideMalFetched;
        if (!eligibleForAdjudication)
            return RelationConfidence.Unknown;

        var matchingEdges = aniListEdges
            .Where(e => (e.AnimeId == viewedAnimeId && e.RelatedAnimeId == farEndId) ||
                        (e.AnimeId == farEndId && e.RelatedAnimeId == viewedAnimeId))
            .ToList();

        var confirmedByAniList = matchingEdges.Any(e => e.AnimeId == viewedAnimeId
            ? AniListRelationTypeMapper.Matches(e.RelationType, typeFromV)
            : farSideType is not null && AniListRelationTypeMapper.Matches(e.RelationType, farSideType));
        if (confirmedByAniList)
            return RelationConfidence.Confirmed;

        if (matchingEdges.Count > 0)
            return RelationConfidence.Corroborated;

        var bothKnownToAniList =
            viewedSync is { AniListId: not null, RelationsFetchedAt: not null } &&
            farSync is { AniListId: not null, RelationsFetchedAt: not null };
        if (bothKnownToAniList)
            return RelationConfidence.Contradicted;

        return farSideType is null ? RelationConfidence.Unknown : RelationConfidence.Unconfirmed;
    }

    // --- Ranked pick (4.5, 4.6) ---

    private async Task<ResolvedRelationEdge?> ResolveDirectionalAsync(
        AnimeMetadata anime, IReadOnlyList<ResolvedRelationEdge> edges, string relationType, CancellationToken ct)
    {
        var candidates = edges.Where(e => e.RelationType == relationType).ToList();
        if (candidates.Count > 0)
        {
            var reduced = await ExcludeRecapAndSideContentAsync(anime, candidates, ct);
            var pool = (reduced.Count > 0 ? reduced : candidates)
                .Where(c => c.Confidence != RelationConfidence.Contradicted)
                .ToList();

            if (pool.Count > 0)
                return RankBest(anime, pool, relationType);
        }

        return await SeriesNeighbourFallbackAsync(anime, relationType, ct);
    }

    /// <summary>Rule 1: discard candidates that are a recap or side content of
    /// another candidate or of the anime being viewed, reusing the same rules
    /// <c>SeriesGraphBuilder</c> uses to exclude them from a series main
    /// line. Only each candidate's own outgoing relations are needed — an
    /// edge stored the other way round is covered because its owner is a
    /// candidate too.</summary>
    private async Task<List<ResolvedRelationEdge>> ExcludeRecapAndSideContentAsync(
        AnimeMetadata anime, IReadOnlyList<ResolvedRelationEdge> candidates, CancellationToken ct)
    {
        var candidateIds = candidates.Select(c => c.AnimeId).ToHashSet();
        candidateIds.Add(anime.Id);

        var otherIds = candidateIds.Where(id => id != anime.Id).ToList();
        var otherOutgoing = otherIds.Count > 0
            ? await db.AnimeRelatedAnime.AsNoTracking()
                .Where(r => otherIds.Contains(r.AnimeId))
                .Select(r => new { r.AnimeId, r.RelatedAnimeId, r.RelationType })
                .ToListAsync(ct)
            : [];

        var ownEdges = anime.RelatedAnime
            .Select(r => (OwnerId: anime.Id, r.RelatedAnimeId, r.RelationType))
            .Concat(otherOutgoing.Select(r => (OwnerId: r.AnimeId, r.RelatedAnimeId, r.RelationType)));

        var ineligible = SeriesRelations.FindRecapIds(ownEdges, candidateIds);
        ineligible.UnionWith(SeriesRelations.FindSideContentIds(ownEdges, candidateIds));

        return candidates.Where(c => !ineligible.Contains(c.AnimeId)).ToList();
    }

    private static ResolvedRelationEdge RankBest(AnimeMetadata anime, IReadOnlyList<ResolvedRelationEdge> pool, string relationType)
    {
        var isPrequel = relationType == "prequel";
        return pool
            .OrderBy(c => ConfidenceRank.GetValueOrDefault(c.Confidence, int.MaxValue))
            .ThenBy(c => c.IsReverseDerived) // false (outgoing) before true
            .ThenBy(c => MediaTypeRank(anime.MediaType, c.MediaType))
            .ThenBy(c => AirDateProximityKey(anime.AiredFrom, c.AiredFrom, isPrequel))
            .ThenBy(c => c.AnimeId)
            .First();
    }

    private static int MediaTypeRank(string? viewedMediaType, string? candidateMediaType) =>
        candidateMediaType == viewedMediaType ? 0
        : candidateMediaType == "tv" ? 1
        : 2;

    /// <summary>Rule 6: nearest preceding aired-from for a prequel, nearest
    /// following for a sequel. A candidate on the wrong side of the viewed
    /// anime's date (rare, MAL data being what it is) still sorts before one
    /// with no date at all — see rule 6's ordering.</summary>
    private static (int Tier, int GapDays) AirDateProximityKey(DateOnly? viewedAiredFrom, DateOnly? candidateAiredFrom, bool isPrequel)
    {
        if (candidateAiredFrom is not { } candidateDate || viewedAiredFrom is not { } viewedDate)
            return (2, 0);

        var gap = isPrequel
            ? viewedDate.DayNumber - candidateDate.DayNumber
            : candidateDate.DayNumber - viewedDate.DayNumber;

        return gap >= 0 ? (0, gap) : (1, -gap);
    }

    private static ResolvedRelationEdge? ResolveParentStory(IReadOnlyList<ResolvedRelationEdge> edges)
    {
        var candidates = edges
            .Where(e => e.RelationType == "parent_story" && e.Confidence != RelationConfidence.Contradicted)
            .ToList();
        if (candidates.Count == 0)
            return null;

        return candidates
            .OrderBy(c => ConfidenceRank.GetValueOrDefault(c.Confidence, int.MaxValue))
            .ThenBy(c => c.IsReverseDerived)
            .ThenBy(c => c.AnimeId)
            .First();
    }

    /// <summary>Rule at the end of the ranked-pick requirement: where a
    /// direction has no candidate edge at all, fall back to the anime's
    /// immediate main-line neighbour in its stored series. Never reached when
    /// a direct edge exists (<see cref="ResolveDirectionalAsync"/> only calls
    /// this once the candidate pool is empty).</summary>
    private async Task<ResolvedRelationEdge?> SeriesNeighbourFallbackAsync(AnimeMetadata anime, string relationType, CancellationToken ct)
    {
        var member = await db.SeriesMembers.AsNoTracking()
            .FirstOrDefaultAsync(m => m.AnimeId == anime.Id, ct);
        if (member is not { IsMainLine: true })
            return null;

        var neighbourOrder = relationType == "prequel" ? member.Order - 1 : member.Order + 1;
        var neighbour = await db.SeriesMembers.AsNoTracking()
            .Include(m => m.Anime)
            .FirstOrDefaultAsync(m => m.SeriesId == member.SeriesId && m.IsMainLine && m.Order == neighbourOrder, ct);
        if (neighbour is null)
            return null;

        return new ResolvedRelationEdge(
            neighbour.AnimeId, relationType, neighbour.Anime.Title, neighbour.Anime.PictureUrl, neighbour.Anime.MediaType,
            neighbour.Anime.AiredFrom, IsReverseDerived: true, HasConfidentInverse: true, RelationConfidence.Unknown);
    }
}
