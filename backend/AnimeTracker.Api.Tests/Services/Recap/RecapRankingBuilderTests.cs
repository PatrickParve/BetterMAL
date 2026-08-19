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

    private static UserAnimeEntry WatchedEntry(
        int animeId, DateOnly airedFrom, int episodesWatched, int durationSeconds, int? myScore = null) =>
        new()
        {
            AnimeId = animeId,
            Anime = new AnimeMetadata
            {
                Id = animeId,
                Title = $"Anime {animeId:D3}",
                AiredFrom = airedFrom,
                AverageEpisodeDurationSeconds = durationSeconds,
            },
            EpisodesWatched = episodesWatched,
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
    public void EveryRankedRowCarriesPosters()
    {
        var winner = Enumerable.Range(1, 5).Select(id => Entry(id, new DateOnly(2022, 4, 15), myScore: 10)).ToList();
        var runnerUp = Enumerable.Range(6, 5).Select(id => Entry(id, new DateOnly(2022, 1, 15), myScore: 3)).ToList();
        var included = winner.Concat(runnerUp).ToList();

        var ranking = RecapRankingBuilder.BuildSeasonRanking(included, RecapPeriod.Yearly(2022), globalMean: 5.0);

        Assert.Equal(2, ranking.Count);
        Assert.Equal("spring", ranking[0].Season);
        Assert.NotEmpty(ranking[0].TopPosters);
        Assert.True(ranking[0].TopPosters.Count <= 3);
        Assert.Equal("winter", ranking[1].Season);
        Assert.NotEmpty(ranking[1].TopPosters);
        Assert.True(ranking[1].TopPosters.Count <= 3);
    }

    // BuildSeasonTimeRanking / BuildYearTimeRanking (tasks.md 2.3-2.8,
    // design.md decisions 7-8).
    [Fact]
    public void SeasonsAreOrderedByTimeWatchedNotByScore()
    {
        var lessTimeHigherScore = WatchedEntry(1, new DateOnly(2022, 1, 15), episodesWatched: 2, durationSeconds: 1200, myScore: 10);
        var moreTimeLowerScore = WatchedEntry(2, new DateOnly(2022, 4, 15), episodesWatched: 20, durationSeconds: 1200, myScore: 5);
        var included = new List<UserAnimeEntry> { lessTimeHigherScore, moreTimeLowerScore };

        var ranking = RecapRankingBuilder.BuildSeasonTimeRanking(included, RecapPeriod.Yearly(2022));

        Assert.Equal(2, ranking.Count);
        Assert.Equal("spring", ranking[0].Season);
        Assert.Equal("winter", ranking[1].Season);
    }

    [Fact]
    public void AGroupWithUnscoredAnimeIsStillRankedByTime()
    {
        var unscored = WatchedEntry(1, new DateOnly(2021, 7, 15), episodesWatched: 12, durationSeconds: 1500, myScore: null);

        var ranking = RecapRankingBuilder.BuildSeasonTimeRanking([unscored], RecapPeriod.Yearly(2021));

        var summer = Assert.Single(ranking);
        Assert.Equal("summer", summer.Season);
        Assert.Equal(12, summer.EpisodesWatched);
    }

    [Fact]
    public void AGroupWithZeroEpisodesWatchedIsOmitted()
    {
        var nothingWatched = WatchedEntry(1, new DateOnly(2022, 1, 15), episodesWatched: 0, durationSeconds: 1200, myScore: 8);

        var ranking = RecapRankingBuilder.BuildSeasonTimeRanking([nothingWatched], RecapPeriod.Yearly(2022));

        Assert.Empty(ranking);
    }

    [Fact]
    public void NoSeasonTimeRankingRowCarriesPosters()
    {
        var leaderPick = WatchedEntry(1, new DateOnly(2022, 4, 15), episodesWatched: 24, durationSeconds: 1200, myScore: 3);
        var leaderOther = WatchedEntry(2, new DateOnly(2022, 4, 15), episodesWatched: 2, durationSeconds: 1200, myScore: 10);
        var runnerUp = WatchedEntry(3, new DateOnly(2022, 1, 15), episodesWatched: 5, durationSeconds: 1200, myScore: 9);
        var included = new List<UserAnimeEntry> { leaderPick, leaderOther, runnerUp };

        var ranking = RecapRankingBuilder.BuildSeasonTimeRanking(included, RecapPeriod.Yearly(2022));

        Assert.Equal(2, ranking.Count);
        Assert.Equal("spring", ranking[0].Season);
        Assert.Empty(ranking[0].TopPosters);
        Assert.Empty(ranking[1].TopPosters);
    }

    [Fact]
    public void OnlyTheLeadingYearTimeRowCarriesPostersChosenByEpisodesWatched()
    {
        var leaderPick = WatchedEntry(1, new DateOnly(2022, 4, 15), episodesWatched: 24, durationSeconds: 1200, myScore: 3);
        var leaderOther = WatchedEntry(2, new DateOnly(2022, 4, 15), episodesWatched: 2, durationSeconds: 1200, myScore: 10);
        var runnerUp = WatchedEntry(3, new DateOnly(2021, 1, 15), episodesWatched: 5, durationSeconds: 1200, myScore: 9);
        var included = new List<UserAnimeEntry> { leaderPick, leaderOther, runnerUp };

        var ranking = RecapRankingBuilder.BuildYearTimeRanking(included, RecapPeriod.MultiYear(2021, 2022));

        Assert.Equal(2, ranking.Count);
        Assert.Equal(2022, ranking[0].Year);
        Assert.NotEmpty(ranking[0].TopPosters);
        Assert.Equal(leaderPick.AnimeId, ranking[0].TopPosters[0].AnimeId);
        Assert.Empty(ranking[1].TopPosters);
    }

    [Fact]
    public void PerGroupTimeSumsToTheStatBlocksTimeSpent()
    {
        var winter = WatchedEntry(1, new DateOnly(2022, 1, 15), episodesWatched: 12, durationSeconds: 1400);
        var spring = WatchedEntry(2, new DateOnly(2022, 4, 15), episodesWatched: 6, durationSeconds: 1500);
        var included = new List<UserAnimeEntry> { winter, spring };

        var ranking = RecapRankingBuilder.BuildSeasonTimeRanking(included, RecapPeriod.Yearly(2022));
        var stats = RecapStatsBuilder.Build(included, included);

        Assert.Equal(stats.TimeSpentSeconds, ranking.Sum(r => r.TimeSpentSeconds));
    }

    [Fact]
    public void YearTimeRankingLeavesSeasonNull()
    {
        var entry = WatchedEntry(1, new DateOnly(2022, 4, 15), episodesWatched: 12, durationSeconds: 1400);

        var ranking = RecapRankingBuilder.BuildYearTimeRanking([entry], RecapPeriod.MultiYear(2020, 2023));

        var year = Assert.Single(ranking);
        Assert.Equal(2022, year.Year);
        Assert.Null(year.Season);
    }
}
