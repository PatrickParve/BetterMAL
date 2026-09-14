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

    // A version neighbour with a version relation to a non-main-line member
    // now resolves via tier 2 (fix-alternative-version-grouping design.md
    // D2, spec rule 2) rather than falling to Other — this is exactly the
    // Road to the Top (Movie) shape (see
    // AnAlternativeVersionOfASideStoryExtraIsGroupedSeparatelyFromIt below
    // for the full-build version of the same claim).
    [Fact]
    public void AVersionNeighbourResolvesByItsVersionRelationToANonMainLineMember()
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
        Assert.Equal(RelationGroup.AlternativeVersion, result[versionNeighbourOfTheSideStory.Id]);
    }

    // A version neighbour is still never *reached by* inheritance (design.md
    // D3, "excluded at both ends"): when its only edge to the telling is a
    // plain story relation to a non-main-line extra — carrying no version
    // relation of its own, so neither tier 1 nor tier 2 can resolve it — it
    // must fall to Other rather than inherit that extra's group.
    [Fact]
    public void AVersionNeighbourWithNoVersionRelationIsNotReachedByInheritance()
    {
        var root = Anime(1, "tv");
        var sideStory = Anime(2, "ova"); // resolves SideStory directly, from its own edge to root
        var versionNeighbourOfTheSideStory = Anime(3, "tv"); // only edge is a story relation to sideStory, a non-main-line extra

        var edges = new List<RelationEdge>
        {
            new(root.Id, sideStory.Id, "side_story"),
            new(sideStory.Id, versionNeighbourOfTheSideStory.Id, "sequel"),
        };

        var result = SeriesGraphBuilder.ResolveExtraGroups(
            tellingIds: [root.Id, sideStory.Id, versionNeighbourOfTheSideStory.Id],
            mainLineIds: [root.Id],
            versionNeighbourIds: [versionNeighbourOfTheSideStory.Id],
            edges);

        Assert.Equal(RelationGroup.SideStory, result[sideStory.Id]);
        Assert.Equal(RelationGroup.Other, result[versionNeighbourOfTheSideStory.Id]);
    }

    // fix-alternative-version-grouping task 4.1, spec "A retelling inside its
    // own series reads as an alternative version": Code Geass' recap movie
    // trilogy carries alternative_version to the main-line TV series
    // alongside sequel/side_story edges to its own sibling movies. Asserted
    // from a full BuildAsync, not ResolveExtraGroups directly, so this
    // exercises both TraverseStoryComponentAsync's recording of the
    // member-to-member version edge (task 1.1) and tier 1's resolution of it
    // (task 2.2) together.
    [Fact]
    public async Task ARetellingMovieInsideItsOwnSeriesIsGroupedAlternativeVersion()
    {
        using var db = CreateDb();
        var tvSeries = Anime(1, "tv", new DateOnly(2006, 10, 6));
        var recapMovie1 = Anime(2, "movie", new DateOnly(2017, 10, 21));
        var recapMovie2 = Anime(3, "movie", new DateOnly(2018, 2, 9));
        var pictureDrama = Anime(4, "special", new DateOnly(2018, 2, 9));

        Relate(tvSeries, recapMovie2, "side_story"); // ties recapMovie2 — and, transitively, recapMovie1 — into the story component
        Relate(recapMovie1, recapMovie2, "sequel");
        Relate(recapMovie1, tvSeries, "alternative_version"); // a retelling of the TV series
        Relate(recapMovie1, pictureDrama, "side_story");

        db.AnimeMetadata.AddRange(tvSeries, recapMovie1, recapMovie2, pictureDrama);
        await db.SaveChangesAsync();

        var series = await CreateBuilder(db).BuildAsync(tvSeries.Id, fetchBudget: 0, probeBudget: 0, expandLeanMembers: false);
        var members = await db.SeriesMembers.Where(m => m.SeriesId == series!.Id).ToListAsync();

        Assert.True(members.Single(m => m.AnimeId == tvSeries.Id).IsMainLine);
        Assert.Equal(
            nameof(RelationGroup.AlternativeVersion),
            members.Single(m => m.AnimeId == recapMovie1.Id).RelationGroup);
    }

    // fix-alternative-version-grouping task 4.2, spec "An alternative version
    // of an extra is still an alternative version" and "A relation to the
    // main line outranks a version relation to an extra": Uma Musume's Road
    // to the Top has a side_story edge to the main line and its own movie cut
    // versioning it. The side-story extra keeps its tier-1 group; its movie,
    // with no relation to the main line at all, resolves by tier 2 instead of
    // falling to Other.
    [Fact]
    public async Task AnAlternativeVersionOfASideStoryExtraIsGroupedSeparatelyFromIt()
    {
        using var db = CreateDb();
        var mainLine = Anime(1, "tv", new DateOnly(2018, 1, 1));
        var sideStory = Anime(2, "ona", new DateOnly(2021, 1, 1));
        var sideStoryMovie = Anime(3, "movie", new DateOnly(2025, 1, 1));

        Relate(mainLine, sideStory, "side_story"); // sideStory is a side story of the main line
        Relate(sideStoryMovie, sideStory, "alternative_version"); // a movie cut of the side story

        db.AnimeMetadata.AddRange(mainLine, sideStory, sideStoryMovie);
        await db.SaveChangesAsync();

        var series = await CreateBuilder(db).BuildAsync(mainLine.Id, fetchBudget: 0, probeBudget: 0, expandLeanMembers: false);
        var members = await db.SeriesMembers.Where(m => m.SeriesId == series!.Id).ToListAsync();

        Assert.Equal(nameof(RelationGroup.SideStory), members.Single(m => m.AnimeId == sideStory.Id).RelationGroup);
        Assert.Equal(nameof(RelationGroup.AlternativeVersion), members.Single(m => m.AnimeId == sideStoryMovie.Id).RelationGroup);
    }

    // fix-alternative-version-grouping task 4.3, spec "An alternative version
    // of an extra does not pass its group on either": tier 2's result must
    // not seed or be reached by the tier-3 inheritance walk, exactly like a
    // version neighbour's own result (AVersionNeighbourDoesNotPassItsGroupOnByInheritance
    // above) — the two exclusions share one reason but are separate code
    // paths (tier-2-resolved ids vs. versionNeighbourIds), so this is its own
    // guard.
    [Fact]
    public void ATier2ResolvedExtraDoesNotPassItsGroupOnByInheritance()
    {
        var root = Anime(1, "tv");
        var sideStory = Anime(2, "ova"); // resolves SideStory directly, tier 1
        var versionOfSideStory = Anime(3, "movie"); // no main-line edge; resolves AlternativeVersion by tier 2
        var hangingOffTheVersion = Anime(4, "special"); // only edge is to versionOfSideStory

        var edges = new List<RelationEdge>
        {
            new(root.Id, sideStory.Id, "side_story"),
            new(versionOfSideStory.Id, sideStory.Id, "alternative_version"),
            new(versionOfSideStory.Id, hangingOffTheVersion.Id, "sequel"),
        };

        var result = SeriesGraphBuilder.ResolveExtraGroups(
            tellingIds: [root.Id, sideStory.Id, versionOfSideStory.Id, hangingOffTheVersion.Id],
            mainLineIds: [root.Id],
            versionNeighbourIds: [],
            edges);

        Assert.Equal(RelationGroup.SideStory, result[sideStory.Id]);
        Assert.Equal(RelationGroup.AlternativeVersion, result[versionOfSideStory.Id]);
        Assert.Equal(RelationGroup.Other, result[hangingOffTheVersion.Id]); // never inherits through the tier-2 result
    }

    // fix-alternative-version-grouping task 4.4: an extra carrying both
    // version relations, neither to the main line, resolves the
    // higher-precedence one — Alternative version over Alternative setting —
    // exactly as tier 1 already does for edges to the main line.
    [Fact]
    public void AnExtraWithBothVersionRelationsToNonMainLineMembersPrefersAlternativeVersion()
    {
        var root = Anime(1, "tv");
        var extraA = Anime(2, "ova"); // resolves SideStory directly, tier 1
        var extraB = Anime(3, "special"); // resolves SpinOff directly, tier 1
        var both = Anime(4, "movie"); // no main-line edge; carries both version relations

        var edges = new List<RelationEdge>
        {
            new(root.Id, extraA.Id, "side_story"),
            new(root.Id, extraB.Id, "spin_off"),
            new(both.Id, extraA.Id, "alternative_setting"),
            new(both.Id, extraB.Id, "alternative_version"),
        };

        var result = SeriesGraphBuilder.ResolveExtraGroups(
            tellingIds: [root.Id, extraA.Id, extraB.Id, both.Id],
            mainLineIds: [root.Id],
            versionNeighbourIds: [],
            edges);

        Assert.Equal(RelationGroup.AlternativeVersion, result[both.Id]);
    }

    private sealed class NoOpRefreshService : IMetadataRefreshService
    {
        public Task RefreshStaleBatchAsync(int batchSize, int? skipAnimeId, MalCallTally tally, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task RefreshOneAsync(int animeId, CancellationToken ct = default) =>
            throw new AnimeMetadataNotFoundException(animeId);
    }
}
