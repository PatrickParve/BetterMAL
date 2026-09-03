using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Ranking;
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

    // A group of scored entries sharing one air date, one per element of
    // `scores` — lets the extended-tie-break tests below spell out an exact
    // histogram without a separate call per anime.
    private static List<UserAnimeEntry> Group(int idStart, DateOnly airedFrom, params int[] scores) =>
        scores.Select((score, i) => Entry(idStart + i, airedFrom, score)).ToList();

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

        var ranking = RecapRankingBuilder.BuildSeasonRanking(included, RecapPeriod.Yearly(2022), globalMean: 7.4, rankingSnapshot: AnimeRankingSnapshot.Empty);

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

        var ranking = RecapRankingBuilder.BuildSeasonRanking(wellCovered, RecapPeriod.Yearly(2022), globalMean: 5.0, rankingSnapshot: AnimeRankingSnapshot.Empty);

        var spring = Assert.Single(ranking);
        Assert.True(Math.Abs(spring.WeightedScore - 9.0) <= 0.5, $"Expected close to 9.0, was {spring.WeightedScore}");
    }

    [Fact]
    public void TheSameSeasonScoresIdenticallyThroughAYearlyAndAMultiYearRecap()
    {
        var fall2019 = Enumerable.Range(1, 6)
            .Select(id => Entry(id, new DateOnly(2019, 11, 1), myScore: 7))
            .ToList();

        var viaYearly = RecapRankingBuilder.BuildSeasonRanking(fall2019, RecapPeriod.Yearly(2019), globalMean: 6.5, rankingSnapshot: AnimeRankingSnapshot.Empty);
        var viaMultiYear = RecapRankingBuilder.BuildSeasonRanking(fall2019, RecapPeriod.MultiYear(2011, 2020), globalMean: 6.5, rankingSnapshot: AnimeRankingSnapshot.Empty);

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

        var seasonRanking = RecapRankingBuilder.BuildSeasonRanking(tenInOneSeason, period, globalMean: 5.0, rankingSnapshot: AnimeRankingSnapshot.Empty);
        var yearRanking = RecapRankingBuilder.BuildYearRanking(tenInOneSeason, period, globalMean: 5.0, rankingSnapshot: AnimeRankingSnapshot.Empty);

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

        var ranking = RecapRankingBuilder.BuildSeasonRanking([unscored], RecapPeriod.Yearly(2022), globalMean: 5.0, rankingSnapshot: AnimeRankingSnapshot.Empty);

        Assert.Empty(ranking);
    }

    // design.md D2/tasks.md 4.2: every ranked row has at least one bucket, no
    // bucket holds more than three, and reading three from the top of the
    // buckets — the same rule describeSeasonRanking/postersFor use on the
    // client for the All case — yields the group's three best.
    [Fact]
    public void EveryRankedRowCarriesPostersByScoreBuckets()
    {
        var winner = Enumerable.Range(1, 5).Select(id => Entry(id, new DateOnly(2022, 4, 15), myScore: 10)).ToList();
        var runnerUp = Enumerable.Range(6, 5).Select(id => Entry(id, new DateOnly(2022, 1, 15), myScore: 3)).ToList();
        var included = winner.Concat(runnerUp).ToList();
        var snapshot = AnimeRankingSnapshot.Build(included, storedOrder: []);

        var ranking = RecapRankingBuilder.BuildSeasonRanking(included, RecapPeriod.Yearly(2022), globalMean: 5.0, rankingSnapshot: snapshot);

        Assert.Equal(2, ranking.Count);
        Assert.Equal("spring", ranking[0].Season);
        Assert.NotEmpty(ranking[0].PostersByScore);
        Assert.All(ranking[0].PostersByScore, bucket => Assert.True(bucket.Posters.Count <= 3));
        // No stored order, so the tie at score 10 falls back to title —
        // ids 1-3 (titles "Anime 001".."Anime 003") win the three slots.
        Assert.Equal([1, 2, 3], ranking[0].PostersByScore.SelectMany(b => b.Posters).Take(3).Select(p => p.AnimeId));

        Assert.Equal("winter", ranking[1].Season);
        Assert.NotEmpty(ranking[1].PostersByScore);
        Assert.All(ranking[1].PostersByScore, bucket => Assert.True(bucket.Posters.Count <= 3));
    }

    // design.md D1/tasks.md 4.3: my ranking, not title, breaks a within-score
    // tie — a stored order that disagrees with title order still wins.
    [Fact]
    public void MyRankingBreaksAWithinScoreTieRatherThanTitle()
    {
        var group = Group(1, new DateOnly(2022, 4, 15), 8, 8, 8, 8); // ids 1-4, titles "Anime 001".."Anime 004"
        var snapshot = AnimeRankingSnapshot.Build(group, storedOrder: [4, 1, 3, 2]);

        var ranking = RecapRankingBuilder.BuildSeasonRanking(group, RecapPeriod.Yearly(2022), globalMean: 5.0, rankingSnapshot: snapshot);

        var row = Assert.Single(ranking);
        var bucket = Assert.Single(row.PostersByScore);
        Assert.Equal(8, bucket.Score);
        Assert.Equal([4, 1, 3], bucket.Posters.Select(p => p.AnimeId));
    }

    // design.md D1/tasks.md 4.4: a scored Plan-to-watch anime is scored (so
    // it's counted and histogrammed) but RankBandResolver leaves it outside
    // the ranking, so it sorts after every ranked anime of its score and is
    // dropped once three ranked ones already fill the bucket.
    [Fact]
    public void AScoredButUnrankedAnimeSortsLastAndIsDroppedWhenThreeRankedFillTheBucket()
    {
        var ranked = Group(1, new DateOnly(2022, 4, 15), 8, 8, 8); // ids 1-3, all hand-ordered
        var unranked = new UserAnimeEntry
        {
            AnimeId = 4,
            Anime = new AnimeMetadata { Id = 4, Title = "Anime 004", AiredFrom = new DateOnly(2022, 4, 15) },
            MyScore = 8,
            Status = WatchStatus.PlanToWatch,
        };
        var group = ranked.Append(unranked).ToList();
        var snapshot = AnimeRankingSnapshot.Build(group, storedOrder: []);

        var ranking = RecapRankingBuilder.BuildSeasonRanking(group, RecapPeriod.Yearly(2022), globalMean: 5.0, rankingSnapshot: snapshot);

        var row = Assert.Single(ranking);
        Assert.Equal(4, row.ScoredCount); // the unranked anime still counts toward the group's scored count
        var bucket = Assert.Single(row.PostersByScore);
        Assert.Equal(3, bucket.Posters.Count);
        Assert.Equal([1, 2, 3], bucket.Posters.Select(p => p.AnimeId));
        Assert.DoesNotContain(bucket.Posters, p => p.AnimeId == 4);
    }

    // design.md D2/tasks.md 4.5: the buckets carry one entry per score the
    // group actually holds, descending from 10, with no entry for a score it
    // doesn't hold.
    [Fact]
    public void PostersByScoreBucketsAreSparseAndScoreDescending()
    {
        var group = Group(1, new DateOnly(2022, 4, 15), 10, 8, 8);
        var snapshot = AnimeRankingSnapshot.Build(group, storedOrder: []);

        var ranking = RecapRankingBuilder.BuildSeasonRanking(group, RecapPeriod.Yearly(2022), globalMean: 5.0, rankingSnapshot: snapshot);

        var row = Assert.Single(ranking);
        Assert.Equal(2, row.PostersByScore.Count);
        Assert.Equal(10, row.PostersByScore[0].Score);
        Assert.Equal(8, row.PostersByScore[1].Score);
    }

    // Extended tie-break (tasks.md 6.6-6.7, design.md decision 6): weighted
    // score at full precision, then scored count, then a score-by-score
    // histogram from 10 down to 1, then newest-first. Equal-count fixtures
    // below use identical score multisets so their weighted scores are
    // bit-identical by construction, needing no reasoning about floating
    // point; the unequal-count fixtures instead give both groups a raw mean
    // exactly equal to globalMean, which collapses W to globalMean
    // regardless of v and was verified to be bit-identical across these
    // specific counts.
    [Fact]
    public void EqualScoresWithUnequalScoredCountsPrefersTheBetterCoveredGroup_Season()
    {
        var betterCovered = Group(1, new DateOnly(2022, 1, 15), 7, 7, 7, 7, 8, 8, 8, 8); // winter, v=8, avg 7.5
        var thinnerCoverage = Group(100, new DateOnly(2022, 4, 15), 7, 7, 8, 8); // spring, v=4, avg 7.5
        var included = betterCovered.Concat(thinnerCoverage).ToList();

        var ranking = RecapRankingBuilder.BuildSeasonRanking(included, RecapPeriod.Yearly(2022), globalMean: 7.5, rankingSnapshot: AnimeRankingSnapshot.Empty);

        Assert.Equal(2, ranking.Count);
        Assert.Equal(ranking[0].WeightedScore, ranking[1].WeightedScore);
        Assert.Equal("winter", ranking[0].Season); // older, but wins on scored count
        Assert.Equal("spring", ranking[1].Season);
    }

    [Fact]
    public void EqualScoresWithUnequalScoredCountsPrefersTheBetterCoveredGroup_Year()
    {
        var betterCovered = Enumerable.Range(1, 10).Select(id => Entry(id, new DateOnly(2021, 6, 1), 7)) // v=20, avg 7.5
            .Concat(Enumerable.Range(11, 10).Select(id => Entry(id, new DateOnly(2021, 6, 1), 8)))
            .ToList();
        var thinnerCoverage = Enumerable.Range(100, 5).Select(id => Entry(id, new DateOnly(2022, 6, 1), 7)) // v=10, avg 7.5
            .Concat(Enumerable.Range(105, 5).Select(id => Entry(id, new DateOnly(2022, 6, 1), 8)))
            .ToList();
        var included = betterCovered.Concat(thinnerCoverage).ToList();

        var ranking = RecapRankingBuilder.BuildYearRanking(included, RecapPeriod.MultiYear(2021, 2022), globalMean: 7.5, rankingSnapshot: AnimeRankingSnapshot.Empty);

        Assert.Equal(2, ranking.Count);
        Assert.Equal(ranking[0].WeightedScore, ranking[1].WeightedScore);
        Assert.Equal(2021, ranking[0].Year); // older, but wins on scored count
        Assert.Equal(2022, ranking[1].Year);
    }

    [Fact]
    public void EqualScoreAndCountSeparatedAtTenPrefersMoreTens_Season()
    {
        var moreTens = Group(1, new DateOnly(2022, 1, 15), 10, 10, 6, 6, 3); // winter, two 10s
        var fewerTens = Group(100, new DateOnly(2022, 4, 15), 10, 7, 7, 7, 4); // spring, one 10
        var included = moreTens.Concat(fewerTens).ToList();

        var ranking = RecapRankingBuilder.BuildSeasonRanking(included, RecapPeriod.Yearly(2022), globalMean: 5.0, rankingSnapshot: AnimeRankingSnapshot.Empty);

        Assert.Equal(2, ranking.Count);
        Assert.Equal(ranking[0].WeightedScore, ranking[1].WeightedScore);
        Assert.Equal(ranking[0].ScoredCount, ranking[1].ScoredCount);
        Assert.Equal("winter", ranking[0].Season); // older, but wins on more 10s
        Assert.Equal("spring", ranking[1].Season);
    }

    [Fact]
    public void EqualScoreAndCountSeparatedAtTenPrefersMoreTens_Year()
    {
        var moreTens = Group(1, new DateOnly(2021, 6, 1), 10, 10, 6, 6, 3);
        var fewerTens = Group(100, new DateOnly(2022, 6, 1), 10, 7, 7, 7, 4);
        var included = moreTens.Concat(fewerTens).ToList();

        var ranking = RecapRankingBuilder.BuildYearRanking(included, RecapPeriod.MultiYear(2021, 2022), globalMean: 5.0, rankingSnapshot: AnimeRankingSnapshot.Empty);

        Assert.Equal(2, ranking.Count);
        Assert.Equal(ranking[0].WeightedScore, ranking[1].WeightedScore);
        Assert.Equal(ranking[0].ScoredCount, ranking[1].ScoredCount);
        Assert.Equal(2021, ranking[0].Year); // older, but wins on more 10s
        Assert.Equal(2022, ranking[1].Year);
    }

    [Fact]
    public void EqualScoreAndCountSeparatedAtEightPrefersMoreEights_Season()
    {
        var moreEights = Group(1, new DateOnly(2022, 1, 15), 10, 10, 9, 9, 8, 8, 8, 2); // winter, three 8s
        var fewerEights = Group(100, new DateOnly(2022, 4, 15), 10, 10, 9, 9, 8, 8, 5, 5); // spring, two 8s
        var included = moreEights.Concat(fewerEights).ToList();

        var ranking = RecapRankingBuilder.BuildSeasonRanking(included, RecapPeriod.Yearly(2022), globalMean: 5.0, rankingSnapshot: AnimeRankingSnapshot.Empty);

        Assert.Equal(2, ranking.Count);
        Assert.Equal(ranking[0].WeightedScore, ranking[1].WeightedScore);
        Assert.Equal(ranking[0].ScoredCount, ranking[1].ScoredCount);
        Assert.Equal("winter", ranking[0].Season); // older, but wins on more 8s (ties at 10 and 9)
        Assert.Equal("spring", ranking[1].Season);
    }

    [Fact]
    public void EqualScoreAndCountSeparatedAtEightPrefersMoreEights_Year()
    {
        var moreEights = Group(1, new DateOnly(2021, 6, 1), 10, 10, 9, 9, 8, 8, 8, 2);
        var fewerEights = Group(100, new DateOnly(2022, 6, 1), 10, 10, 9, 9, 8, 8, 5, 5);
        var included = moreEights.Concat(fewerEights).ToList();

        var ranking = RecapRankingBuilder.BuildYearRanking(included, RecapPeriod.MultiYear(2021, 2022), globalMean: 5.0, rankingSnapshot: AnimeRankingSnapshot.Empty);

        Assert.Equal(2, ranking.Count);
        Assert.Equal(ranking[0].WeightedScore, ranking[1].WeightedScore);
        Assert.Equal(ranking[0].ScoredCount, ranking[1].ScoredCount);
        Assert.Equal(2021, ranking[0].Year); // older, but wins on more 8s (ties at 10 and 9)
        Assert.Equal(2022, ranking[1].Year);
    }

    [Fact]
    public void WhollyIdenticalHistogramsFallBackToTheNewerGroup_Season()
    {
        var older = Group(1, new DateOnly(2022, 1, 15), 9, 9, 7, 7, 5, 5); // winter
        var newer = Group(100, new DateOnly(2022, 4, 15), 9, 9, 7, 7, 5, 5); // spring, same histogram

        var ranking = RecapRankingBuilder.BuildSeasonRanking(older.Concat(newer).ToList(), RecapPeriod.Yearly(2022), globalMean: 5.0, rankingSnapshot: AnimeRankingSnapshot.Empty);

        Assert.Equal(2, ranking.Count);
        Assert.Equal(ranking[0].WeightedScore, ranking[1].WeightedScore);
        Assert.Equal(ranking[0].ScoredCount, ranking[1].ScoredCount);
        Assert.Equal("spring", ranking[0].Season); // newer wins once every score level ties
        Assert.Equal("winter", ranking[1].Season);
    }

    [Fact]
    public void WhollyIdenticalHistogramsFallBackToTheNewerGroup_Year()
    {
        var older = Group(1, new DateOnly(2021, 6, 1), 9, 9, 7, 7, 5, 5);
        var newer = Group(100, new DateOnly(2022, 6, 1), 9, 9, 7, 7, 5, 5);

        var ranking = RecapRankingBuilder.BuildYearRanking(older.Concat(newer).ToList(), RecapPeriod.MultiYear(2021, 2022), globalMean: 5.0, rankingSnapshot: AnimeRankingSnapshot.Empty);

        Assert.Equal(2, ranking.Count);
        Assert.Equal(ranking[0].WeightedScore, ranking[1].WeightedScore);
        Assert.Equal(ranking[0].ScoredCount, ranking[1].ScoredCount);
        Assert.Equal(2022, ranking[0].Year); // newer wins once every score level ties
        Assert.Equal(2021, ranking[1].Year);
    }

    [Fact]
    public void FullPrecisionOrderingIsNeverOverruledByScoredCount()
    {
        // Both groups' weighted scores round to 7.75 at the two displayed
        // decimals — 7.7454545...(winter) against 7.750000...1 (spring) —
        // but spring's unrounded score is genuinely higher, so it ranks
        // first despite covering far fewer scored anime than winter
        // (design.md decision 6, step 1).
        var largerButLowerScoring = Enumerable.Range(1, 41).Select(id => Entry(id, new DateOnly(2022, 1, 15), 8))
            .Concat(Enumerable.Range(42, 9).Select(id => Entry(id, new DateOnly(2022, 1, 15), 7)))
            .ToList();
        var smallerButHigherScoring = Group(100, new DateOnly(2022, 4, 15), 8, 8, 8, 8, 8, 8, 10);
        var included = largerButLowerScoring.Concat(smallerButHigherScoring).ToList();

        var ranking = RecapRankingBuilder.BuildSeasonRanking(included, RecapPeriod.Yearly(2022), globalMean: 7.0, rankingSnapshot: AnimeRankingSnapshot.Empty);

        Assert.Equal(2, ranking.Count);
        Assert.Equal(7.75, ranking[0].WeightedScore);
        Assert.Equal(7.75, ranking[1].WeightedScore);
        Assert.Equal("spring", ranking[0].Season);
        Assert.Equal(7, ranking[0].ScoredCount);
        Assert.Equal("winter", ranking[1].Season);
        Assert.Equal(50, ranking[1].ScoredCount);
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
        var stats = RecapStatsBuilder.Build(included, included, [], new Dictionary<int, int>(), RecapTimeFilter.Aired);

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

    // ScoreCounts (design.md D5, tasks.md 4.1-4.2, 11.3): ten ascending
    // counts — index 0 is score 1, index 9 is score 10 — read straight off
    // the same histogram the tie-break above already uses, so a group's
    // reported counts can never disagree with what actually decided its
    // ranking position.
    [Fact]
    public void ScoreCountsAreTenAscendingCountsMatchingTheScoredEntries_Season()
    {
        var group = Group(1, new DateOnly(2022, 1, 15), 10, 10, 7, 5, 5, 5);

        var ranking = RecapRankingBuilder.BuildSeasonRanking(group, RecapPeriod.Yearly(2022), globalMean: 5.0, rankingSnapshot: AnimeRankingSnapshot.Empty);

        var row = Assert.Single(ranking);
        Assert.Equal(10, row.ScoreCounts.Count);
        Assert.Equal(0, row.ScoreCounts[0]); // score 1
        Assert.Equal(3, row.ScoreCounts[4]); // score 5
        Assert.Equal(0, row.ScoreCounts[5]); // score 6 — nobody gave it
        Assert.Equal(1, row.ScoreCounts[6]); // score 7
        Assert.Equal(2, row.ScoreCounts[9]); // score 10
        Assert.Equal(6, row.ScoreCounts.Sum());
    }

    [Fact]
    public void ScoreCountsAreTenAscendingCountsMatchingTheScoredEntries_Year()
    {
        var group = Group(1, new DateOnly(2022, 6, 1), 9, 9, 9, 4);

        var ranking = RecapRankingBuilder.BuildYearRanking(group, RecapPeriod.Yearly(2022), globalMean: 5.0, rankingSnapshot: AnimeRankingSnapshot.Empty);

        var row = Assert.Single(ranking);
        Assert.Equal(10, row.ScoreCounts.Count);
        Assert.Equal(1, row.ScoreCounts[3]); // score 4
        Assert.Equal(3, row.ScoreCounts[8]); // score 9
        Assert.Equal(4, row.ScoreCounts.Sum());
    }

    // The count that decides a tie (the 10 column, per
    // EqualScoreAndCountSeparatedAtTenPrefersMoreTens_Season above) must be
    // exactly what the winning row's own ScoreCounts reports — the DTO and
    // the tie-break can't drift apart because both read the same histogram.
    [Fact]
    public void TheHistogramColumnThatDecidesATieMatchesTheWinningRowsReportedScoreCounts()
    {
        var moreTens = Group(1, new DateOnly(2022, 1, 15), 10, 10, 6, 6, 3); // winter, two 10s
        var fewerTens = Group(100, new DateOnly(2022, 4, 15), 10, 7, 7, 7, 4); // spring, one 10
        var included = moreTens.Concat(fewerTens).ToList();

        var ranking = RecapRankingBuilder.BuildSeasonRanking(included, RecapPeriod.Yearly(2022), globalMean: 5.0, rankingSnapshot: AnimeRankingSnapshot.Empty);

        Assert.Equal("winter", ranking[0].Season);
        Assert.Equal(2, ranking[0].ScoreCounts[9]);
        Assert.Equal("spring", ranking[1].Season);
        Assert.Equal(1, ranking[1].ScoreCounts[9]);
    }
}
