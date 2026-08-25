using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Metadata;
using AnimeTracker.Api.Services.Relations;
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
    // Standing for "this anime has been full-detail fetched" — only ever
    // compared against `default`, never against wall-clock time.
    private static readonly DateTimeOffset Fetched = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

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

    private static SeriesGraphBuilder CreateBuilder(AnimeTrackerDbContext db, IMetadataRefreshService? refreshService = null) =>
        new(db, refreshService ?? new FakeMetadataRefreshService(db), new RelationResolver(db), NullLogger<SeriesGraphBuilder>.Instance);

    private static async Task<List<SeriesMember>> BuildAndReadMembersAsync(
        AnimeTrackerDbContext db, int seedAnimeId, int probeBudget = 0, IMetadataRefreshService? refreshService = null)
    {
        var series = await CreateBuilder(db, refreshService).BuildAsync(seedAnimeId, fetchBudget: 0, probeBudget, expandLeanMembers: false);
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

        var series = await CreateBuilder(db).BuildAsync(show.Id, fetchBudget: 0, probeBudget: 0, expandLeanMembers: false);
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

    // Companion-media-aware `other` traversal (design.md decision 1/tasks
    // 1.1-1.3, widened to `pv` by polish-rewatch-more-and-filters design.md
    // D5a): an `other` edge is traversed exactly when one end is a cached
    // `music`/`pv` entry, regardless of which end the seed build starts
    // from. An uncached neighbour is resolved by a bounded probe rather than
    // silently left untraversable — see the probe-pass tests below.
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

    // --- pv extras (polish-rewatch-more-and-filters design.md D5) ---

    [Fact]
    public async Task ShowOtherLinkedToPvEntryAdmitsItAsAnExtra()
    {
        using var db = CreateDb();
        var show = Anime(1, "tv", new DateOnly(2022, 4, 6));
        var pv = Anime(2, "pv", new DateOnly(2022, 4, 6));

        Relate(show, pv, "other");

        db.AnimeMetadata.AddRange(show, pv);
        await db.SaveChangesAsync();

        var members = await BuildAndReadMembersAsync(db, show.Id);

        Assert.True(members.Single(m => m.AnimeId == show.Id).IsMainLine);
        Assert.False(members.Single(m => m.AnimeId == pv.Id).IsMainLine);
    }

    [Fact]
    public async Task PvToPvOtherLinkIsNotTraversed()
    {
        using var db = CreateDb();
        var show = Anime(1, "tv", new DateOnly(2022, 4, 6));
        var pv1 = Anime(2, "pv", new DateOnly(2022, 4, 6));
        var pv2 = Anime(3, "pv", new DateOnly(2022, 5, 1));

        Relate(show, pv1, "other");
        Relate(pv1, pv2, "other"); // both ends pv — must not chain

        db.AnimeMetadata.AddRange(show, pv1, pv2);
        await db.SaveChangesAsync();

        var members = await BuildAndReadMembersAsync(db, show.Id);

        Assert.Equal(2, members.Count);
        Assert.DoesNotContain(members, m => m.AnimeId == pv2.Id);
    }

    [Fact]
    public async Task PvToMusicOtherLinkIsNotTraversed()
    {
        using var db = CreateDb();
        var pv = Anime(1, "pv", new DateOnly(2022, 4, 6));
        var song = Anime(2, "music", new DateOnly(2022, 4, 6));

        // A promo for a theme song — both ends are companion media, so this
        // must not fuse the pv's franchise with the song's.
        Relate(pv, song, "other");

        db.AnimeMetadata.AddRange(pv, song);
        await db.SaveChangesAsync();

        var series = await CreateBuilder(db).BuildAsync(pv.Id, fetchBudget: 0, probeBudget: 0, expandLeanMembers: false);

        Assert.Null(series); // pv's component has no other member at all
    }

    [Fact]
    public async Task PvIsNeverMainLineEvenWhenSequelLinked()
    {
        using var db = CreateDb();
        var show = Anime(1, "tv", new DateOnly(2022, 4, 6));
        var pv = Anime(2, "pv", new DateOnly(2023, 4, 6));

        // MAL occasionally relates a pv with sequel/prequel rather than
        // other; pv must stay an extra regardless of how it's related.
        Relate(show, pv, "sequel");

        db.AnimeMetadata.AddRange(show, pv);
        await db.SaveChangesAsync();

        var members = await BuildAndReadMembersAsync(db, show.Id);

        Assert.True(members.Single(m => m.AnimeId == show.Id).IsMainLine);
        Assert.False(members.Single(m => m.AnimeId == pv.Id).IsMainLine);
    }

    // --- The `other`-edge probe pass (design.md D5c, tasks 4.3-4.5) ---

    [Fact]
    public async Task ZeroProbeBudgetSkipsAnUncachedOtherFarEndAndMarksPartial()
    {
        using var db = CreateDb();
        var show = Anime(1, "tv", new DateOnly(2022, 4, 6));
        var season2 = Anime(2, "tv", new DateOnly(2023, 4, 6));

        Relate(show, season2, "sequel");
        // No AnimeMetadata row exists for id 99 at all, and the fake refresh
        // service has no configured result for it — this build must not try.
        Relate(show, new AnimeMetadata { Id = 99, Title = "Uncached" }, "other");

        db.AnimeMetadata.AddRange(show, season2);
        await db.SaveChangesAsync();

        var refreshService = new FakeMetadataRefreshService(db);
        var series = await CreateBuilder(db, refreshService).BuildAsync(show.Id, fetchBudget: 0, probeBudget: 0, expandLeanMembers: false);
        var members = await db.SeriesMembers.Where(m => m.SeriesId == series!.Id).ToListAsync();

        Assert.Equal(2, members.Count);
        Assert.DoesNotContain(members, m => m.AnimeId == 99);
        Assert.Empty(refreshService.Calls); // a zero budget never attempts a fetch
        Assert.True(series!.IsPartial); // an unprobed `other` far end remains
    }

    [Fact]
    public async Task UncachedOtherFarEndIsProbedOnceAndTheVerdictIsCached()
    {
        using var db = CreateDb();
        var show = Anime(1, "tv", new DateOnly(2022, 4, 6));
        Relate(show, new AnimeMetadata { Id = 99, Title = "Uncached PV" }, "other");
        db.AnimeMetadata.Add(show);
        await db.SaveChangesAsync();

        var refreshService = new FakeMetadataRefreshService(db, new Dictionary<int, string> { [99] = "pv" });
        var members = await BuildAndReadMembersAsync(db, show.Id, probeBudget: 1, refreshService: refreshService);

        Assert.Equal(2, members.Count);
        Assert.False(members.Single(m => m.AnimeId == 99).IsMainLine);
        Assert.Equal([99], refreshService.Calls); // probed exactly once
        Assert.True(await db.AnimeMetadata.AnyAsync(a => a.Id == 99)); // now cached permanently
    }

    [Fact]
    public async Task AProbedCommercialCostsNothingOnTheNextBuild()
    {
        using var db = CreateDb();
        var show = Anime(1, "tv", new DateOnly(2022, 4, 6));
        var season2 = Anime(2, "tv", new DateOnly(2023, 4, 6)); // keeps the series non-empty either way
        Relate(show, season2, "sequel");
        Relate(show, new AnimeMetadata { Id = 99, Title = "Uncached CM" }, "other");
        db.AnimeMetadata.AddRange(show, season2);
        await db.SaveChangesAsync();

        var refreshService = new FakeMetadataRefreshService(db, new Dictionary<int, string> { [99] = "cm" });
        var firstBuildMembers = await BuildAndReadMembersAsync(db, show.Id, probeBudget: 1, refreshService: refreshService);
        Assert.DoesNotContain(firstBuildMembers, m => m.AnimeId == 99); // a commercial, not admitted

        // Rebuilding with zero probe budget must still resolve the edge from
        // the row the first build's probe already cached — no second probe.
        var secondBuild = await CreateBuilder(db, refreshService)
            .BuildAsync(show.Id, fetchBudget: 0, probeBudget: 0, expandLeanMembers: false);

        Assert.Equal([99], refreshService.Calls); // still just the one probe, ever
        Assert.False(secondBuild!.IsPartial);
    }

    [Fact]
    public async Task ProbeBudgetExhaustedAcrossMultipleFarEndsMarksPartial()
    {
        using var db = CreateDb();
        var show = Anime(1, "tv", new DateOnly(2022, 4, 6));
        var season2 = Anime(2, "tv", new DateOnly(2023, 4, 6)); // keeps the series non-empty either way
        Relate(show, season2, "sequel");
        Relate(show, new AnimeMetadata { Id = 98, Title = "Uncached A" }, "other");
        Relate(show, new AnimeMetadata { Id = 99, Title = "Uncached B" }, "other");
        db.AnimeMetadata.AddRange(show, season2);
        await db.SaveChangesAsync();

        var refreshService = new FakeMetadataRefreshService(db, new Dictionary<int, string> { [98] = "cm", [99] = "cm" });
        var series = await CreateBuilder(db, refreshService)
            .BuildAsync(show.Id, fetchBudget: 0, probeBudget: 1, expandLeanMembers: false);

        Assert.Single(refreshService.Calls); // only one of the two far ends was probed
        Assert.True(series!.IsPartial);
    }

    [Fact]
    public async Task ProbesNeverDrawOnTheMemberFetchBudget()
    {
        using var db = CreateDb();
        var show = Anime(1, "tv", new DateOnly(2022, 4, 6));
        // An uncached sequel (spends member fetch budget if fetched) and an
        // uncached `other` far end (spends probe budget if fetched) — with
        // one unit of each budget, both must resolve.
        Relate(show, new AnimeMetadata { Id = 50, Title = "Uncached Sequel", MediaType = "tv" }, "sequel");
        Relate(show, new AnimeMetadata { Id = 99, Title = "Uncached PV" }, "other");
        db.AnimeMetadata.Add(show);
        await db.SaveChangesAsync();

        var refreshService = new FakeMetadataRefreshService(db, new Dictionary<int, string> { [50] = "tv", [99] = "pv" });
        var series = await CreateBuilder(db, refreshService)
            .BuildAsync(show.Id, fetchBudget: 1, probeBudget: 1, expandLeanMembers: false);
        var members = await db.SeriesMembers.Where(m => m.SeriesId == series!.Id).ToListAsync();

        Assert.Equal(3, members.Count); // both the sequel and the pv were resolved
        Assert.Contains(members, m => m.AnimeId == 50);
        Assert.Contains(members, m => m.AnimeId == 99);
        Assert.False(series!.IsPartial); // one unit of each budget was enough for both
    }

    // --- 6.1/8.10: Contradicted edges are excluded from traversal ---

    [Fact]
    public async Task ContradictedEdgeExcludesTheMemberButKeepsTheRestOfTheSeries()
    {
        using var db = CreateDb();
        var root = Anime(17965, "movie", new DateOnly(1999, 1, 1));
        var contradicted = Anime(39360, "movie", new DateOnly(1999, 6, 1));
        var realMember = Anime(3044, "tv", new DateOnly(1980, 1, 1));
        root.LastSyncedAt = Fetched;
        contradicted.LastSyncedAt = Fetched;
        realMember.LastSyncedAt = Fetched;

        Relate(root, contradicted, "sequel"); // one-sided; 39360 stores nothing back
        Relate(root, realMember, "sequel");

        db.AnimeMetadata.AddRange(root, contradicted, realMember);
        db.AnimeAiringSyncs.AddRange(
            new AnimeAiringSync { AnimeId = 17965, AniListId = 1, RelationsFetchedAt = Fetched },
            new AnimeAiringSync { AnimeId = 39360, AniListId = 2, RelationsFetchedAt = Fetched });
        // No AniListRelation row between 17965 and 39360 at all: AniList
        // knows both and reports no edge -> Contradicted.
        await db.SaveChangesAsync();

        var members = await BuildAndReadMembersAsync(db, 17965);

        Assert.DoesNotContain(members, m => m.AnimeId == 39360);
        Assert.Contains(members, m => m.AnimeId == 17965);
        Assert.Contains(members, m => m.AnimeId == 3044);
    }

    [Fact]
    public async Task UnconfirmedUnadjudicatedEdgeStillTraverses()
    {
        // Same one-sided edge as above, but AniList has never been asked
        // about either anime — Unconfirmed, not Contradicted, so it must
        // still traverse (design.md decision 4's guard).
        using var db = CreateDb();
        var root = Anime(1, "movie", new DateOnly(1999, 1, 1));
        var oneSided = Anime(2, "movie", new DateOnly(1999, 6, 1));
        root.LastSyncedAt = Fetched;
        oneSided.LastSyncedAt = Fetched;

        Relate(root, oneSided, "sequel");

        db.AnimeMetadata.AddRange(root, oneSided);
        await db.SaveChangesAsync();

        var members = await BuildAndReadMembersAsync(db, 1);

        Assert.Contains(members, m => m.AnimeId == 2);
    }

    // --- 6.2/6.3/8.11: topological main-line ordering ---

    [Fact]
    public async Task ChainEdgeOverridesAirDateOrdering()
    {
        // Mirrors the live Jujutsu Kaisen case: the movie (0) airs after the
        // TV season but is the story's prequel, and MAL stores that mutually
        // (0 --sequel--> TV, TV --prequel--> 0).
        using var db = CreateDb();
        var jjk0 = Anime(48561, "movie", new DateOnly(2021, 12, 24));
        var jjkTv = Anime(40748, "tv", new DateOnly(2020, 10, 3));
        Relate(jjk0, jjkTv, "sequel");
        Relate(jjkTv, jjk0, "prequel");

        db.AnimeMetadata.AddRange(jjk0, jjkTv);
        await db.SaveChangesAsync();

        var members = await BuildAndReadMembersAsync(db, 48561);
        var ordered = members.Where(m => m.IsMainLine).OrderBy(m => m.Order).Select(m => m.AnimeId).ToList();

        Assert.Equal([48561, 40748], ordered);
    }

    [Fact]
    public async Task UnconstrainedSiblingsFallBackToAirDate()
    {
        // root has two sequels with no chain edge to each other — a "fork" —
        // so their relative order is unconstrained and must fall back to air
        // date rather than to id order.
        using var db = CreateDb();
        var root = Anime(1, "tv", new DateOnly(2015, 1, 1));
        var earlierAiredHigherId = Anime(20, "tv", new DateOnly(2017, 1, 1));
        var laterAiredLowerId = Anime(10, "tv", new DateOnly(2018, 1, 1));
        Relate(root, earlierAiredHigherId, "sequel");
        Relate(root, laterAiredLowerId, "sequel");

        db.AnimeMetadata.AddRange(root, earlierAiredHigherId, laterAiredLowerId);
        await db.SaveChangesAsync();

        var members = await BuildAndReadMembersAsync(db, 1);
        var ordered = members.Where(m => m.IsMainLine).OrderBy(m => m.Order).Select(m => m.AnimeId).ToList();

        Assert.Equal([1, 20, 10], ordered);
    }

    [Fact]
    public async Task CyclicChainDoesNotThrowAndFallsBackToAirDateOrder()
    {
        using var db = CreateDb();
        var a = Anime(1, "tv", new DateOnly(2018, 1, 1));
        var b = Anime(2, "tv", new DateOnly(2019, 1, 1));
        // A 2-cycle: each claims to be the other's sequel.
        Relate(a, b, "sequel");
        Relate(b, a, "sequel");

        db.AnimeMetadata.AddRange(a, b);
        await db.SaveChangesAsync();

        var members = await BuildAndReadMembersAsync(db, 1); // must not throw
        var ordered = members.Where(m => m.IsMainLine).OrderBy(m => m.Order).Select(m => m.AnimeId).ToList();

        Assert.Equal([1, 2], ordered); // cycle remainder emitted in air-date order
    }

    // --- 7.5: SelectedTitle/SelectedPictureUrl survive a rebuild and an absorption (design.md D9) ---

    [Fact]
    public async Task RebuildKeepsAChosenTitleAndPicture()
    {
        using var db = CreateDb();
        var a = Anime(1, "tv", new DateOnly(2018, 1, 1));
        var b = Anime(2, "tv", new DateOnly(2019, 1, 1));
        Relate(a, b, "sequel");
        db.AnimeMetadata.AddRange(a, b);
        await db.SaveChangesAsync();

        var series = await CreateBuilder(db).BuildAsync(a.Id, fetchBudget: 0, probeBudget: 0, expandLeanMembers: false);
        var stored = await db.Series.FirstAsync(s => s.Id == series!.Id);
        stored.SelectedTitle = "My Title";
        stored.SelectedPictureUrl = "chosen.jpg";
        await db.SaveChangesAsync();

        await CreateBuilder(db).BuildAsync(a.Id, fetchBudget: 0, probeBudget: 0, expandLeanMembers: false);

        var rebuilt = await db.Series.AsNoTracking().FirstAsync(s => s.Id == series!.Id);
        Assert.Equal("My Title", rebuilt.SelectedTitle);
        Assert.Equal("chosen.jpg", rebuilt.SelectedPictureUrl);
    }

    [Fact]
    public async Task AChoiceOutlivesItsSourceMemberLeavingTheSeries()
    {
        using var db = CreateDb();
        var a = Anime(1, "tv", new DateOnly(2018, 1, 1));
        var b = Anime(2, "tv", new DateOnly(2019, 1, 1));
        var c = Anime(3, "tv", new DateOnly(2020, 1, 1));
        Relate(a, b, "sequel");
        Relate(b, c, "sequel");
        db.AnimeMetadata.AddRange(a, b, c);
        await db.SaveChangesAsync();

        var series = await CreateBuilder(db).BuildAsync(a.Id, fetchBudget: 0, probeBudget: 0, expandLeanMembers: false);
        var stored = await db.Series.FirstAsync(s => s.Id == series!.Id);
        stored.SelectedPictureUrl = "picked-from-c.jpg"; // stands in for a picture chosen from member C
        await db.SaveChangesAsync();

        // C leaves the component: remove its only connecting edge.
        var edge = await db.AnimeRelatedAnime.FirstAsync(r => r.AnimeId == b.Id && r.RelatedAnimeId == c.Id);
        db.AnimeRelatedAnime.Remove(edge);
        await db.SaveChangesAsync();

        var rebuiltSeries = await CreateBuilder(db).BuildAsync(a.Id, fetchBudget: 0, probeBudget: 0, expandLeanMembers: false);
        var members = await db.SeriesMembers.Where(m => m.SeriesId == rebuiltSeries!.Id).ToListAsync();
        var rebuilt = await db.Series.AsNoTracking().FirstAsync(s => s.Id == series!.Id);

        Assert.DoesNotContain(members, m => m.AnimeId == c.Id); // C genuinely left
        Assert.Equal("picked-from-c.jpg", rebuilt.SelectedPictureUrl); // choice is kept anyway
    }

    [Fact]
    public async Task AbsorptionFillsInAMissingChoiceFromTheLargestOverlapAbsorbedSeries()
    {
        using var db = CreateDb();
        var (a, bAnime, cAnime, dAnime, eAnime, fAnime) = SixMemberChain();
        db.AnimeMetadata.AddRange(a, bAnime, cAnime, dAnime, eAnime, fAnime);

        // Pre-existing stored series overlapping the new build's component
        // differently: the eventual survivor (largest overlap), and two
        // absorbed candidates with differing overlap counts.
        db.Series.Add(new AnimeTracker.Api.Models.Series { Id = 1, RootAnimeId = a.Id, BuiltAt = DateTimeOffset.UtcNow });
        db.Series.Add(new AnimeTracker.Api.Models.Series { Id = 2, RootAnimeId = cAnime.Id, BuiltAt = DateTimeOffset.UtcNow, SelectedTitle = "FromTwo" });
        db.Series.Add(new AnimeTracker.Api.Models.Series { Id = 3, RootAnimeId = eAnime.Id, BuiltAt = DateTimeOffset.UtcNow, SelectedTitle = "FromThree" });
        db.SeriesMembers.AddRange(
            new SeriesMember { AnimeId = a.Id, SeriesId = 1, IsMainLine = true, Order = 0 },
            new SeriesMember { AnimeId = bAnime.Id, SeriesId = 1, IsMainLine = true, Order = 1 },
            new SeriesMember { AnimeId = fAnime.Id, SeriesId = 1, IsMainLine = true, Order = 2 }, // overlap 3 -> survivor
            new SeriesMember { AnimeId = cAnime.Id, SeriesId = 2, IsMainLine = true, Order = 0 },
            new SeriesMember { AnimeId = dAnime.Id, SeriesId = 2, IsMainLine = true, Order = 1 }, // overlap 2
            new SeriesMember { AnimeId = eAnime.Id, SeriesId = 3, IsMainLine = true, Order = 0 }); // overlap 1
        await db.SaveChangesAsync();

        var result = await CreateBuilder(db).BuildAsync(a.Id, fetchBudget: 0, probeBudget: 0, expandLeanMembers: false);

        Assert.Equal(1, result!.Id); // largest-overlap series (3 members) survives
        var survivor = await db.Series.AsNoTracking().FirstAsync(s => s.Id == 1);
        Assert.Equal("FromTwo", survivor.SelectedTitle); // adopted from series 2 (overlap 2), not series 3 (overlap 1)
        Assert.False(await db.Series.AsNoTracking().AnyAsync(s => s.Id == 2 || s.Id == 3)); // absorbed rows deleted
    }

    [Fact]
    public async Task AbsorptionDoesNotOverwriteTheSurvivorsOwnChoice()
    {
        using var db = CreateDb();
        var (a, bAnime, cAnime, dAnime, eAnime, fAnime) = SixMemberChain();
        db.AnimeMetadata.AddRange(a, bAnime, cAnime, dAnime, eAnime, fAnime);

        db.Series.Add(new AnimeTracker.Api.Models.Series
        {
            Id = 1, RootAnimeId = a.Id, BuiltAt = DateTimeOffset.UtcNow, SelectedTitle = "Survivor's Own",
        });
        db.Series.Add(new AnimeTracker.Api.Models.Series { Id = 2, RootAnimeId = cAnime.Id, BuiltAt = DateTimeOffset.UtcNow, SelectedTitle = "FromTwo" });
        db.SeriesMembers.AddRange(
            new SeriesMember { AnimeId = a.Id, SeriesId = 1, IsMainLine = true, Order = 0 },
            new SeriesMember { AnimeId = bAnime.Id, SeriesId = 1, IsMainLine = true, Order = 1 },
            new SeriesMember { AnimeId = fAnime.Id, SeriesId = 1, IsMainLine = true, Order = 2 },
            new SeriesMember { AnimeId = cAnime.Id, SeriesId = 2, IsMainLine = true, Order = 0 },
            new SeriesMember { AnimeId = dAnime.Id, SeriesId = 2, IsMainLine = true, Order = 1 });
        await db.SaveChangesAsync();

        await CreateBuilder(db).BuildAsync(a.Id, fetchBudget: 0, probeBudget: 0, expandLeanMembers: false);

        var survivor = await db.Series.AsNoTracking().FirstAsync(s => s.Id == 1);
        Assert.Equal("Survivor's Own", survivor.SelectedTitle);
    }

    // A straight sequel chain A-B-C-D-E-F, all tv, air-date ordered — one
    // connected component so a single BuildAsync(a.Id) call reaches all six.
    private static (AnimeMetadata A, AnimeMetadata B, AnimeMetadata C, AnimeMetadata D, AnimeMetadata E, AnimeMetadata F) SixMemberChain()
    {
        var a = Anime(1, "tv", new DateOnly(2015, 1, 1));
        var b = Anime(2, "tv", new DateOnly(2016, 1, 1));
        var c = Anime(3, "tv", new DateOnly(2017, 1, 1));
        var d = Anime(4, "tv", new DateOnly(2018, 1, 1));
        var e = Anime(5, "tv", new DateOnly(2019, 1, 1));
        var f = Anime(6, "tv", new DateOnly(2020, 1, 1));
        Relate(a, b, "sequel");
        Relate(b, c, "sequel");
        Relate(c, d, "sequel");
        Relate(d, e, "sequel");
        Relate(e, f, "sequel");
        return (a, b, c, d, e, f);
    }

    // `probeResults` simulates a live full-detail fetch: when configured for
    // an id, RefreshOneAsync inserts a cached row with that media type (as a
    // real fetch would populate one); when not configured, it throws
    // AnimeMetadataNotFoundException, matching a MAL 404. Every call is
    // recorded in `Calls` so probe tests can assert an edge was probed at
    // most once.
    private sealed class FakeMetadataRefreshService(AnimeTrackerDbContext db, Dictionary<int, string>? probeResults = null)
        : IMetadataRefreshService
    {
        public List<int> Calls { get; } = [];

        public Task<int> RefreshStaleBatchAsync(int batchSize, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public async Task RefreshOneAsync(int animeId, CancellationToken ct = default)
        {
            Calls.Add(animeId);
            if (probeResults is null || !probeResults.TryGetValue(animeId, out var mediaType))
                throw new AnimeMetadataNotFoundException(animeId);

            db.AnimeMetadata.Add(new AnimeMetadata { Id = animeId, Title = $"Anime {animeId}", MediaType = mediaType });
            await db.SaveChangesAsync(ct);
        }
    }
}
