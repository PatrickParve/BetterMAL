using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Series;

namespace AnimeTracker.Api.Tests.Services.Series;

// SeriesVersionSlots (rebuild-series-by-story-component design.md decision
// D4, tasks 5.2/5.4): version slots and branches over a series' already-
// classified main line — a connected group of size >= 2 over version
// relations among main-line members is a slot; a main-line member strictly
// nearest one alternative, over sequel/prequel with every other alternative
// removed, is that alternative's branch; anything tied or unreachable is
// trunk.
public class SeriesVersionSlotsTests
{
    private static AnimeMetadata Anime(
        int id, string mediaType, DateOnly? airedFrom = null, double? malScore = null, int? popularityRank = null) => new()
    {
        Id = id,
        Title = $"Anime {id}",
        MediaType = mediaType,
        AiredFrom = airedFrom,
        MalScore = malScore,
        PopularityRank = popularityRank,
    };

    private static void Relate(AnimeMetadata from, AnimeMetadata to, string relationType) =>
        from.RelatedAnime.Add(new AnimeRelatedAnime
        {
            AnimeId = from.Id,
            RelatedAnimeId = to.Id,
            RelationType = relationType,
            Title = to.Title,
        });

    private static List<SeriesVersionSlots.VersionSlot> Resolve(params AnimeMetadata[] mainLine)
    {
        var mainLineIds = mainLine.Select(m => m.Id).ToHashSet();
        var memberById = mainLine.ToDictionary(m => m.Id);
        return SeriesVersionSlots.Resolve(mainLine.ToList(), mainLineIds, memberById);
    }

    // 38000 (S1) --sequel--> {40456 movie, 49926 TV arc} --sequel--> 47778
    // (S2) --sequel--> 51019. Movie and TV arc are alternative_version of
    // each other; every other entry is equidistant from both once the other
    // alternative is removed, so the whole rest of the franchise is trunk —
    // the picker changes exactly one position (design.md D4's worked
    // example).
    [Fact]
    public void DemonSlayerHasOneSlotWithAnAllTrunkRemainder()
    {
        var s1 = Anime(38000, "tv");
        var movie = Anime(40456, "movie");
        var tvArc = Anime(49926, "tv");
        var s2 = Anime(47778, "tv");
        var laterArc = Anime(51019, "tv");

        Relate(s1, movie, "sequel");
        Relate(s1, tvArc, "sequel");
        Relate(movie, tvArc, "alternative_version");
        Relate(movie, s2, "sequel");
        Relate(tvArc, s2, "sequel");
        Relate(s2, laterArc, "sequel");

        var slots = Resolve(s1, movie, tvArc, s2, laterArc);

        var slot = Assert.Single(slots);
        Assert.Equal(movie.Id, slot.SlotKey); // lower of the two alternative ids
        Assert.Equal(new[] { movie.Id, tvArc.Id }.OrderBy(x => x), slot.AlternativeIds.OrderBy(x => x));

        // Trunk members never appear in either branch.
        var branchedIds = slot.BranchMemberIdsByAlternativeId.Values.SelectMany(ids => ids).ToHashSet();
        Assert.DoesNotContain(s1.Id, branchedIds);
        Assert.DoesNotContain(s2.Id, branchedIds);
        Assert.DoesNotContain(laterArc.Id, branchedIds);

        // Each alternative's branch holds only itself.
        Assert.Equal(new HashSet<int> { movie.Id }, slot.BranchMemberIdsByAlternativeId[movie.Id]);
        Assert.Equal(new HashSet<int> { tvArc.Id }, slot.BranchMemberIdsByAlternativeId[tvArc.Id]);
    }

    // Fate/Zero (10087) -> Fate/Zero 2nd Season (11741) -> four alternatives:
    // F/SN '06 (356), UBW movie (6922), UBW TV (22297), Heaven's Feel I
    // (25537). UBW Prologue (27821, a tv_special — eligible per the
    // series-page capability's recap/special carve-out) and UBW 2nd Season
    // (28701) hang off UBW TV alone; Heaven's Feel II/III (33049/33050) hang
    // off Heaven's Feel I alone. 10087/11741 are equidistant from every
    // alternative and stay trunk (design.md D4's worked example).
    [Fact]
    public void FatesFourAlternativesPutUbwPrologueAndTheHeavensFeelFilmsInTheirOwnBranches()
    {
        var fateZero = Anime(10087, "tv");
        var fateZero2nd = Anime(11741, "tv");
        var fsn06 = Anime(356, "tv");
        var ubwMovie = Anime(6922, "movie");
        var ubwTv = Anime(22297, "tv");
        var heavensFeelI = Anime(25537, "movie");
        var ubwPrologue = Anime(27821, "tv_special");
        var ubw2nd = Anime(28701, "tv");
        var heavensFeelII = Anime(33049, "movie");
        var heavensFeelIII = Anime(33050, "movie");

        Relate(fateZero, fateZero2nd, "sequel");
        Relate(fateZero2nd, fsn06, "sequel");
        Relate(fateZero2nd, ubwMovie, "sequel");
        Relate(fateZero2nd, ubwTv, "sequel");
        Relate(fateZero2nd, heavensFeelI, "sequel");
        Relate(fsn06, ubwMovie, "alternative_version");
        Relate(fsn06, ubwTv, "alternative_version");
        Relate(fsn06, heavensFeelI, "alternative_version");
        Relate(ubwPrologue, ubwTv, "sequel"); // the prologue precedes UBW TV
        Relate(ubwTv, ubw2nd, "sequel");
        Relate(heavensFeelI, heavensFeelII, "sequel");
        Relate(heavensFeelII, heavensFeelIII, "sequel");

        var mainLine = new[]
        {
            fateZero, fateZero2nd, fsn06, ubwMovie, ubwTv, heavensFeelI, ubwPrologue, ubw2nd, heavensFeelII, heavensFeelIII,
        };
        var slots = Resolve(mainLine);

        var slot = Assert.Single(slots);
        Assert.Equal(fsn06.Id, slot.SlotKey); // 356 is the lowest of the four alternative ids
        Assert.Equal(
            new[] { fsn06.Id, ubwMovie.Id, ubwTv.Id, heavensFeelI.Id }.OrderBy(x => x),
            slot.AlternativeIds.OrderBy(x => x));

        var branchedIds = slot.BranchMemberIdsByAlternativeId.Values.SelectMany(ids => ids).ToHashSet();
        Assert.DoesNotContain(fateZero.Id, branchedIds);
        Assert.DoesNotContain(fateZero2nd.Id, branchedIds);

        Assert.Equal(
            new HashSet<int> { ubwTv.Id, ubwPrologue.Id, ubw2nd.Id },
            slot.BranchMemberIdsByAlternativeId[ubwTv.Id]);
        Assert.Equal(
            new HashSet<int> { heavensFeelI.Id, heavensFeelII.Id, heavensFeelIII.Id },
            slot.BranchMemberIdsByAlternativeId[heavensFeelI.Id]);
        Assert.Equal(new HashSet<int> { fsn06.Id }, slot.BranchMemberIdsByAlternativeId[fsn06.Id]);
        Assert.Equal(new HashSet<int> { ubwMovie.Id }, slot.BranchMemberIdsByAlternativeId[ubwMovie.Id]);
    }

    // Clannad Movie's alternative_version edges to Clannad and After Story
    // never reach here at all — it has no story relation of its own, so it
    // folds in as an extra and is never a main-line member (design.md D2).
    // The main line itself carries no version relation among its own two
    // members, so there's no slot.
    [Fact]
    public void ClannadsMainLineProducesNoSlot()
    {
        var clannad = Anime(1, "tv");
        var afterStory = Anime(2, "tv");
        Relate(clannad, afterStory, "sequel");

        var slots = Resolve(clannad, afterStory);

        Assert.Empty(slots);
    }

    [Fact]
    public void AnOrdinarySeriesWithNoVersionRelationsProducesNoSlot()
    {
        var season1 = Anime(1, "tv");
        var season2 = Anime(2, "tv");
        var movie = Anime(3, "movie");
        Relate(season1, season2, "sequel");
        Relate(season2, movie, "sequel");

        var slots = Resolve(season1, season2, movie);

        Assert.Empty(slots);
    }

    // --- DefaultAlternativeId (design.md D5, task 5.4) ---

    private static SeriesVersionSlots.VersionSlot TwoAlternativeSlot(AnimeMetadata a, AnimeMetadata b) => new()
    {
        SlotKey = Math.Min(a.Id, b.Id),
        AlternativeIds = [a.Id, b.Id],
        BranchMemberIdsByAlternativeId = new Dictionary<int, HashSet<int>>
        {
            [a.Id] = [a.Id],
            [b.Id] = [b.Id],
        },
    };

    [Fact]
    public void TheRouteIHaveWatchedOpens()
    {
        var movie = Anime(1, "movie", malScore: 8.0);
        var tvArc = Anime(2, "tv", malScore: 8.0);
        movie.UserEntry = new UserAnimeEntry { AnimeId = movie.Id, Status = WatchStatus.Completed, EpisodesWatched = 1 };

        var slot = TwoAlternativeSlot(movie, tvArc);
        var memberById = new Dictionary<int, AnimeMetadata> { [movie.Id] = movie, [tvArc.Id] = tvArc };

        Assert.Equal(movie.Id, SeriesVersionSlots.DefaultAlternativeId(slot, memberById));
    }

    [Fact]
    public void TheOtherRouteOpensWhenThatIsTheOneIWatched()
    {
        var movie = Anime(1, "movie", malScore: 8.0);
        var tvArc = Anime(2, "tv", malScore: 8.0);
        tvArc.UserEntry = new UserAnimeEntry { AnimeId = tvArc.Id, Status = WatchStatus.Completed, EpisodesWatched = 26 };

        var slot = TwoAlternativeSlot(movie, tvArc);
        var memberById = new Dictionary<int, AnimeMetadata> { [movie.Id] = movie, [tvArc.Id] = tvArc };

        Assert.Equal(tvArc.Id, SeriesVersionSlots.DefaultAlternativeId(slot, memberById));
    }

    [Fact]
    public void PopularitySettlesACloseScoreWhenNothingIsWatched()
    {
        var ubwTv = Anime(1, "tv", malScore: 8.19, popularityRank: 50);
        var heavensFeel = Anime(2, "movie", malScore: 8.11, popularityRank: 500);

        var slot = TwoAlternativeSlot(ubwTv, heavensFeel);
        var memberById = new Dictionary<int, AnimeMetadata> { [ubwTv.Id] = ubwTv, [heavensFeel.Id] = heavensFeel };

        Assert.Equal(ubwTv.Id, SeriesVersionSlots.DefaultAlternativeId(slot, memberById));
    }

    [Fact]
    public void AClearlyBetterRouteWinsOutrightRegardlessOfPopularity()
    {
        var better = Anime(1, "tv", malScore: 8.60, popularityRank: 900);
        var worse = Anime(2, "tv", malScore: 8.30, popularityRank: 10);

        var slot = TwoAlternativeSlot(better, worse);
        var memberById = new Dictionary<int, AnimeMetadata> { [better.Id] = better, [worse.Id] = worse };

        Assert.Equal(better.Id, SeriesVersionSlots.DefaultAlternativeId(slot, memberById));
    }

    [Fact]
    public void UnscoredAlternativesSortLast()
    {
        var scored = Anime(1, "tv", malScore: 7.0);
        var unscored = Anime(2, "tv", malScore: null);

        var slot = TwoAlternativeSlot(scored, unscored);
        var memberById = new Dictionary<int, AnimeMetadata> { [scored.Id] = scored, [unscored.Id] = unscored };

        Assert.Equal(scored.Id, SeriesVersionSlots.DefaultAlternativeId(slot, memberById));
    }

    [Fact]
    public void WithNothingWatchedNoScoresAndNoAiredDatesTheLowestMalIdWins()
    {
        var lower = Anime(1, "tv");
        var higher = Anime(2, "tv");

        var slot = TwoAlternativeSlot(higher, lower);
        var memberById = new Dictionary<int, AnimeMetadata> { [higher.Id] = higher, [lower.Id] = lower };

        Assert.Equal(lower.Id, SeriesVersionSlots.DefaultAlternativeId(slot, memberById));
    }

    [Fact]
    public void AnEarlierAiredDateWinsOverALaterOneWhenNeitherIsScoredOrWatched()
    {
        var earlier = Anime(2, "tv", airedFrom: new DateOnly(2006, 1, 1));
        var later = Anime(1, "tv", airedFrom: new DateOnly(2014, 1, 1)); // lower id, but airs later

        var slot = TwoAlternativeSlot(earlier, later);
        var memberById = new Dictionary<int, AnimeMetadata> { [earlier.Id] = earlier, [later.Id] = later };

        Assert.Equal(earlier.Id, SeriesVersionSlots.DefaultAlternativeId(slot, memberById));
    }
}
