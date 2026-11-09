using AnimeTracker.Api.Services.Series;

namespace AnimeTracker.Api.Tests.Services.Series;

// My series ranking (add-my-rank-badges design.md D4/D5, spec series-browser "The
// My average order is my series ranking"): the Series page's My average sort as a
// C# order, and each series' place in it. A copy of the client's compareMyScoreChain
// + sortSeries, so each step is pinned by a pair that ONLY that step separates.
// In every such pair the winner carries the HIGHER series id, so the final series-id
// step can never stand in for the step under test.
public class SeriesMyRankTests
{
    // Every figure the order reads is a parameter; the rest of the card is filler.
    private static SeriesListItemDto Item(
        int id, double? average = 8.0, double? averageRank = 10, int aired = 12, string title = "Title", string? english = null) =>
        new(id, title, english, PictureUrl: null, Status: "Finished", SeriesProgressBadge.None, BehindEpisodes: null,
            MalMain: new SeriesAverageDto(null, 0, 0),
            MineMain: new SeriesAverageDto(average, average is null ? 0 : 1, 1),
            MalRevealed: false, FirstYear: 2020, LastYear: 2020, MainLineEpisodeTotal: 12, HasUnknownEpisodeCounts: false,
            EntryCount: 1, MainLineWatchedEpisodes: 0, MainLineAiredEpisodes: aired, MainLineAiredCount: 1,
            MainLineAverageRank: averageRank, WatchedSeconds: 0);

    // The listed series in the order the My average sort gives them, by id, in both
    // input orders so the answer cannot come from the order the series arrived in.
    private static void AssertOrder(int[] expectedIds, params SeriesListItemDto[] items)
    {
        Assert.Equal(expectedIds, items.Order(SeriesMyRank.MyAverageOrder).Select(i => i.SeriesId));
        Assert.Equal(expectedIds, items.Reverse().Order(SeriesMyRank.MyAverageOrder).Select(i => i.SeriesId));
    }

    // --- one fact per step ---

    [Fact]
    public void TheHigherMyAverageComesFirst() =>
        AssertOrder([2, 1], Item(1, average: 8.0), Item(2, average: 8.5));

    [Fact]
    public void TheUnroundedAverageDecides() =>
        // 8.33 and 8.34 round to the same 8.3 on a card; the order reads the unrounded figures.
        AssertOrder([2, 1], Item(1, average: 8.33), Item(2, average: 8.34));

    [Fact]
    public void TheNearerAverageRankingPositionComesFirstWhenTheAveragesTie() =>
        AssertOrder([2, 1], Item(1, averageRank: 40), Item(2, averageRank: 12.5));

    [Fact]
    public void ASeriesWithAnAverageButNoRankedEntrySortsAfterOneThatHasAPosition() =>
        // Even a position far down the ranking outranks having none: nulls last, not "0".
        AssertOrder([2, 1], Item(1, averageRank: null), Item(2, averageRank: 500));

    [Fact]
    public void MoreMainLineEpisodesAiredComesFirstWhenAverageAndPositionTie() =>
        AssertOrder([2, 1], Item(1, aired: 12), Item(2, aired: 24));

    [Fact]
    public void TheDisplayTitleComesNextAndTheEnglishTitleIsPreferred() =>
        // 2's English title "Alpha" is the display title ("zzz" would sort last), 1 is "beta".
        AssertOrder([2, 1], Item(1, title: "beta"), Item(2, title: "zzz", english: "Alpha"));

    [Fact]
    public void ABlankEnglishTitleFallsBackToTheSeriesTitle() =>
        AssertOrder([2, 1], Item(1, title: "beta", english: "  "), Item(2, title: "alpha", english: "  "));

    [Fact]
    public void TheTitleIsComparedWithItsCaseIgnored() =>
        // Lowercased the two titles are equal, so the last step (series id) orders them;
        // compared as written, "apple" would sort before "Apple".
        AssertOrder([1, 2], Item(1, title: "Apple"), Item(2, title: "apple"));

    [Fact]
    public void TheTitleSortsAsLettersNotAsCharacterCodes() =>
        // "C item" < "b item" by character code, but "b item" < "c item" as the page's
        // lowercased title sort has it.
        AssertOrder([2, 1], Item(1, title: "C item"), Item(2, title: "b item"));

    [Fact]
    public void TheTitleIsComparedAsTheLanguageSortsItNotByCharacterCode() =>
        // The page's localeCompare puts an accented letter with its base letter: "été" sorts
        // before "zebra". By character code é (233) would sort after z (122).
        AssertOrder([2, 1], Item(1, title: "zebra"), Item(2, title: "été"));

    // --- precedence: an earlier step wins whatever the later ones say ---

    [Fact]
    public void AnEarlierStepWinsWhateverTheLaterStepsSay()
    {
        var bestAverageButEverythingElseWorse = Item(1, average: 9.0, averageRank: null, aired: 1, title: "zzz");
        var betterPosition = Item(2, average: 8.0, averageRank: 1, aired: 99, title: "aaa");
        var sameFiguresMoreAired = Item(3, average: 8.0, averageRank: 1, aired: 120, title: "zzz");
        var sameThroughAiredBetterTitle = Item(4, average: 8.0, averageRank: 1, aired: 120, title: "mmm");

        AssertOrder([1, 4, 3, 2], bestAverageButEverythingElseWorse, betterPosition, sameFiguresMoreAired, sameThroughAiredBetterTitle);
    }

    // --- the rank itself ---

    [Fact]
    public void RanksRunFromOneWithNoGapsEvenWhenAveragesTie()
    {
        var listed = Enumerable.Range(1, 12)
            .Select(id => Item(id, average: 8.0 + id % 3 * 0.5, averageRank: id % 4 == 0 ? null : id, aired: id % 5, title: $"Series {id}"))
            .ToList();

        var results = listed.Select(i => SeriesMyRank.Of(i.SeriesId, listed)).ToList();

        Assert.All(results, r => Assert.Equal(12, r.RankedCount));
        Assert.Equal(Enumerable.Range(1, 12), results.Select(r => r.Rank!.Value).Order());
    }

    [Fact]
    public void TheFirstSeriesOfTheOrderHoldsRankOne()
    {
        var listed = new[] { Item(1, average: 7), Item(2, average: 9), Item(3, average: 8) };

        Assert.Equal((1, 3), SeriesMyRank.Of(2, listed));
        Assert.Equal((2, 3), SeriesMyRank.Of(3, listed));
        Assert.Equal((3, 3), SeriesMyRank.Of(1, listed));
    }

    [Fact]
    public void ASeriesWithNoAverageHoldsNoRankAndIsNotCounted()
    {
        var listed = new[] { Item(1, average: 8), Item(2, average: null), Item(3, average: 7), Item(4, average: null) };

        Assert.Equal(((int?)null, 2), SeriesMyRank.Of(2, listed));
        Assert.Equal(((int?)null, 2), SeriesMyRank.Of(4, listed));
        // The two with an average are ranked 1 and 2 of 2: the unscored ones below them take no number.
        Assert.Equal((1, 2), SeriesMyRank.Of(1, listed));
        Assert.Equal((2, 2), SeriesMyRank.Of(3, listed));
    }

    [Fact]
    public void ASeriesTheListDoesNotHoldHoldsNoRank()
    {
        var listed = new[] { Item(1, average: 8), Item(2, average: 7) };

        Assert.Equal(((int?)null, 2), SeriesMyRank.Of(999, listed));
    }

    [Fact]
    public void NoListedSeriesRanksNothing() =>
        Assert.Equal(((int?)null, 0), SeriesMyRank.Of(1, []));

    [Fact]
    public void TheSameSeriesKeepsTheSameRankWhateverOrderTheListArrivesIn()
    {
        // Two series identical on every figure the order reads: only the series id tells them apart.
        var a = Item(1, title: "Same");
        var b = Item(2, title: "Same");

        Assert.Equal((1, 2), SeriesMyRank.Of(1, new[] { a, b }));
        Assert.Equal((1, 2), SeriesMyRank.Of(1, new[] { b, a }));
        Assert.Equal((2, 2), SeriesMyRank.Of(2, new[] { b, a }));
    }
}
