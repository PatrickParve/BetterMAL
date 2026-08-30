using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Services.Series;

/// <summary>Resolves a story component's version slots and branches
/// (rebuild-series-by-story-component design.md decision D4, tasks 5.2/5.4):
/// where two or more <em>main-line</em> members are alternative versions of
/// one another, they share one numbered watch-order position (a version
/// slot) with a picker; a main-line member reachable only through one
/// alternative belongs to that alternative's branch and is shown only while
/// it's picked; everything else is trunk and always shown.
///
/// Operates purely over the main line already classified by
/// <c>SeriesGraphBuilder.ClassifyMainLineChain</c> — a version neighbour is
/// never a main-line member (design.md D2), so it's never a candidate for a
/// slot or a branch here at all; this never reasons about the graph beyond
/// the main line it's handed.</summary>
internal static class SeriesVersionSlots
{
    /// <summary>One version slot: <see cref="AlternativeIds"/> is every
    /// main-line member the slot holds — a connected group, of at least two,
    /// over version relations among main-line members — ordered by
    /// <see cref="SeriesGraphBuilder.OrderKey"/>. <see cref="SlotKey"/> is the
    /// lowest MAL id among them, the value <c>SeriesMember.VersionSlotKey</c>
    /// is keyed on. <see cref="BranchMemberIdsByAlternativeId"/> maps each
    /// alternative to its branch's member ids, the alternative itself
    /// included.</summary>
    internal sealed class VersionSlot
    {
        public required int SlotKey { get; init; }
        public required List<int> AlternativeIds { get; init; }
        public required Dictionary<int, HashSet<int>> BranchMemberIdsByAlternativeId { get; init; }
    }

    /// <summary>Finds every version slot in <paramref name="mainLineMembers"/>
    /// and, for each of its alternatives, the branch reachable only through
    /// it (design.md D4). A slot is a connected component, of size >= 2, of
    /// the undirected <see cref="SeriesRelations.VersionRelations"/> edges
    /// among main-line members — reusing
    /// <see cref="SeriesGraphBuilder.FindConnectedComponents"/>, the same
    /// generic BFS <c>ClassifyMainLineChain</c> already uses for the
    /// sequel/prequel chain itself. Returns an empty list when no two
    /// main-line members are alternatives of each other (an ordinary series,
    /// or one whose only alternative version folded in as an extra — Clannad
    /// Movie never reaches here at all, since it's never main line).</summary>
    internal static List<VersionSlot> Resolve(
        List<AnimeMetadata> mainLineMembers, HashSet<int> mainLineIds, Dictionary<int, AnimeMetadata> memberById)
    {
        var versionAdjacency = mainLineIds.ToDictionary(id => id, _ => new HashSet<int>());
        foreach (var member in mainLineMembers)
        {
            foreach (var relation in member.RelatedAnime)
            {
                if (!SeriesRelations.VersionRelations.Contains(relation.RelationType))
                    continue;
                if (!mainLineIds.Contains(relation.RelatedAnimeId))
                    continue;

                versionAdjacency[member.Id].Add(relation.RelatedAnimeId);
                versionAdjacency[relation.RelatedAnimeId].Add(member.Id);
            }
        }

        var slotComponents = SeriesGraphBuilder.FindConnectedComponents(mainLineIds, versionAdjacency)
            .Where(component => component.Count >= 2)
            .ToList();

        if (slotComponents.Count == 0)
            return [];

        var alternativeIdsBySlotKey = slotComponents.ToDictionary(
            component => component.Min(),
            component => component.OrderBy(id => SeriesGraphBuilder.OrderKey(memberById[id])).ToList());
        var allAlternativeIds = alternativeIdsBySlotKey.Values.SelectMany(ids => ids).ToHashSet();

        // Story adjacency among main-line members, for branch distance
        // (design.md D4: "over sequel/prequel relations").
        var storyAdjacency = mainLineIds.ToDictionary(id => id, _ => new HashSet<int>());
        foreach (var member in mainLineMembers)
        {
            foreach (var relation in member.RelatedAnime)
            {
                if (relation.RelationType is not ("sequel" or "prequel"))
                    continue;
                if (!mainLineIds.Contains(relation.RelatedAnimeId))
                    continue;

                storyAdjacency[member.Id].Add(relation.RelatedAnimeId);
                storyAdjacency[relation.RelatedAnimeId].Add(member.Id);
            }
        }

        // Distances from each alternative, over the graph with every *other*
        // alternative of every slot removed (design.md D4) — one BFS per
        // alternative, not per member, since what a branch needs is each
        // member's distance to every alternative, not the other way round.
        var distancesByAlternativeId = allAlternativeIds.ToDictionary(
            alternativeId => alternativeId,
            alternativeId => BfsDistances(
                alternativeId, storyAdjacency, excludedIds: allAlternativeIds.Where(id => id != alternativeId).ToHashSet()));

        var branchByAlternativeId = allAlternativeIds.ToDictionary(id => id, id => new HashSet<int> { id });
        foreach (var memberId in mainLineIds)
        {
            if (allAlternativeIds.Contains(memberId))
                continue; // an alternative is trivially its own branch head — every other alternative is removed from its own reduced graph, so nothing else can tie or beat it

            int? nearestAlternativeId = null;
            var nearestDistance = int.MaxValue;
            var tied = false;
            foreach (var alternativeId in allAlternativeIds)
            {
                if (!distancesByAlternativeId[alternativeId].TryGetValue(memberId, out var distance))
                    continue; // unreachable to this alternative once every other alternative is removed

                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestAlternativeId = alternativeId;
                    tied = false;
                }
                else if (distance == nearestDistance)
                {
                    tied = true;
                }
            }

            // Ties between two or more alternatives, or reaches none: trunk
            // (design.md D4) — simply never added to any branch below.
            if (nearestAlternativeId is { } winnerId && !tied)
                branchByAlternativeId[winnerId].Add(memberId);
        }

        return alternativeIdsBySlotKey
            .Select(kvp => new VersionSlot
            {
                SlotKey = kvp.Key,
                AlternativeIds = kvp.Value,
                BranchMemberIdsByAlternativeId = kvp.Value.ToDictionary(id => id, id => branchByAlternativeId[id]),
            })
            .OrderBy(slot => slot.SlotKey)
            .ToList();
    }

    private static Dictionary<int, int> BfsDistances(int sourceId, Dictionary<int, HashSet<int>> adjacency, HashSet<int> excludedIds)
    {
        var distances = new Dictionary<int, int> { [sourceId] = 0 };
        var queue = new Queue<int>();
        queue.Enqueue(sourceId);

        while (queue.Count > 0)
        {
            var currentId = queue.Dequeue();
            foreach (var neighbourId in adjacency[currentId])
            {
                if (excludedIds.Contains(neighbourId) || distances.ContainsKey(neighbourId))
                    continue;

                distances[neighbourId] = distances[currentId] + 1;
                queue.Enqueue(neighbourId);
            }
        }

        return distances;
    }

    /// <summary>The alternative a slot opens on (design.md D5, task 5.4),
    /// applying these rules in order and stopping at the first that
    /// separates the candidates:
    /// <list type="number">
    /// <item>The branch across which I've watched the most episodes, ties
    /// broken by more entries Completed/Rewatching — skipped entirely when
    /// I've watched nothing in any branch.</item>
    /// <item>The higher MAL score; where the two best alternatives' scores
    /// differ by 0.25 or less, the lower <c>PopularityRank</c> (more popular)
    /// wins. An alternative with no score sorts after every alternative that
    /// has one.</item>
    /// <item>Earliest <c>AiredFrom</c>, then lowest MAL id (<see cref="SeriesGraphBuilder.OrderKey"/>).</item>
    /// </list>
    /// Deliberately doesn't touch <c>AiredEpisodes</c>/airing-aware capping
    /// the way <c>SeriesService.BuildStats</c>'s watched-episode figures do —
    /// this only ranks branches against each other, so a raw
    /// <c>UserEntry.EpisodesWatched</c> sum is enough, and pulling in the
    /// schedule-aware figure would make picking a default depend on a live
    /// fetch it has no need for.</summary>
    internal static int DefaultAlternativeId(VersionSlot slot, Dictionary<int, AnimeMetadata> memberById) =>
        ByWatchProgress(slot, memberById)
        ?? ByScore(slot, memberById)
        ?? slot.AlternativeIds.OrderBy(id => SeriesGraphBuilder.OrderKey(memberById[id])).First();

    private static int? ByWatchProgress(VersionSlot slot, Dictionary<int, AnimeMetadata> memberById)
    {
        var watchedByAlternativeId = slot.AlternativeIds.ToDictionary(
            id => id,
            id => slot.BranchMemberIdsByAlternativeId[id].Sum(memberId => memberById[memberId].UserEntry?.EpisodesWatched ?? 0));

        if (watchedByAlternativeId.Values.All(watched => watched == 0))
            return null; // rule 1 is skipped outright when nothing is watched in any branch

        var completedByAlternativeId = slot.AlternativeIds.ToDictionary(
            id => id,
            id => slot.BranchMemberIdsByAlternativeId[id].Count(memberId =>
                memberById[memberId].UserEntry?.Status is WatchStatus.Completed or WatchStatus.Rewatching));

        var ranked = slot.AlternativeIds
            .OrderByDescending(id => watchedByAlternativeId[id])
            .ThenByDescending(id => completedByAlternativeId[id])
            .ToList();

        var top = ranked[0];
        var runnerUp = ranked[1];
        var isSeparated = watchedByAlternativeId[top] != watchedByAlternativeId[runnerUp] ||
            completedByAlternativeId[top] != completedByAlternativeId[runnerUp];
        return isSeparated ? top : null;
    }

    private static int? ByScore(VersionSlot slot, Dictionary<int, AnimeMetadata> memberById)
    {
        var scoredIds = slot.AlternativeIds
            .Where(id => memberById[id].MalScore is not null)
            .OrderByDescending(id => memberById[id].MalScore!.Value)
            .ToList();

        if (scoredIds.Count == 0)
            return null; // no alternative has a score at all: rule 2 has nothing to compare

        if (scoredIds.Count == 1)
            return scoredIds[0];

        var best = scoredIds[0];
        var runnerUp = scoredIds[1];
        var gap = memberById[best].MalScore!.Value - memberById[runnerUp].MalScore!.Value;
        if (gap > 0.25)
            return best;

        var bestPopularity = memberById[best].PopularityRank ?? int.MaxValue;
        var runnerUpPopularity = memberById[runnerUp].PopularityRank ?? int.MaxValue;
        if (bestPopularity != runnerUpPopularity)
            return bestPopularity < runnerUpPopularity ? best : runnerUp;

        return null; // a near-tie neither score nor popularity can settle: fall to rule 3
    }
}
