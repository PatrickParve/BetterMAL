using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Profile;
using AnimeTracker.Api.Services.Recap;

namespace AnimeTracker.Api.Tests.Services.Recap;

// RecapStatsBuilder.Build (tasks.md 3.1-3.4).
public class RecapStatsBuilderTests
{
    private static UserAnimeEntry Entry(
        int animeId, string? mediaType, int episodesWatched, int? myScore = null, double? malScore = null,
        int? durationSeconds = null, WatchStatus status = WatchStatus.Watching) =>
        new()
        {
            AnimeId = animeId,
            Anime = new AnimeMetadata
            {
                Id = animeId,
                Title = $"Anime {animeId}",
                MediaType = mediaType,
                MalScore = malScore,
                AverageEpisodeDurationSeconds = durationSeconds,
            },
            EpisodesWatched = episodesWatched,
            MyScore = myScore,
            Status = status,
        };

    [Fact]
    public void MoviesStayOutOfTheEpisodeCount()
    {
        var series = Entry(1, "tv", episodesWatched: 12);
        var movie = Entry(2, "movie", episodesWatched: 1);

        var stats = RecapStatsBuilder.Build([series, movie], [series, movie]);

        Assert.Equal(12, stats.EpisodesWatched);
        Assert.Equal(1, stats.MoviesWatched);
    }

    [Fact]
    public void UnscoredEntriesStayOutOfTheMean()
    {
        var scoredA = Entry(1, "tv", 12, myScore: 8);
        var scoredB = Entry(2, "tv", 12, myScore: 6);
        var unscored = Entry(3, "tv", 12, myScore: null);

        var stats = RecapStatsBuilder.Build([scoredA, scoredB, unscored], [scoredA, scoredB, unscored]);

        Assert.Equal(7.0, stats.MeanScore);
    }

    [Fact]
    public void NoScoresYieldsNullMean()
    {
        var unscored = Entry(1, "tv", 12, myScore: null);

        var stats = RecapStatsBuilder.Build([unscored], [unscored]);

        Assert.Null(stats.MeanScore);
    }

    [Fact]
    public void DurationFallbackMatchesTheAppWideAssumption()
    {
        var noCachedDuration = Entry(1, "tv", episodesWatched: 5, durationSeconds: null);

        var stats = RecapStatsBuilder.Build([noCachedDuration], [noCachedDuration]);

        Assert.Equal(5L * ProfileService.AssumedMinutesPerEpisode * 60, stats.TimeSpentSeconds);
    }

    [Fact]
    public void CachedDurationIsUsedOverTheFallback()
    {
        var cached = Entry(1, "tv", episodesWatched: 5, durationSeconds: 1500);

        var stats = RecapStatsBuilder.Build([cached], [cached]);

        Assert.Equal(5L * 1500, stats.TimeSpentSeconds);
    }

    [Fact]
    public void TimeSpentIncludesMoviesUnlikeTheEpisodeCount()
    {
        var movie = Entry(1, "movie", episodesWatched: 1, durationSeconds: 6000);

        var stats = RecapStatsBuilder.Build([movie], [movie]);

        Assert.Equal(0, stats.EpisodesWatched);
        Assert.Equal(6000L, stats.TimeSpentSeconds);
    }

    // Ten entries pairing my score (1-10) against MAL's community score
    // (9.5-5.0) in perfect anti-correlation: clears the shared helper's
    // minimum-rated-pairs guard, gives both scales a non-zero SD, and — by
    // construction (myMean=5.5, mySd≈2.872, malMean=7.25, malSd≈1.436) —
    // yields entries on both sides of ScoreDivergence's SD threshold and
    // label gates. Entries 1-4 clear "they liked it, I didn't" (my<=5,
    // mal>=7.5, divergence 1.04-3.13 SD); entries 8-10 clear "I liked it,
    // they didn't" (my>=8, mal<=7.5, divergence 1.74-3.13 SD); entry 7 (my=7,
    // mal=6.5) crosses the 1.0 SD threshold (divergence ≈1.04 SD) but sits in
    // the neutral 6-7 band, so it's excluded by the label gate alone; entries
    // 5-6 sit well under the threshold.
    private static List<UserAnimeEntry> SpreadPopulation() =>
        Enumerable.Range(1, 10)
            .Select(id => Entry(id, "tv", 12, myScore: id, malScore: 9.5 - (id - 1) * 0.5))
            .ToList();

    [Fact]
    public void DroppedCountsOnlyDroppedEntries()
    {
        var completed = Entry(1, "tv", 12, status: WatchStatus.Completed);
        var dropped1 = Entry(2, "tv", 4, status: WatchStatus.Dropped);
        var dropped2 = Entry(3, "tv", 2, status: WatchStatus.Dropped);
        var watching = Entry(4, "tv", 6, status: WatchStatus.Watching);

        var stats = RecapStatsBuilder.Build(
            [completed, dropped1, dropped2, watching], [completed, dropped1, dropped2, watching]);

        Assert.Equal(1, stats.Completed);
        Assert.Equal(2, stats.Dropped);
    }

    [Fact]
    public void HotTakesDegradeToTwoWhenOnlyTwoEligible()
    {
        var wholeList = SpreadPopulation();
        var included = wholeList.Take(2).ToList();

        var stats = RecapStatsBuilder.Build(included, wholeList);

        Assert.Equal(2, stats.HotTakes.Count);
    }

    [Fact]
    public void HotTakesDegradeToOneWhenOnlyOneEligible()
    {
        var wholeList = SpreadPopulation();
        var included = wholeList.Take(1).ToList();

        var stats = RecapStatsBuilder.Build(included, wholeList);

        Assert.Single(stats.HotTakes);
    }

    [Fact]
    public void HotTakesAreEmptyWhenNoneEligible()
    {
        var wholeList = SpreadPopulation();
        var unscored = Entry(99, "tv", 12, myScore: null, malScore: 7.0);

        var stats = RecapStatsBuilder.Build([unscored], wholeList);

        Assert.Empty(stats.HotTakes);
    }

    [Fact]
    public void HotTakesCapAtFiveOrderedByAbsoluteDivergence()
    {
        var wholeList = SpreadPopulation();

        var stats = RecapStatsBuilder.Build(wholeList, wholeList);

        Assert.Equal(5, stats.HotTakes.Count);
        var divergences = stats.HotTakes.Select(h => Math.Abs(h.Divergence)).ToList();
        Assert.Equal(divergences.OrderByDescending(d => d), divergences);
    }

    [Fact]
    public void HotTakesMarkDroppedEntriesRevealedButNotWatchingEntries()
    {
        var wholeList = SpreadPopulation();
        var dropped = wholeList[0];
        dropped.Status = WatchStatus.Dropped;
        var watching = wholeList[1];
        watching.Status = WatchStatus.Watching;
        var included = new List<UserAnimeEntry> { dropped, watching };

        var stats = RecapStatsBuilder.Build(included, wholeList);

        Assert.True(stats.HotTakes.Single(h => h.AnimeId == dropped.AnimeId).MalRevealed);
        Assert.False(stats.HotTakes.Single(h => h.AnimeId == watching.AnimeId).MalRevealed);
    }

    [Fact]
    public void HotTakesDegradeToFourWhenOnlyFourEligible()
    {
        var wholeList = SpreadPopulation();
        var included = wholeList.Take(4).ToList();

        var stats = RecapStatsBuilder.Build(included, wholeList);

        Assert.Equal(4, stats.HotTakes.Count);
    }

    [Fact]
    public void HotTakesExcludeAnEntryInTheNeutralBandDespiteCrossingTheThreshold()
    {
        var wholeList = SpreadPopulation();
        // my=7, mal=6.5: divergence is ≈1.04 SD (past the threshold), but
        // myScore=7 sits in the neutral 6-7 band, so neither label gate is
        // satisfied.
        var neutralButDivergent = wholeList[6];

        var stats = RecapStatsBuilder.Build([neutralButDivergent], wholeList);

        Assert.Empty(stats.HotTakes);
    }

    [Fact]
    public void HotTakesExcludeAHighScoreMalAlsoLiked()
    {
        var wholeList = SpreadPopulation();
        // I scored it 9 and MAL's average is 8.4 — both liked it, so neither
        // rule's like/dislike boundaries are crossed.
        var agreed = Entry(98, "tv", 12, myScore: 9, malScore: 8.4);

        var stats = RecapStatsBuilder.Build([agreed], wholeList);

        Assert.Empty(stats.HotTakes);
    }

    [Fact]
    public void HotTakesExcludeDivergenceJustShortOfOneStandardDeviation()
    {
        var wholeList = SpreadPopulation();
        // my<=5 and mal>=7.5 clear both label gates; divergence is ≈0.95 SD —
        // just short of the 1.0 SD threshold.
        var almostDivergent = Entry(97, "tv", 12, myScore: 5, malScore: 8.36);

        var stats = RecapStatsBuilder.Build([almostDivergent], wholeList);

        Assert.Empty(stats.HotTakes);
    }

    [Fact]
    public void HotTakesIncludeAnAnimeTheProfilePageWouldName()
    {
        var wholeList = SpreadPopulation();
        // my=1, mal=9.5 — qualifies under ScoreDivergence.IsTheyLikedItIDidnt,
        // the exact predicate ProfileService.BuildOpinionDivergence applies
        // for its TheyLikedItIDidnt list (spec scenario "Agreement with the
        // profile page").
        var candidate = wholeList[0];
        var context = ScoreDivergence.TryCompute(wholeList)!.Value;
        Assert.True(ScoreDivergence.IsTheyLikedItIDidnt(candidate, context.DivergenceOf(candidate)));

        var stats = RecapStatsBuilder.Build([candidate], wholeList);

        Assert.Contains(stats.HotTakes, h => h.AnimeId == candidate.AnimeId);
    }
}
