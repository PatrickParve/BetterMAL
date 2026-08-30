using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Services.Series;

/// <summary>The main-line-eligibility predicate, extracted out of
/// <c>SeriesGraphBuilder.ClassifyMainLineChain</c> (split-series-by-version
/// design.md decision 1, task 4.1) so the version partition can decide which
/// members may anchor a telling using the same rule a single-telling build
/// already classifies its main line with. A member is ineligible when its
/// media type is <c>special</c>/<c>music</c>/<c>pv</c>, when it's a recap of
/// another member (<see cref="SeriesRelations.FindRecapIds"/>), or when it's
/// side content of another member (<see cref="SeriesRelations.FindSideContentIds"/>).
/// Evaluated over whichever member set is passed in — a single telling's own
/// members for main-line classification, or the whole undivided component for
/// anchor eligibility, which must be decided before the component is
/// partitioned so the answer never depends on which telling a build was
/// seeded from.</summary>
public static class SeriesMainLineEligibility
{
    public static HashSet<int> FindIneligibleIds(List<AnimeMetadata> members)
    {
        var candidateIds = members.Select(m => m.Id).ToHashSet();
        var ownEdges = members
            .SelectMany(m => m.RelatedAnime.Select(r => (OwnerId: m.Id, r.RelatedAnimeId, r.RelationType)))
            .ToList();

        var ineligibleIds = SeriesRelations.FindRecapIds(ownEdges, candidateIds);
        ineligibleIds.UnionWith(SeriesRelations.FindSideContentIds(ownEdges, candidateIds));

        foreach (var member in members)
        {
            if (member.MediaType is "special" or "music" or "pv")
                ineligibleIds.Add(member.Id);
        }

        return ineligibleIds;
    }
}
