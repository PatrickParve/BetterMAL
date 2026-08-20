using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Metadata;
using AnimeTracker.Api.Services.Series;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimeTracker.Api.Tests.Services.Series;

// SeriesGraphBuilder.ClassifyMainLineChain (design.md decisions 1-2/tasks
// 2.1-2.4): a member is main-line-eligible unless it's special/music, a
// recap, or side content of another member (an outgoing parent_story edge or
// an incoming side_story edge). Candidate sequel/prequel chains are ranked by
// their count of eligible members, not raw size, so a franchise whose
// members carry no sequel/prequel edges at all — every member its own
// singleton chain — still resolves to its actual show rather than whichever
// promotional short happens to have aired first.
public class SeriesGraphBuilderTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static AnimeMetadata Anime(int id, string mediaType, DateOnly airedFrom) => new()
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
        new(db, new FakeMetadataRefreshService(), NullLogger<SeriesGraphBuilder>.Instance);

    private static async Task<List<SeriesMember>> BuildAndReadMembersAsync(AnimeTrackerDbContext db, int seedAnimeId)
    {
        var series = await CreateBuilder(db).BuildAsync(seedAnimeId, fetchBudget: 0, expandLeanMembers: false);
        Assert.NotNull(series);
        return await db.SeriesMembers.Where(m => m.SeriesId == series!.Id).ToListAsync();
    }

    [Fact]
    public async Task FranchiseWithNoSequelEdgesIsLedByItsShowNotItsPromos()
    {
        using var db = CreateDb();
        var show = Anime(1, "ona", new DateOnly(2025, 4, 6));
        var concept = Anime(2, "ona", new DateOnly(2022, 10, 29)); // aired years before the show
        var characterStory = Anime(3, "ona", new DateOnly(2024, 12, 29));
        var characterConcept = Anime(4, "ona", new DateOnly(2024, 7, 12));

        Relate(concept, show, "parent_story");
        Relate(characterStory, show, "parent_story");
        Relate(characterConcept, show, "parent_story");

        db.AnimeMetadata.AddRange(show, concept, characterStory, characterConcept);
        await db.SaveChangesAsync();

        var series = await CreateBuilder(db).BuildAsync(show.Id, fetchBudget: 0, expandLeanMembers: false);
        var members = await db.SeriesMembers.Where(m => m.SeriesId == series!.Id).ToListAsync();

        Assert.Equal(show.Id, series!.RootAnimeId);
        Assert.True(members.Single(m => m.AnimeId == show.Id).IsMainLine);
        Assert.False(members.Single(m => m.AnimeId == concept.Id).IsMainLine);
        Assert.False(members.Single(m => m.AnimeId == characterStory.Id).IsMainLine);
        Assert.False(members.Single(m => m.AnimeId == characterConcept.Id).IsMainLine);
    }

    [Fact]
    public async Task SideStoryTaggedMemberIsExtraEvenWhenItsMediaTypeAllowsMainLine()
    {
        using var db = CreateDb();
        var root = Anime(1, "tv", new DateOnly(2020, 1, 1));
        var sideStory = Anime(2, "tv", new DateOnly(2021, 1, 1)); // tv, would otherwise be eligible

        Relate(root, sideStory, "side_story"); // sideStory is a side story of root

        db.AnimeMetadata.AddRange(root, sideStory);
        await db.SaveChangesAsync();

        var members = await BuildAndReadMembersAsync(db, root.Id);

        Assert.True(members.Single(m => m.AnimeId == root.Id).IsMainLine);
        Assert.False(members.Single(m => m.AnimeId == sideStory.Id).IsMainLine);
    }

    [Fact]
    public async Task SideEntryBridgingTwoSeasonsDoesNotSplitTheMainLine()
    {
        using var db = CreateDb();
        var season1 = Anime(1, "tv", new DateOnly(2018, 1, 1));
        var bridge = Anime(2, "ova", new DateOnly(2019, 1, 1)); // side content that links the seasons
        var season2 = Anime(3, "tv", new DateOnly(2020, 1, 1));

        Relate(season1, bridge, "sequel");
        Relate(bridge, season2, "sequel");
        Relate(bridge, season1, "parent_story"); // bridge is side content of season1

        db.AnimeMetadata.AddRange(season1, bridge, season2);
        await db.SaveChangesAsync();

        var members = await BuildAndReadMembersAsync(db, season1.Id);

        Assert.True(members.Single(m => m.AnimeId == season1.Id).IsMainLine);
        Assert.True(members.Single(m => m.AnimeId == season2.Id).IsMainLine);
        Assert.False(members.Single(m => m.AnimeId == bridge.Id).IsMainLine); // bridges but never becomes main line
    }

    [Fact]
    public async Task LoneTvMemberOutranksAChainOfSideMovies()
    {
        using var db = CreateDb();
        var tvShow = Anime(1, "tv", new DateOnly(2015, 1, 1));
        var movie1 = Anime(2, "movie", new DateOnly(2016, 1, 1));
        var movie2 = Anime(3, "movie", new DateOnly(2017, 1, 1));

        Relate(tvShow, movie1, "spin_off"); // links the movies into the series without joining the sequel/prequel subgraph
        Relate(movie1, movie2, "sequel");

        db.AnimeMetadata.AddRange(tvShow, movie1, movie2);
        await db.SaveChangesAsync();

        var members = await BuildAndReadMembersAsync(db, tvShow.Id);

        Assert.True(members.Single(m => m.AnimeId == tvShow.Id).IsMainLine);
        Assert.False(members.Single(m => m.AnimeId == movie1.Id).IsMainLine);
        Assert.False(members.Single(m => m.AnimeId == movie2.Id).IsMainLine);
    }

    [Fact]
    public async Task FranchiseWithNoTvMemberRanksEveryChain()
    {
        using var db = CreateDb();
        var movieA = Anime(1, "movie", new DateOnly(2015, 1, 1));
        var movieB = Anime(2, "movie", new DateOnly(2016, 1, 1));
        var singleton = Anime(3, "movie", new DateOnly(2010, 1, 1)); // aired earliest, but its chain is shorter

        Relate(movieA, movieB, "sequel");
        Relate(movieA, singleton, "spin_off"); // links singleton into the series without a sequel/prequel edge

        db.AnimeMetadata.AddRange(movieA, movieB, singleton);
        await db.SaveChangesAsync();

        var members = await BuildAndReadMembersAsync(db, movieA.Id);

        Assert.True(members.Single(m => m.AnimeId == movieA.Id).IsMainLine);
        Assert.True(members.Single(m => m.AnimeId == movieB.Id).IsMainLine);
        Assert.False(members.Single(m => m.AnimeId == singleton.Id).IsMainLine);
    }

    [Fact]
    public async Task AllIneligibleMembersFallBackToTheUnreducedChain()
    {
        using var db = CreateDb();
        var special1 = Anime(1, "special", new DateOnly(2015, 1, 1));
        var special2 = Anime(2, "special", new DateOnly(2016, 1, 1));

        Relate(special1, special2, "sequel");

        db.AnimeMetadata.AddRange(special1, special2);
        await db.SaveChangesAsync();

        var members = await BuildAndReadMembersAsync(db, special1.Id);

        Assert.True(members.Single(m => m.AnimeId == special1.Id).IsMainLine);
        Assert.True(members.Single(m => m.AnimeId == special2.Id).IsMainLine);
    }

    // Music-aware `other` traversal (design.md decision 1/tasks 1.1-1.3): an
    // `other` edge is traversed exactly when one end is a cached `music`
    // entry, regardless of which end the seed build starts from, and never
    // spends fetch budget probing an uncached neighbour to find out.
    [Fact]
    public async Task ShowOtherLinkedToMusicEntryAdmitsItAsAnExtra()
    {
        using var db = CreateDb();
        var show = Anime(1, "tv", new DateOnly(2022, 4, 6));
        var song = Anime(2, "music", new DateOnly(2022, 4, 6));

        Relate(show, song, "other");

        db.AnimeMetadata.AddRange(show, song);
        await db.SaveChangesAsync();

        var members = await BuildAndReadMembersAsync(db, show.Id);

        Assert.True(members.Single(m => m.AnimeId == show.Id).IsMainLine);
        Assert.False(members.Single(m => m.AnimeId == song.Id).IsMainLine);
    }

    [Fact]
    public async Task BuildingFromTheMusicEntryProducesTheSameSeriesAsBuildingFromTheShow()
    {
        using var db = CreateDb();
        var show = Anime(1, "tv", new DateOnly(2022, 4, 6));
        var song = Anime(2, "music", new DateOnly(2022, 4, 6));

        Relate(show, song, "other");

        db.AnimeMetadata.AddRange(show, song);
        await db.SaveChangesAsync();

        var membersFromSong = await BuildAndReadMembersAsync(db, song.Id);

        Assert.Equal(
            new[] { show.Id, song.Id }.OrderBy(x => x),
            membersFromSong.Select(m => m.AnimeId).OrderBy(x => x));
        Assert.True(membersFromSong.Single(m => m.AnimeId == show.Id).IsMainLine);
        Assert.False(membersFromSong.Single(m => m.AnimeId == song.Id).IsMainLine);
    }

    [Fact]
    public async Task OtherLinkToNonMusicEntryIsNotTraversed()
    {
        using var db = CreateDb();
        var show = Anime(1, "tv", new DateOnly(2022, 4, 6));
        var season2 = Anime(2, "tv", new DateOnly(2023, 4, 6));
        var commercial = Anime(3, "cm", new DateOnly(2022, 4, 6));

        Relate(show, season2, "sequel");
        Relate(show, commercial, "other"); // a CM, not a song — must not join

        db.AnimeMetadata.AddRange(show, season2, commercial);
        await db.SaveChangesAsync();

        var members = await BuildAndReadMembersAsync(db, show.Id);

        Assert.Equal(2, members.Count);
        Assert.DoesNotContain(members, m => m.AnimeId == commercial.Id);
    }

    [Fact]
    public async Task MusicToMusicOtherLinkIsNotTraversed()
    {
        using var db = CreateDb();
        var show = Anime(1, "tv", new DateOnly(2022, 4, 6));
        var song = Anime(2, "music", new DateOnly(2022, 4, 6));
        var cover = Anime(3, "music", new DateOnly(2022, 5, 1));

        Relate(show, song, "other");
        Relate(song, cover, "other"); // both ends music — must not join

        db.AnimeMetadata.AddRange(show, song, cover);
        await db.SaveChangesAsync();

        var members = await BuildAndReadMembersAsync(db, show.Id);

        Assert.Equal(2, members.Count);
        Assert.DoesNotContain(members, m => m.AnimeId == cover.Id);
    }

    [Fact]
    public async Task OtherNeighbourWithNoCachedRowIsSkippedWithoutFetchOrPartial()
    {
        using var db = CreateDb();
        var show = Anime(1, "tv", new DateOnly(2022, 4, 6));
        var season2 = Anime(2, "tv", new DateOnly(2023, 4, 6));

        Relate(show, season2, "sequel");
        // No AnimeMetadata row exists for id 99 at all, and
        // FakeMetadataRefreshService throws if a fetch is attempted — this
        // build must not try.
        Relate(show, new AnimeMetadata { Id = 99, Title = "Uncached" }, "other");

        db.AnimeMetadata.AddRange(show, season2);
        await db.SaveChangesAsync();

        var series = await CreateBuilder(db).BuildAsync(show.Id, fetchBudget: 0, expandLeanMembers: false);
        var members = await db.SeriesMembers.Where(m => m.SeriesId == series!.Id).ToListAsync();

        Assert.Equal(2, members.Count);
        Assert.DoesNotContain(members, m => m.AnimeId == 99);
        Assert.False(series!.IsPartial);
    }

    private sealed class FakeMetadataRefreshService : IMetadataRefreshService
    {
        public Task<int> RefreshStaleBatchAsync(int batchSize, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task RefreshOneAsync(int animeId, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }
}
