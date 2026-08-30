using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Services.Series;

/// <summary>Classifies a story component's version neighbours — anime linked
/// to a member by <see cref="SeriesRelations.VersionRelations"/> but outside
/// the component itself (rebuild-series-by-story-component design.md decision
/// D2, task 3.2).
///
/// A neighbour with no story relations of its own has a story component of
/// exactly itself, so it can never be a series: it folds in as an ordinary
/// extra (<see cref="MembershipKind.FoldedVersion"/>). A neighbour that does
/// carry story relations of its own heads a component of its own, so its tile
/// opens that series instead (<see cref="MembershipKind.NeighbourTelling"/>).
/// A neighbour with no cached metadata row at all can't be a
/// <see cref="Models.SeriesMember"/> — the row is required — so it's excluded
/// here entirely; the caller falls back to the related-entry projection for
/// it instead.
///
/// The decision needs only cached rows already in hand — <paramref name="componentMembers"/>'s
/// own <c>RelatedAnime</c> to find the candidates, and <paramref name="cachedNeighbours"/>'s
/// own <c>RelatedAnime</c> to classify each one — so it costs no MAL
/// fetch.
///
/// A candidate is found from either side of the edge — a component member's
/// own declared version relation, or a candidate's own declared version
/// relation back at a member — because <c>AnimeRelatedAnime</c> can be
/// one-sided the same way story relations already are
/// (rebuild-series-by-story-component design.md D3: "in both directions";
/// task 4.2).</summary>
internal static class SeriesVersionNeighbours
{
    internal sealed class Neighbour
    {
        public required int AnimeId { get; init; }
        public required MembershipKind MembershipKind { get; init; }
        public required AnimeMetadata Anime { get; init; }
    }

    /// <summary><paramref name="cachedNeighbours"/> need only contain the
    /// version-relation far ends that actually have a cached row — any
    /// candidate absent from it is silently excluded from the result, exactly
    /// as a neighbour with no cached metadata row should be.</summary>
    internal static List<Neighbour> Classify(List<AnimeMetadata> componentMembers, List<AnimeMetadata> cachedNeighbours)
    {
        var componentMemberIds = componentMembers.Select(m => m.Id).ToHashSet();

        var outgoingCandidateIds = componentMembers
            .SelectMany(m => m.RelatedAnime)
            .Where(r => SeriesRelations.VersionRelations.Contains(r.RelationType))
            .Select(r => r.RelatedAnimeId);

        var incomingCandidateIds = cachedNeighbours
            .Where(n => n.RelatedAnime.Any(r =>
                SeriesRelations.VersionRelations.Contains(r.RelationType) && componentMemberIds.Contains(r.RelatedAnimeId)))
            .Select(n => n.Id);

        var candidateIds = outgoingCandidateIds.Concat(incomingCandidateIds)
            .Where(id => !componentMemberIds.Contains(id))
            .ToHashSet();

        var cachedNeighbourById = cachedNeighbours.ToDictionary(a => a.Id);

        var neighbours = new List<Neighbour>();
        foreach (var candidateId in candidateIds)
        {
            if (!cachedNeighbourById.TryGetValue(candidateId, out var neighbourMetadata))
                continue;

            var hasOwnStoryRelation = neighbourMetadata.RelatedAnime.Any(r => SeriesRelations.IsTraversable(r.RelationType));
            neighbours.Add(new Neighbour
            {
                AnimeId = candidateId,
                Anime = neighbourMetadata,
                MembershipKind = hasOwnStoryRelation ? MembershipKind.NeighbourTelling : MembershipKind.FoldedVersion,
            });
        }

        return neighbours;
    }
}
