using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Metadata;
using AnimeTracker.Api.Services.Relations;
using AnimeTracker.Api.Services.Series;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimeTracker.Api.Tests.Services.Series;

// SeriesGraphBuilder's extras grouping (split-series-by-version design.md
// decision 4, tasks 5.1-5.4): each extra's RelationGroup is the
// highest-precedence direct edge to a main-line member
// (SeriesRelations.HighestPrecedenceGroup), else inherited breadth-first from
// the nearest already-resolved extra, else Other. A version neighbour never
// passes its own group on by inheritance (rebuild-series-by-story-component
// design.md D2, task 5.5).
public class SeriesGraphBuilderExtraGroupResolutionTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static AnimeMetadata Anime(int id, string mediaType, DateOnly? airedFrom = null) => new()
    {
        Id = id,
        Title = $"Anime {id}",
        MediaType = mediaType,
        AiredFrom = airedFrom,
    };

    private static void Relate(AnimeMetadata from, AnimeMetadata to, string relationType) =>
        from.RelatedAnime.Add(new AnimeRelatedAnime
        {
            AnimeId = from.Id,
            RelatedAnimeId = to.Id,
            RelationType = relationType,
            Title = to.Title,
        });

    private static SeriesGraphBuilder CreateBuilder(AnimeTrackerDbContext db) =>
        new(db, new NoOpRefreshService(), new RelationResolver(db), NullLogger<SeriesGraphBuilder>.Instance);

    [Fact]
    public async Task ASpecialHangingOffASideStoryInheritsSideStory()
    {
        using var db = CreateDb();
        var root = Anime(1, "tv", new DateOnly(2018, 1, 1));
        var sideStory = Anime(2, "ova");
        var hangingSpecial = Anime(3, "special"); // no edge to root at all — only to sideStory

        Relate(root, sideStory, "side_story"); // sideStory is a side story of root
        Relate(sideStory, hangingSpecial, "sequel");

        db.AnimeMetadata.AddRange(root, sideStory, hangingSpecial);
        await db.SaveChangesAsync();

        var series = await CreateBuilder(db).BuildAsync(root.Id, fetchBudget: 0, probeBudget: 0, expandLeanMembers: false);
        var members = await db.SeriesMembers.Where(m => m.SeriesId == series!.Id).ToListAsync();

        Assert.True(members.Single(m => m.AnimeId == root.Id).IsMainLine);
        Assert.Equal(nameof(RelationGroup.SideStory), members.Single(m => m.AnimeId == sideStory.Id).RelationGroup);
        Assert.Equal(nameof(RelationGroup.SideStory), members.Single(m => m.AnimeId == hangingSpecial.Id).RelationGroup);
    }

    // ResolveExtraGroups's version-neighbour exclusion
    // (rebuild-series-by-story-component design.md D2, task 5.5): a version
    // neighbour resolves its own group directly (rule 1, a direct edge to a
    // main-line member) but never passes it on to whatever else it happens
    // to sit next to — otherwise a story extra hanging off Brotherhood would
    // misread as an Alternative version merely for being next to it.
    [Fact]
    public void AVersionNeighbourDoesNotPassItsGroupOnByInheritance()
    {
        var root = Anime(1, "tv");
        var versionNeighbour = Anime(2, "tv"); // e.g. Brotherhood: resolves AlternativeVersion directly
        var hangingOffTheNeighbour = Anime(3, "special"); // only edge is to the version neighbour, not to root

        var edges = new List<RelationEdge>
        {
            new(root.Id, versionNeighbour.Id, "alternative_version"),
            new(versionNeighbour.Id, hangingOffTheNeighbour.Id, "side_story"),
        };

        var result = SeriesGraphBuilder.ResolveExtraGroups(
            tellingIds: [root.Id, versionNeighbour.Id, hangingOffTheNeighbour.Id],
            mainLineIds: [root.Id],
            versionNeighbourIds: [versionNeighbour.Id],
            edges);

        Assert.Equal(RelationGroup.AlternativeVersion, result[versionNeighbour.Id]); // resolves directly, rule 1
        Assert.Equal(RelationGroup.Other, result[hangingOffTheNeighbour.Id]); // never inherits through it
    }

    // Without the exclusion, the same graph would hand hangingOffTheNeighbour
    // AlternativeVersion by inheritance (the general BFS-inheritance rule
    // ASpecialHangingOffASideStoryInheritsSideStory above exercises) — this
    // is the control that shows the version-neighbour source is what changes
    // the outcome, not some other difference between the two edges.
    [Fact]
    public void TheSameGraphWithoutTheVersionNeighbourExclusionWouldInherit()
    {
        var root = Anime(1, "tv");
        var versionNeighbour = Anime(2, "tv");
        var hangingOffTheNeighbour = Anime(3, "special");

        var edges = new List<RelationEdge>
        {
            new(root.Id, versionNeighbour.Id, "alternative_version"),
            new(versionNeighbour.Id, hangingOffTheNeighbour.Id, "side_story"),
        };

        var result = SeriesGraphBuilder.ResolveExtraGroups(
            tellingIds: [root.Id, versionNeighbour.Id, hangingOffTheNeighbour.Id],
            mainLineIds: [root.Id],
            versionNeighbourIds: [], // nothing excluded this time
            edges);

        Assert.Equal(RelationGroup.AlternativeVersion, result[hangingOffTheNeighbour.Id]);
    }

    // A version neighbour is also never *reached by* inheritance: its only
    // edge here is to a non-main-line extra (not to the main line itself),
    // so rule 1 can't resolve it directly either — it must fall to Other
    // rather than inherit the extra's group.
    [Fact]
    public void AVersionNeighbourIsNotReachedByInheritance()
    {
        var root = Anime(1, "tv");
        var sideStory = Anime(2, "ova"); // resolves SideStory directly, from its own edge to root
        var versionNeighbourOfTheSideStory = Anime(3, "tv"); // only edge is to sideStory, a non-main-line extra

        var edges = new List<RelationEdge>
        {
            new(root.Id, sideStory.Id, "side_story"),
            new(sideStory.Id, versionNeighbourOfTheSideStory.Id, "alternative_version"),
        };

        var result = SeriesGraphBuilder.ResolveExtraGroups(
            tellingIds: [root.Id, sideStory.Id, versionNeighbourOfTheSideStory.Id],
            mainLineIds: [root.Id],
            versionNeighbourIds: [versionNeighbourOfTheSideStory.Id],
            edges);

        Assert.Equal(RelationGroup.SideStory, result[sideStory.Id]);
        Assert.Equal(RelationGroup.Other, result[versionNeighbourOfTheSideStory.Id]);
    }

    private sealed class NoOpRefreshService : IMetadataRefreshService
    {
        public Task<int> RefreshStaleBatchAsync(int batchSize, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task RefreshOneAsync(int animeId, CancellationToken ct = default) =>
            throw new AnimeMetadataNotFoundException(animeId);
    }
}
