using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Recap;

namespace AnimeTracker.Api.Tests.Services.Recap;

// RecapRankingBuilder (tasks.md 4.1-4.5, design.md "Bayesian ranking of
// seasons and years"). globalMean (C) is passed straight in rather than
// derived from a synthetic whole list, since RecapService is what computes
// it from the real whole list — these tests exercise the weighting formula
// itself.
public class RecapRankingBuilderTests
{
    private static UserAnimeEntry Entry(int animeId, DateOnly airedFrom, int myScore) =>
        new()
        {
            AnimeId = animeId,
            Anime = new AnimeMetadata { Id = animeId, Title = $"Anime {animeId:D3}", AiredFrom = airedFrom },
            MyScore = myScore,
            Status = WatchStatus.Completed,
        };

    [Fact]
    public void AWellCoveredThinSeasonComparison_EightAnimeAveragingHigherOutranksOneAnimeScoringTen()
    {
        var thin = Entry(1, new DateOnly(2022, 1, 15), myScore: 10); // winter
        var wellCovered = Enumerable.Range(2, 8)
            .Select(id => Entry(id, new DateOnly(2022, 4, 15), myScore: id % 2 == 0 ? 8 : 9)) // spring, avg 8.5
            .ToList();
        var included = new List<UserAnimeEntry> { thin }.Concat(wellCovered).ToList();

        var ranking = RecapRankingBuilder.BuildSeasonRanking(included, RecapPeriod.Yearly(2022), globalMean: 7.4);

        Assert.Equal(2, ranking.Count);
        Assert.Equal("spring", ranking[0].Season);
        Assert.Equal("winter", ranking[1].Season);
    }

    [Fact]
    public void AWellCoveredSeasonSitsCloseToItsRawMean()
    {
        var wellCovered = Enumerable.Range(1, 45)
            .Select(id => Entry(id, new DateOnly(2022, 4, 15), myScore: 9))
            .ToList();

        var ranking = RecapRankingBuilder.BuildSeasonRanking(wellCovered, RecapPeriod.Yearly(2022), globalMean: 5.0);

        var spring = Assert.Single(ranking);
        Assert.True(Math.Abs(spring.WeightedScore - 9.0) <= 0.5, $"Expected close to 9.0, was {spring.WeightedScore}");
    }

    [Fact]
    public void TheSameSeasonScoresIdenticallyThroughAYearlyAndAMultiYearRecap()
    {
        var fall2019 = Enumerable.Range(1, 6)
            .Select(id => Entry(id, new DateOnly(2019, 11, 1), myScore: 7))
            .ToList();

        var viaYearly = RecapRankingBuilder.BuildSeasonRanking(fall2019, RecapPeriod.Yearly(2019), globalMean: 6.5);
        var viaMultiYear = RecapRankingBuilder.BuildSeasonRanking(fall2019, RecapPeriod.MultiYear(2011, 2020), globalMean: 6.5);

        var yearlyResult = Assert.Single(viaYearly);
        var multiYearResult = Assert.Single(viaMultiYear);
        Assert.Equal(yearlyResult.WeightedScore, multiYearResult.WeightedScore);
        Assert.Equal(yearlyResult.ScoredCount, multiYearResult.ScoredCount);
    }

    [Fact]
    public void ASeasonAndAYearBothHoldingTenScoredAnimeAreWeightedDifferently()
    {
        var tenInOneSeason = Enumerable.Range(1, 10)
            .Select(id => Entry(id, new DateOnly(2022, 4, 15), myScore: 9))
            .ToList();
        var period = RecapPeriod.Yearly(2022);

        var seasonRanking = RecapRankingBuilder.BuildSeasonRanking(tenInOneSeason, period, globalMean: 5.0);
        var yearRanking = RecapRankingBuilder.BuildYearRanking(tenInOneSeason, period, globalMean: 5.0);

        var season = Assert.Single(seasonRanking);
        var year = Assert.Single(yearRanking);
        Assert.Equal(10, season.ScoredCount);
        Assert.Equal(10, year.ScoredCount);
        Assert.NotEqual(season.WeightedScore, year.WeightedScore);
        // The year (m=20) isn't past its trust threshold at v=10 the way the
        // season (m=5) is, so its weighted score sits closer to the global
        // mean — pulled further away from the raw R=9.0 than the season's is.
        Assert.True(Math.Abs(year.WeightedScore - 5.0) < Math.Abs(season.WeightedScore - 5.0));
    }

    [Fact]
    public void SeasonsWithNoScoredAnimeAreOmitted()
    {
        var unscored = new UserAnimeEntry
        {
            AnimeId = 1,
            Anime = new AnimeMetadata { Id = 1, Title = "Anime 1", AiredFrom = new DateOnly(2022, 1, 15) },
            MyScore = null,
            Status = WatchStatus.Watching,
        };

        var ranking = RecapRankingBuilder.BuildSeasonRanking([unscored], RecapPeriod.Yearly(2022), globalMean: 5.0);

        Assert.Empty(ranking);
    }

    [Fact]
    public void OnlyTheLeaderCarriesPosters()
    {
        var winner = Enumerable.Range(1, 5).Select(id => Entry(id, new DateOnly(2022, 4, 15), myScore: 10)).ToList();
        var runnerUp = Enumerable.Range(6, 5).Select(id => Entry(id, new DateOnly(2022, 1, 15), myScore: 3)).ToList();
        var included = winner.Concat(runnerUp).ToList();

        var ranking = RecapRankingBuilder.BuildSeasonRanking(included, RecapPeriod.Yearly(2022), globalMean: 5.0);

        Assert.Equal(2, ranking.Count);
        Assert.Equal("spring", ranking[0].Season);
        Assert.NotEmpty(ranking[0].TopPosters);
        Assert.True(ranking[0].TopPosters.Count <= 3);
        Assert.Empty(ranking[1].TopPosters);
    }
}
