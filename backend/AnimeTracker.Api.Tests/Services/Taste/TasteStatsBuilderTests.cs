using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Ranking;
using AnimeTracker.Api.Services.Scheduling;
using AnimeTracker.Api.Services.Taste;

namespace AnimeTracker.Api.Tests.Services.Taste;

// TasteStatsBuilder (the taste-stats capability, add-genre-and-studio-stats task
// 1.6): one fact per scenario, using the spec's own numbers. The builder is pure.
// It takes the counted set, the whole list's mean score (C) and my ranking, so
// every fact builds exactly those and nothing else. Which entries are counted
// (Plan to watch out on the profile, the recap's included set on a recap) is the
// caller's rule, and is pinned where the callers are tested.
public class TasteStatsBuilderTests
{
    private static readonly AnimeRankingSnapshot NoRanking = AnimeRankingSnapshot.Empty;
    private static readonly IBroadcastLocalTimeConverter LocalTime = new BroadcastLocalTimeConverter();

    private static UserAnimeEntry Entry(
        int id, string[]? genres = null, string[]? studios = null, int? score = null,
        WatchStatus status = WatchStatus.Completed, int? popularity = null, string? source = null,
        DateOnly? airedFrom = null, string? title = null, int rewatchCount = 0) =>
        new()
        {
            AnimeId = id,
            Anime = new AnimeMetadata
            {
                Id = id,
                Title = title ?? $"Anime {id:D3}",
                PictureUrl = $"https://img.test/{id}.jpg",
                Genres = genres?.ToList(),
                Studios = studios?.ToList(),
                PopularityRank = popularity,
                Source = source,
                AiredFrom = airedFrom,
            },
            MyScore = score,
            Status = status,
            RewatchCount = rewatchCount,
        };

    // One entry per score, ids counting up from firstId, all carrying the same
    // genres and studios. A null score is an unscored entry.
    private static List<UserAnimeEntry> Run(int firstId, string[]? genres, string[]? studios, params int?[] scores) =>
        scores.Select((score, i) => Entry(firstId + i, genres, studios, score)).ToList();

    private static int?[] Repeat(int score, int times) => Enumerable.Repeat<int?>(score, times).ToArray();

    private static ActivityLog Log(int animeId, DateTimeOffset at, int from, int to) =>
        new()
        {
            AnimeId = animeId,
            Timestamp = at,
            ChangeType = ActivityChangeType.EpisodeIncremented,
            ChangeDetail = $"Episode {to}",
            PreviousEpisodesWatched = from,
        };

    private static DateTimeOffset NoonUtc(int year, int month, int day) => new(year, month, day, 12, 0, 0, TimeSpan.Zero);

    private static AnimeRankingSnapshot RankedInOrder(IReadOnlyList<UserAnimeEntry> entries, params int[] storedOrder) =>
        AnimeRankingSnapshot.Build(entries, storedOrder);

    // --- Weighted scores -------------------------------------------------------

    [Fact]
    public void AThinStudioIsPulledTowardTheWholeListMean()
    {
        // C = 7.25. Studio A has one anime scored 10, Studio B six averaging 8.5.
        List<UserAnimeEntry> counted = [.. Run(1, null, ["Studio A"], 10), .. Run(10, null, ["Studio B"], 8, 9, 8, 9, 8, 9)];

        var studios = TasteStatsBuilder.Studios(counted, wholeListMean: 7.25, NoRanking);

        var a = studios.Single(s => s.Name == "Studio A");
        var b = studios.Single(s => s.Name == "Studio B");
        Assert.Equal(7.71, a.WeightedScore);
        Assert.Equal(7.93, b.WeightedScore);
        Assert.Equal(1, b.ScoreRank);
        Assert.Equal(2, a.ScoreRank);
    }

    [Fact]
    public void TheFavouriteStudioIsTheBestLikedNotTheOneWithASingleTen()
    {
        // The list-recaps scenario: Studio A has four anime scored 9, 9, 8, 9, Studio B one scored 10.
        List<UserAnimeEntry> counted = [.. Run(1, null, ["Studio A"], 9, 9, 8, 9), .. Run(10, null, ["Studio B"], 10)];

        var studios = TasteStatsBuilder.Studios(counted, wholeListMean: 7.25, NoRanking);

        var first = studios.OrderBy(s => s.ScoreRank).First();
        Assert.Equal("Studio A", first.Name);
        Assert.Equal(7.92, first.WeightedScore);
    }

    [Fact]
    public void AGenreIsTrustedOnlyFromTenScoredAnime()
    {
        // Showbiz: three anime scored 10, 10, 9. Gore: 35 anime averaging 8.09 (three 9s and thirty-two 8s). C = 7.25.
        List<UserAnimeEntry> counted =
        [
            .. Run(1, ["Showbiz"], null, 10, 10, 9),
            .. Run(11, ["Gore"], null, [.. Repeat(9, 3), .. Repeat(8, 32)]),
        ];

        var genres = TasteStatsBuilder.Genres(counted, wholeListMean: 7.25, NoRanking);

        var gore = genres.Single(g => g.Name == "Gore");
        var showbiz = genres.Single(g => g.Name == "Showbiz");
        Assert.Equal(7.9, gore.WeightedScore);
        Assert.Equal(7.81, showbiz.WeightedScore);
        Assert.Equal(1, gore.ScoreRank);
        Assert.Equal(2, showbiz.ScoreRank);
    }

    [Fact]
    public void AStudioIsTrustedFromFiveScoredAnime()
    {
        // The same credits as the genres above, read as studios (m = 5): Showbiz's three
        // high scores carry it (8.16 against 7.98), which is why genres need the larger m.
        List<UserAnimeEntry> counted =
        [
            .. Run(1, null, ["Showbiz"], 10, 10, 9),
            .. Run(11, null, ["Gore"], [.. Repeat(9, 3), .. Repeat(8, 32)]),
        ];

        var studios = TasteStatsBuilder.Studios(counted, wholeListMean: 7.25, NoRanking);

        Assert.Equal(8.16, studios.Single(s => s.Name == "Showbiz").WeightedScore);
        Assert.Equal(7.98, studios.Single(s => s.Name == "Gore").WeightedScore);
        Assert.Equal(1, studios.Single(s => s.Name == "Showbiz").ScoreRank);
    }

    [Fact]
    public void TheMeanIsTheCallersWholeListMeanWhateverTheCountedSetAverages()
    {
        // The counted set averages 9, the whole list 7.25: every weighted score uses 7.25.
        List<UserAnimeEntry> counted = Run(1, ["Action"], ["Studio A"], 9, 9, 9, 9);

        var withListMean = TasteStatsBuilder.Genres(counted, wholeListMean: 7.25, NoRanking).Single();
        var withSetMean = TasteStatsBuilder.Genres(counted, wholeListMean: 9.0, NoRanking).Single();

        // v = 4, R = 9, m = 10: (4/14)*9 + (10/14)*C.
        Assert.Equal(7.75, withListMean.WeightedScore);
        Assert.Equal(9.0, withSetMean.WeightedScore);
    }

    [Fact]
    public void WithNothingScoredAnywhereThereIsNoWeightedScore()
    {
        List<UserAnimeEntry> counted = Run(1, ["Action"], ["Studio A"], null, null);

        var genre = TasteStatsBuilder.Genres(counted, wholeListMean: null, NoRanking).Single();
        var studio = TasteStatsBuilder.Studios(counted, wholeListMean: null, NoRanking).Single();

        Assert.Null(genre.WeightedScore);
        Assert.Null(genre.ScoreRank);
        Assert.Null(studio.WeightedScore);
        Assert.Null(studio.ScoreRank);
    }

    [Fact]
    public void NothingScoredInAStudioMeansACountAndNoWeightedScore()
    {
        // The list has scores elsewhere, so C exists; this studio's anime are all unscored.
        List<UserAnimeEntry> counted = Run(1, null, ["Studio A"], null, null, null);

        var studio = TasteStatsBuilder.Studios(counted, wholeListMean: 7.25, NoRanking).Single();

        Assert.Equal(3, studio.AnimeCount);
        Assert.Equal(0, studio.ScoredCount);
        Assert.Null(studio.WeightedScore); // not C, not zero
        Assert.Null(studio.ScoreRank);
    }

    // --- What counts, and as what ----------------------------------------------

    [Fact]
    public void CoProducedAnimeCountForBothStudios()
    {
        List<UserAnimeEntry> counted = [Entry(1, studios: ["Studio A", "Studio B"], score: 8), Entry(2, studios: ["Studio A"], score: 6)];

        var studios = TasteStatsBuilder.Studios(counted, wholeListMean: 7.0, NoRanking);

        Assert.Equal(2, studios.Single(s => s.Name == "Studio A").AnimeCount);
        Assert.Equal(1, studios.Single(s => s.Name == "Studio B").AnimeCount);
    }

    [Fact]
    public void AnAnimeCarryingSeveralGenresCountsOnceTowardEach()
    {
        List<UserAnimeEntry> counted = [Entry(1, genres: ["Action", "Fantasy"]), Entry(2, genres: ["Action"])];

        var genres = TasteStatsBuilder.Genres(counted, null, NoRanking);

        Assert.Equal(2, genres.Single(g => g.Name == "Action").AnimeCount);
        Assert.Equal(1, genres.Single(g => g.Name == "Fantasy").AnimeCount);
    }

    [Fact]
    public void ARewatchedAnimeCountsOnce()
    {
        // The counted set holds one entry per anime, so three rewatches add nothing.
        List<UserAnimeEntry> counted = [Entry(1, genres: ["Action"], studios: ["Studio A"], score: 8, rewatchCount: 3)];

        var action = TasteStatsBuilder.Genres(counted, 8.0, NoRanking).Single();
        var studio = TasteStatsBuilder.Studios(counted, 8.0, NoRanking).Single();

        Assert.Equal(1, action.AnimeCount);
        Assert.Equal(1, action.ScoredCount);
        Assert.Equal(1, studio.AnimeCount);
    }

    [Fact]
    public void AnAnimeWithoutGenresCountsInNoGenreFigureButStillCountsTowardItsStudios()
    {
        List<UserAnimeEntry> counted =
        [
            Entry(1, genres: null, studios: ["Studio A"], source: "manga"),
            Entry(2, genres: [], studios: ["Studio A"], source: "manga"),
        ];

        Assert.Empty(TasteStatsBuilder.Genres(counted, null, NoRanking));
        Assert.Empty(TasteStatsBuilder.Pairs(counted, null, limit: 5));
        Assert.Empty(TasteStatsBuilder.Trios(counted, null, limit: 5));
        Assert.Equal(2, TasteStatsBuilder.Studios(counted, null, NoRanking).Single().AnimeCount);
        Assert.Equal(2, TasteStatsBuilder.Sources(counted).Single().Count);
    }

    [Fact]
    public void ThemesCountAsGenresAndDemographicsOnlyInTheirOwnSplit()
    {
        List<UserAnimeEntry> counted = [Entry(1, genres: ["Action", "Gore", "Shounen"])];

        var genres = TasteStatsBuilder.Genres(counted, null, NoRanking);

        Assert.Equal(["Action", "Gore"], genres.Select(g => g.Name).OrderBy(n => n));
        Assert.DoesNotContain(genres, g => g.Name == "Shounen");
        var demographic = Assert.Single(TasteStatsBuilder.Demographics(counted));
        Assert.Equal(("Shounen", 1), (demographic.Label, demographic.Count));
    }

    [Fact]
    public void ShounenIsNotAFavouriteGenreHoweverManyCarryIt()
    {
        // Six anime carry Shounen, four Action and two Comedy: the favourite genre is Action.
        List<UserAnimeEntry> counted =
        [
            Entry(1, genres: ["Shounen", "Action"]), Entry(2, genres: ["Shounen", "Action"]),
            Entry(3, genres: ["Shounen", "Action"]), Entry(4, genres: ["Shounen", "Action"]),
            Entry(5, genres: ["Shounen", "Comedy"]), Entry(6, genres: ["Shounen", "Comedy"]),
        ];

        var genres = TasteStatsBuilder.Genres(counted, null, NoRanking);

        Assert.Equal("Action", genres[0].Name);
        Assert.DoesNotContain(genres, g => TasteNames.IsDemographic(g.Name));
        Assert.Equal(6, TasteStatsBuilder.Demographics(counted).Single(d => d.Label == "Shounen").Count);
    }

    [Theory]
    [InlineData("Shounen")]
    [InlineData("Seinen")]
    [InlineData("Shoujo")]
    [InlineData("Josei")]
    [InlineData("Kids")]
    public void EachOfTheFiveDemographicsIsKeptOutOfGenres(string demographic)
    {
        List<UserAnimeEntry> counted = [Entry(1, genres: [demographic, "Drama"]), Entry(2, genres: [demographic, "Drama"])];

        Assert.Equal("Drama", Assert.Single(TasteStatsBuilder.Genres(counted, null, NoRanking)).Name);
        Assert.Empty(TasteStatsBuilder.Pairs(counted, null, limit: 5)); // Drama alone is not a pair
        Assert.Equal(demographic, Assert.Single(TasteStatsBuilder.Demographics(counted)).Label);
    }

    [Fact]
    public void ANameMyAnimeListStoresInADifferentCaseIsNotADemographic()
    {
        // Names are used exactly as MyAnimeList stores them.
        List<UserAnimeEntry> counted = [Entry(1, genres: ["shounen"])];

        Assert.Equal("shounen", Assert.Single(TasteStatsBuilder.Genres(counted, null, NoRanking)).Name);
        Assert.Empty(TasteStatsBuilder.Demographics(counted));
    }

    // --- Most-watched order ------------------------------------------------------

    [Fact]
    public void TheMostCarriedGenreComesFirst()
    {
        List<UserAnimeEntry> counted =
        [
            .. Run(1, ["Fantasy"], null, null, null),
            .. Run(10, ["Action"], null, null, null, null),
        ];

        var genres = TasteStatsBuilder.Genres(counted, null, NoRanking);

        Assert.Equal(["Action", "Fantasy"], genres.Select(g => g.Name));
        Assert.Equal([1, 2], genres.Select(g => g.CountRank));
    }

    [Fact]
    public void EqualCountsFallToTheBetterLikedStudio()
    {
        // Both made four anime; the alphabetically later one is better liked.
        List<UserAnimeEntry> counted = [.. Run(1, null, ["Alpha"], 6, 6, 6, 6), .. Run(10, null, ["Zeta"], 9, 9, 9, 9)];

        var studios = TasteStatsBuilder.Studios(counted, wholeListMean: 7.0, NoRanking);

        Assert.Equal(["Zeta", "Alpha"], studios.Select(s => s.Name));
    }

    [Fact]
    public void AScoredStudioRanksAheadOfAnUnscoredOneAtTheSameCount()
    {
        List<UserAnimeEntry> counted = [Entry(1, studios: ["Alpha"]), Entry(2, studios: ["Zeta"], score: 4)];

        var studios = TasteStatsBuilder.Studios(counted, wholeListMean: 7.0, NoRanking);

        Assert.Equal(["Zeta", "Alpha"], studios.Select(s => s.Name));
    }

    [Fact]
    public void EqualCountsAndNoScoresFallToTheNameIgnoringCase()
    {
        List<UserAnimeEntry> counted =
        [
            Entry(1, genres: ["charlie"]), Entry(2, genres: ["Alpha"]), Entry(3, genres: ["bravo"]),
        ];

        var genres = TasteStatsBuilder.Genres(counted, null, NoRanking);

        Assert.Equal(["Alpha", "bravo", "charlie"], genres.Select(g => g.Name));
    }

    [Fact]
    public void TwoNamesDifferingOnlyInCaseStillOrderTheSameOnEveryLoad()
    {
        List<UserAnimeEntry> forward = [Entry(1, genres: ["ab"]), Entry(2, genres: ["AB"])];
        List<UserAnimeEntry> backward = [Entry(2, genres: ["AB"]), Entry(1, genres: ["ab"])];

        var first = TasteStatsBuilder.Genres(forward, null, NoRanking).Select(g => g.Name);
        var second = TasteStatsBuilder.Genres(backward, null, NoRanking).Select(g => g.Name);

        Assert.Equal(first, second);
    }

    [Fact]
    public void TheSameEntriesGiveTheSameOrderEveryTime()
    {
        List<UserAnimeEntry> counted = [.. Run(1, ["A", "B"], ["S1"], 8, 7), .. Run(10, ["B", "C"], ["S2"], 9, null)];

        var first = TasteStatsBuilder.Genres(counted, 7.5, NoRanking).Select(g => (g.Name, g.CountRank, g.ScoreRank)).ToList();
        var second = TasteStatsBuilder.Genres(counted, 7.5, NoRanking).Select(g => (g.Name, g.CountRank, g.ScoreRank)).ToList();

        Assert.Equal(first, second);
    }

    // --- Highest-rated order --------------------------------------------------------

    [Fact]
    public void AnUnscoredGenreIsLeftOutOfHighestRatedButStaysInMostWatched()
    {
        List<UserAnimeEntry> counted =
        [
            .. Run(1, ["Villainess"], null, null, null, null),
            .. Run(10, ["Action"], null, 8, 8),
            .. Run(20, ["Comedy"], null, 7),
        ];

        var genres = TasteStatsBuilder.Genres(counted, 7.5, NoRanking);

        Assert.Equal(3, genres.Count); // the most-watched list holds all three
        Assert.Null(genres.Single(g => g.Name == "Villainess").ScoreRank);
        Assert.Equal([1, 2], genres.Where(g => g.ScoreRank is not null).Select(g => g.ScoreRank!.Value).OrderBy(r => r));
    }

    [Fact]
    public void AnExactTieInWeightedScoreFallsToCoverage()
    {
        // C = 8 and every score is 8, so both weighted scores are exactly 8. The first
        // studio has 5 scored anime among 25, the second 15 scored among 15: coverage
        // puts the second first in highest rated though the first made more anime.
        List<UserAnimeEntry> counted =
        [
            .. Run(1, null, ["Few scored"], 8, 8, 8, 8, 8),
            .. Run(10, null, ["Few scored"], Enumerable.Repeat<int?>(null, 20).ToArray()),
            .. Run(100, null, ["Many scored"], Repeat(8, 15)),
        ];

        var studios = TasteStatsBuilder.Studios(counted, wholeListMean: 8.0, NoRanking);

        var few = studios.Single(s => s.Name == "Few scored");
        var many = studios.Single(s => s.Name == "Many scored");
        Assert.Equal(8.0, few.WeightedScore);
        Assert.Equal(8.0, many.WeightedScore);
        Assert.Equal(1, few.CountRank); // 25 anime against 15
        Assert.Equal(1, many.ScoreRank);
        Assert.Equal(2, few.ScoreRank);
    }

    [Fact]
    public void WhenScoredCountsAlsoTieTheScoreByScoreHistoryDecides()
    {
        // Both: two scored anime averaging 8, so identical weighted scores. One 10 beats none.
        List<UserAnimeEntry> counted = [.. Run(1, null, ["Even"], 8, 8), .. Run(10, null, ["Spread"], 10, 6)];

        var studios = TasteStatsBuilder.Studios(counted, wholeListMean: 7.0, NoRanking);

        Assert.Equal(studios.Single(s => s.Name == "Even").WeightedScore, studios.Single(s => s.Name == "Spread").WeightedScore);
        Assert.Equal(1, studios.Single(s => s.Name == "Spread").ScoreRank);
        Assert.Equal(2, studios.Single(s => s.Name == "Even").ScoreRank);
    }

    [Fact]
    public void AnExactTieInEverythingFallsToTheNameIgnoringCase()
    {
        List<UserAnimeEntry> counted = [.. Run(1, null, ["zeta"], 8, 8), .. Run(10, null, ["Alpha"], 8, 8)];

        var studios = TasteStatsBuilder.Studios(counted, wholeListMean: 7.0, NoRanking);

        Assert.Equal(1, studios.Single(s => s.Name == "Alpha").ScoreRank);
        Assert.Equal(2, studios.Single(s => s.Name == "zeta").ScoreRank);
    }

    [Fact]
    public void TheOrderComesFromTheFullPrecisionScoreNotTheRoundedOne()
    {
        // C = 6.425. One anime scored 10 weighs 7.0208; two scored 8 and 9 weigh 7.0179.
        // Both show as 7.02, and the one-anime studio is ahead only at full precision (a
        // rounded comparison would tie them and let the larger coverage put "Two" first).
        List<UserAnimeEntry> counted = [.. Run(1, null, ["One"], 10), .. Run(10, null, ["Two"], 8, 9)];

        var studios = TasteStatsBuilder.Studios(counted, wholeListMean: 6.425, NoRanking);

        Assert.Equal(7.02, studios.Single(s => s.Name == "One").WeightedScore);
        Assert.Equal(7.02, studios.Single(s => s.Name == "Two").WeightedScore);
        Assert.Equal(1, studios.Single(s => s.Name == "One").ScoreRank);
        Assert.Equal(2, studios.Single(s => s.Name == "Two").ScoreRank);
    }

    // --- Combinations -----------------------------------------------------------------

    [Fact]
    public void ThePairIWatchMostComesFirst()
    {
        List<UserAnimeEntry> counted =
        [
            .. Run(1, ["Action", "Fantasy"], null, null, null, null),
            .. Run(10, ["Adventure", "Fantasy"], null, null, null),
        ];

        var pairs = TasteStatsBuilder.Pairs(counted, null, limit: 5);

        Assert.Equal(["Action", "Fantasy"], pairs[0].Genres);
        Assert.Equal(3, pairs[0].AnimeCount);
        Assert.Equal(["Adventure", "Fantasy"], pairs[1].Genres);
        Assert.Equal(2, pairs[1].AnimeCount);
    }

    [Fact]
    public void ATrioCountsOnlyAnimeCarryingAllThree()
    {
        List<UserAnimeEntry> counted =
        [
            Entry(1, genres: ["Action", "Adventure", "Fantasy"]),
            Entry(2, genres: ["Action", "Adventure", "Fantasy"]),
            Entry(3, genres: ["Action", "Fantasy"]), // not Adventure
        ];

        var pair = TasteStatsBuilder.Pairs(counted, null, limit: 10).Single(p => p.Genres.SequenceEqual(["Action", "Fantasy"]));
        var trio = Assert.Single(TasteStatsBuilder.Trios(counted, null, limit: 10));

        Assert.Equal(3, pair.AnimeCount);
        Assert.Equal(["Action", "Adventure", "Fantasy"], trio.Genres);
        Assert.Equal(2, trio.AnimeCount);
    }

    [Fact]
    public void OneAnimeIsNotACombination()
    {
        // Exactly one carries both Sports and Horror; two carry Sports and Comedy.
        List<UserAnimeEntry> counted =
        [
            Entry(1, genres: ["Sports", "Horror"]),
            Entry(2, genres: ["Sports", "Comedy"]),
            Entry(3, genres: ["Sports", "Comedy"]),
        ];

        var pairs = TasteStatsBuilder.Pairs(counted, null, limit: 10);

        Assert.DoesNotContain(pairs, p => p.Genres.Contains("Horror"));
        Assert.Equal(["Comedy", "Sports"], Assert.Single(pairs).Genres);
    }

    [Fact]
    public void ADemographicIsNeverPartOfACombination()
    {
        // Five anime carry Action and Shounen, and three carry Action, Comedy and Shounen.
        List<UserAnimeEntry> counted =
        [
            .. Run(1, ["Action", "Shounen"], null, null, null, null, null),
            .. Run(10, ["Action", "Comedy", "Shounen"], null, null, null),
        ];

        var pairs = TasteStatsBuilder.Pairs(counted, null, limit: 10);
        var trios = TasteStatsBuilder.Trios(counted, null, limit: 10);

        Assert.DoesNotContain(pairs.Concat(trios), c => c.Genres.Any(TasteNames.IsDemographic));
        Assert.Equal(["Action", "Comedy"], Assert.Single(pairs).Genres);
        Assert.Empty(trios); // only the pair of non-demographic genres remains
    }

    [Fact]
    public void ACombinationNamesItsGenresAlphabeticallyWhateverOrderAnAnimeListsThem()
    {
        List<UserAnimeEntry> counted = [Entry(1, genres: ["Fantasy", "Action"]), Entry(2, genres: ["Action", "Fantasy"])];

        var pair = Assert.Single(TasteStatsBuilder.Pairs(counted, null, limit: 5));

        Assert.Equal(["Action", "Fantasy"], pair.Genres);
        Assert.Equal(2, pair.AnimeCount);
    }

    [Fact]
    public void CombinationsWithTheSameCountFallToTheBetterLikedThenTheNames()
    {
        List<UserAnimeEntry> counted =
        [
            .. Run(1, ["Alpha", "Beta"], null, 5, 5),
            .. Run(10, ["Gamma", "Delta"], null, 9, 9),
            .. Run(20, ["Epsilon", "Zeta"], null, 9, 9), // same score as Delta + Gamma: the names decide
        ];

        var pairs = TasteStatsBuilder.Pairs(counted, wholeListMean: 7.0, limit: 5);

        Assert.Equal(
            [new[] { "Delta", "Gamma" }, new[] { "Epsilon", "Zeta" }, new[] { "Alpha", "Beta" }],
            pairs.Select(p => p.Genres.ToArray()));
    }

    [Fact]
    public void PairsAndTriosStopAtTheLimit()
    {
        // Six genres carried together by two anime give 15 pairs and 20 trios.
        string[] genres = ["A", "B", "C", "D", "E", "F"];
        List<UserAnimeEntry> counted = [Entry(1, genres: genres), Entry(2, genres: genres)];

        Assert.Equal(5, TasteStatsBuilder.Pairs(counted, null, limit: 5).Count);
        Assert.Equal(5, TasteStatsBuilder.Trios(counted, null, limit: 5).Count);
        Assert.Equal(15, TasteStatsBuilder.Pairs(counted, null, limit: 100).Count);
        Assert.Equal(20, TasteStatsBuilder.Trios(counted, null, limit: 100).Count);
    }

    // --- Hidden gems ----------------------------------------------------------------------

    [Fact]
    public void TheLeastPopularFavouriteComesFirst()
    {
        List<UserAnimeEntry> counted =
        [
            Entry(1, score: 8, popularity: 16825, title: "Insomnia"),
            Entry(2, score: 8, popularity: 22218, title: "Ghost Avenue"),
        ];

        var gems = TasteStatsBuilder.HiddenGems(counted, NoRanking, limit: 10);

        Assert.Equal(["Ghost Avenue", "Insomnia"], gems.Select(g => g.Title));
        Assert.Equal(22218, gems[0].PopularityRank);
        Assert.Equal(8, gems[0].MyScore);
    }

    [Fact]
    public void APopularAnimeIsNotHiddenHoweverHighItIsScored()
    {
        List<UserAnimeEntry> counted = [Entry(1, score: 10, popularity: 150), Entry(2, score: 10, popularity: 1000)];

        Assert.Empty(TasteStatsBuilder.HiddenGems(counted, NoRanking, limit: 10));
    }

    [Fact]
    public void TheFloorIsAboveTheThousandMostPopular()
    {
        List<UserAnimeEntry> counted = [Entry(1, score: 9, popularity: 1000), Entry(2, score: 9, popularity: 1001)];

        var gem = Assert.Single(TasteStatsBuilder.HiddenGems(counted, NoRanking, limit: 10));

        Assert.Equal(2, gem.AnimeId);
    }

    [Fact]
    public void BelowEightDoesNotQualify()
    {
        List<UserAnimeEntry> counted = [Entry(1, score: 7, popularity: 20000), Entry(2, score: 8, popularity: 20000)];

        Assert.Equal(2, Assert.Single(TasteStatsBuilder.HiddenGems(counted, NoRanking, limit: 10)).AnimeId);
    }

    [Fact]
    public void AnAnimeWithNoPopularityRankOrNoScoreDoesNotQualify()
    {
        List<UserAnimeEntry> counted = [Entry(1, score: 10, popularity: null), Entry(2, score: null, popularity: 5000)];

        Assert.Empty(TasteStatsBuilder.HiddenGems(counted, NoRanking, limit: 10));
    }

    [Fact]
    public void GemsOfEqualPopularityFallToMyScoreThenMyRankingThenTheTitle()
    {
        List<UserAnimeEntry> counted =
        [
            Entry(1, score: 8, popularity: 5000, title: "Zulu"),
            Entry(2, score: 10, popularity: 5000, title: "Yankee"),
            Entry(3, score: 8, popularity: 5000, title: "X-ray"),
            Entry(4, score: 8, popularity: 5000, title: "Whiskey"),
            Entry(5, score: 8, popularity: 5000, title: "Victor"),
        ];
        // My ranking puts the 10 first and then the 8s as stored: Zulu, X-ray, Victor,
        // Whiskey. That is not their title order (Victor, Whiskey, X-ray, Zulu), so the
        // ranking, not the title, must be what separates them.
        var snapshot = RankedInOrder(counted, 2, 1, 3, 5, 4);

        var gems = TasteStatsBuilder.HiddenGems(counted, snapshot, limit: 10);

        Assert.Equal([2, 1, 3, 5, 4], gems.Select(g => g.AnimeId));
    }

    [Fact]
    public void GemsLeftUnrankedByMyRankingFallToTheTitle()
    {
        // Plan to watch is never ranked, so only the title can separate these two.
        List<UserAnimeEntry> counted =
        [
            Entry(1, score: 9, popularity: 5000, title: "Beta", status: WatchStatus.PlanToWatch),
            Entry(2, score: 9, popularity: 5000, title: "alpha", status: WatchStatus.PlanToWatch),
        ];

        var gems = TasteStatsBuilder.HiddenGems(counted, RankedInOrder(counted), limit: 10);

        Assert.Equal(["alpha", "Beta"], gems.Select(g => g.Title));
    }

    [Fact]
    public void GemsStopAtTheLimit()
    {
        List<UserAnimeEntry> counted = Enumerable.Range(1, 30).Select(i => Entry(i, score: 9, popularity: 2000 + i)).ToList();

        var gems = TasteStatsBuilder.HiddenGems(counted, NoRanking, limit: 10);

        Assert.Equal(10, gems.Count);
        Assert.Equal(2030, gems[0].PopularityRank); // still the least popular ten
    }

    // --- Splits -----------------------------------------------------------------------------

    [Fact]
    public void SourcesMergeAfterTheFourthLargest()
    {
        // Eleven kinds of source: counts 11, 10, 9, 8 are named; the seven others (1 each) merge.
        List<UserAnimeEntry> counted = [];
        var id = 1;
        string[] kinds = ["manga", "original", "light_novel", "game", "visual_novel", "novel", "web_manga", "4_koma_manga", "book", "card_game", "music"];
        int[] sizes = [11, 10, 9, 8, 1, 1, 1, 1, 1, 1, 1];
        for (var k = 0; k < kinds.Length; k++)
            for (var n = 0; n < sizes[k]; n++)
                counted.Add(Entry(id++, source: kinds[k]));

        var split = TasteStatsBuilder.Sources(counted);

        Assert.Equal(
            [("manga", 11), ("original", 10), ("light_novel", 9), ("game", 8), ("other", 7)],
            split.Select(s => (s.Label, s.Count)));
    }

    [Fact]
    public void MyAnimeListsOwnOtherSourceJoinsTheMergedCategoryInsteadOfListingTwoOthers()
    {
        // MyAnimeList's own "other" is carried by three anime, as many as light novels. Left as a
        // category of its own it would take the fourth place and, with the one smaller kind merged
        // after it, put two different things called "other" in one legend. Folded in, the legend
        // names the four largest real kinds and one Other holding MyAnimeList's "other".
        List<UserAnimeEntry> counted = [];
        var id = 1;
        foreach (var (source, count) in new[] { ("manga", 5), ("original", 4), ("light_novel", 3), ("other", 3), ("game", 2) })
            for (var n = 0; n < count; n++)
                counted.Add(Entry(id++, source: source));

        var split = TasteStatsBuilder.Sources(counted);

        Assert.Equal(
            [("manga", 5), ("original", 4), ("light_novel", 3), ("game", 2), ("other", 3)],
            split.Select(s => (s.Label, s.Count)));
        Assert.Single(split, s => s.Label == "other");
    }

    [Fact]
    public void SharesAddUpToTheWholeOfTheSplit()
    {
        // 206 Shounen, 64 Seinen, 7 Shoujo, 4 Kids and 1 Josei: shares are over 282.
        List<UserAnimeEntry> counted = [];
        var id = 1;
        foreach (var (name, count) in new[] { ("Shounen", 206), ("Seinen", 64), ("Shoujo", 7), ("Kids", 4), ("Josei", 1) })
            for (var n = 0; n < count; n++)
                counted.Add(Entry(id++, genres: [name]));

        var split = TasteStatsBuilder.Demographics(counted);

        Assert.Equal([("Shounen", 206), ("Seinen", 64), ("Shoujo", 7), ("Kids", 4), ("other", 1)], split.Select(s => (s.Label, s.Count)));
        Assert.Equal(282, split.Sum(s => s.Count));
    }

    [Fact]
    public void AnimeWithoutADemographicTakeNoPart()
    {
        List<UserAnimeEntry> counted = [Entry(1, genres: ["Shounen"]), Entry(2, genres: ["Action"]), Entry(3, genres: null)];

        var split = TasteStatsBuilder.Demographics(counted);

        Assert.Equal([("Shounen", 1)], split.Select(s => (s.Label, s.Count)));
    }

    [Fact]
    public void AnAnimeCarryingTwoDemographicsCountsTowardBoth()
    {
        List<UserAnimeEntry> counted = [Entry(1, genres: ["Shounen", "Seinen"])];

        var split = TasteStatsBuilder.Demographics(counted);

        Assert.Equal(2, split.Count);
        Assert.All(split, s => Assert.Equal(1, s.Count));
    }

    [Fact]
    public void AnAnimeWithNoSourceTakesNoPart()
    {
        List<UserAnimeEntry> counted = [Entry(1, source: "manga"), Entry(2, source: null), Entry(3, source: " ")];

        Assert.Equal([("manga", 1)], TasteStatsBuilder.Sources(counted).Select(s => (s.Label, s.Count)));
    }

    [Fact]
    public void ErasAreDecadesAndATieGoesToTheMoreRecentOne()
    {
        List<UserAnimeEntry> counted =
        [
            Entry(1, airedFrom: new DateOnly(2010, 1, 5)), Entry(2, airedFrom: new DateOnly(2019, 12, 31)),
            Entry(3, airedFrom: new DateOnly(2020, 1, 1)), Entry(4, airedFrom: new DateOnly(2021, 6, 1)),
            Entry(5, airedFrom: new DateOnly(1999, 4, 3)), // alone in the 1990s
            Entry(6, airedFrom: null), // no air date: takes no part
        ];

        var split = TasteStatsBuilder.Eras(counted);

        // 2020s and 2010s tie on two each: the more recent decade comes first.
        Assert.Equal([("2020s", 2), ("2010s", 2), ("1990s", 1)], split.Select(s => (s.Label, s.Count)));
    }

    [Fact]
    public void AnEmptySplitHasNothingToShow()
    {
        List<UserAnimeEntry> counted = [Entry(1)];

        Assert.Empty(TasteStatsBuilder.Demographics(counted));
        Assert.Empty(TasteStatsBuilder.Sources(counted));
        Assert.Empty(TasteStatsBuilder.Eras(counted));
    }

    // --- Posters ---------------------------------------------------------------------------------

    [Fact]
    public void MyFavouritesIllustrateTheStudioBestRankedFirst()
    {
        // Five anime scored alike: my stored order places 5, 3, 1, 2, 4.
        List<UserAnimeEntry> counted = Run(1, null, ["Studio A"], 8, 8, 8, 8, 8);
        var snapshot = RankedInOrder(counted, 5, 3, 1, 2, 4);

        var studio = TasteStatsBuilder.Studios(counted, 8.0, snapshot).Single();

        Assert.Equal([5, 3, 1], studio.Posters.Select(p => p.AnimeId));
    }

    [Fact]
    public void UnrankedAnimeComeAfterEveryRankedOneByTitle()
    {
        // Two ranked anime and three unscored (so unranked) ones titled Zeta, Alpha and Mid.
        List<UserAnimeEntry> counted =
        [
            Entry(1, genres: ["Action"], score: 8),
            Entry(2, genres: ["Action"], score: 8),
            Entry(10, genres: ["Action"], title: "Zeta"),
            Entry(11, genres: ["Action"], title: "Alpha"),
            Entry(12, genres: ["Action"], title: "Mid"),
        ];
        var snapshot = RankedInOrder(counted, 2, 1);

        var action = TasteStatsBuilder.Genres(counted, 8.0, snapshot).Single();

        Assert.Equal([2, 1, 11], action.Posters.Select(p => p.AnimeId));
    }

    [Fact]
    public void ARowWithFewerThanThreeAnimeShowsWhatItHas()
    {
        List<UserAnimeEntry> counted = [Entry(1, genres: ["Action"], score: 8)];

        var action = TasteStatsBuilder.Genres(counted, 8.0, RankedInOrder(counted, 1)).Single();

        var poster = Assert.Single(action.Posters);
        Assert.Equal((1, "https://img.test/1.jpg"), (poster.AnimeId, poster.PictureUrl));
    }

    // --- The biggest day -----------------------------------------------------------------------------

    [Fact]
    public void TheBusiestDayWinsWithTheEpisodesOfEveryAnimeThatDay()
    {
        // 15 September: 20 + 15 + 10 + 6 = 51 episodes across four anime. 3 October holds 30.
        List<UserAnimeEntry> counted = Enumerable.Range(1, 5).Select(i => Entry(i, score: 8)).ToList();
        List<ActivityLog> log =
        [
            Log(1, NoonUtc(2026, 9, 15), 0, 20), Log(2, NoonUtc(2026, 9, 15), 0, 15),
            Log(3, NoonUtc(2026, 9, 15), 0, 10), Log(4, NoonUtc(2026, 9, 15), 0, 6),
            Log(5, NoonUtc(2026, 10, 3), 0, 30),
        ];

        var day = TasteStatsBuilder.BiggestDay(log, counted, LocalTime, NoRanking);

        Assert.NotNull(day);
        Assert.Equal(new DateOnly(2026, 9, 15), day.Date);
        Assert.Equal(51, day.Episodes);
        Assert.Equal([1, 2, 3], day.Posters.Select(p => p.AnimeId)); // most episodes that day first, three at most
    }

    [Fact]
    public void RowsOfTheSameAnimeOnOneDaySumUp()
    {
        List<UserAnimeEntry> counted = [Entry(1), Entry(2)];
        List<ActivityLog> log =
        [
            Log(1, NoonUtc(2026, 3, 2).AddHours(-3), 0, 4), Log(1, NoonUtc(2026, 3, 2), 4, 9), // 9 on one day
            Log(2, NoonUtc(2026, 3, 9), 0, 8),
        ];

        var day = TasteStatsBuilder.BiggestDay(log, counted, LocalTime, NoRanking);

        Assert.Equal((new DateOnly(2026, 3, 2), 9), (day!.Date, day.Episodes));
    }

    [Fact]
    public void ACorrectionSubtractsNothing()
    {
        // I lowered an anime's count by 3 on a day I also watched 10 episodes: the day is 10.
        List<UserAnimeEntry> counted = [Entry(1), Entry(2)];
        List<ActivityLog> log = [Log(1, NoonUtc(2026, 5, 4), 0, 10), Log(2, NoonUtc(2026, 5, 4), 8, 5)];

        var day = TasteStatsBuilder.BiggestDay(log, counted, LocalTime, NoRanking);

        Assert.Equal(10, day!.Episodes);
        Assert.Equal([1], day.Posters.Select(p => p.AnimeId)); // the decrease puts anime 2 nowhere
    }

    [Fact]
    public void ATieGoesToTheEarlierDay()
    {
        List<UserAnimeEntry> counted = [Entry(1), Entry(2)];
        List<ActivityLog> log = [Log(2, NoonUtc(2026, 3, 9), 0, 20), Log(1, NoonUtc(2026, 3, 2), 0, 20)];

        var day = TasteStatsBuilder.BiggestDay(log, counted, LocalTime, NoRanking);

        Assert.Equal(new DateOnly(2026, 3, 2), day!.Date);
    }

    [Fact]
    public void ARowAtHalfPastElevenUtcBelongsToTheNextLocalDay()
    {
        // 14 September holds 5 episodes at ten in the morning UTC; 8 more are logged at 23:30 UTC,
        // which is 02:30 on the 15th in Helsinki. By UTC dates the 14th would hold 13 and win.
        List<UserAnimeEntry> counted = [Entry(1), Entry(2)];
        List<ActivityLog> log =
        [
            Log(1, new DateTimeOffset(2026, 9, 14, 10, 0, 0, TimeSpan.Zero), 0, 5),
            Log(2, new DateTimeOffset(2026, 9, 14, 23, 30, 0, TimeSpan.Zero), 0, 8),
        ];

        var day = TasteStatsBuilder.BiggestDay(log, counted, LocalTime, NoRanking);

        Assert.Equal((new DateOnly(2026, 9, 15), 8), (day!.Date, day.Episodes));
    }

    [Fact]
    public void OnlyAnimeOfTheCountedSetAddToADay()
    {
        List<UserAnimeEntry> counted = [Entry(1)];
        List<ActivityLog> log = [Log(1, NoonUtc(2026, 4, 1), 0, 3), Log(99, NoonUtc(2026, 4, 2), 0, 40)];

        var day = TasteStatsBuilder.BiggestDay(log, counted, LocalTime, NoRanking);

        Assert.Equal((new DateOnly(2026, 4, 1), 3), (day!.Date, day.Episodes));
    }

    [Fact]
    public void WithNoPositiveIncreaseThePeriodHasNoBiggestDay()
    {
        List<UserAnimeEntry> counted = [Entry(1)];

        Assert.Null(TasteStatsBuilder.BiggestDay([], counted, LocalTime, NoRanking));
        Assert.Null(TasteStatsBuilder.BiggestDay([Log(1, NoonUtc(2026, 4, 1), 6, 2)], counted, LocalTime, NoRanking));
        Assert.Null(TasteStatsBuilder.BiggestDay([Log(1, NoonUtc(2026, 4, 1), 5, 5)], counted, LocalTime, NoRanking));
    }

    [Fact]
    public void ARowWhoseEpisodeNumberCannotBeReadCountsForNothing()
    {
        List<UserAnimeEntry> counted = [Entry(1)];
        var unreadable = Log(1, NoonUtc(2026, 4, 1), 0, 5);
        unreadable.ChangeDetail = "Watched something";
        var noPrevious = Log(1, NoonUtc(2026, 4, 2), 0, 5);
        noPrevious.PreviousEpisodesWatched = null;

        Assert.Null(TasteStatsBuilder.BiggestDay([unreadable, noPrevious], counted, LocalTime, NoRanking));
    }

    [Fact]
    public void TheDaysAnimeAreOrderedByEpisodesThatDayThenByMyRanking()
    {
        // C has the most episodes that day; A, B and D watched 5 each, and my ranking puts D above A above B.
        List<UserAnimeEntry> counted =
        [
            Entry(1, score: 8, title: "A"), Entry(2, score: 8, title: "B"), Entry(3, score: 8, title: "C"), Entry(4, score: 8, title: "D"),
        ];
        var snapshot = RankedInOrder(counted, 4, 1, 3, 2);
        List<ActivityLog> log =
        [
            Log(1, NoonUtc(2026, 6, 1), 0, 5), Log(2, NoonUtc(2026, 6, 1), 0, 5),
            Log(3, NoonUtc(2026, 6, 1), 0, 9), Log(4, NoonUtc(2026, 6, 1), 0, 5),
        ];

        var day = TasteStatsBuilder.BiggestDay(log, counted, LocalTime, snapshot);

        Assert.Equal([3, 4, 1], day!.Posters.Select(p => p.AnimeId));
        Assert.Equal(24, day.Episodes);
    }
}
