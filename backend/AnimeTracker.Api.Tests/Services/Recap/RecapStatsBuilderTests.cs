using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Profile;
using AnimeTracker.Api.Services.Recap;

namespace AnimeTracker.Api.Tests.Services.Recap;

// RecapStatsBuilder.Build (tasks.md 3.1-3.4).
public class RecapStatsBuilderTests
{
    private static UserAnimeEntry Entry(
        int animeId, string? mediaType, int episodesWatched, int? myScore = null, double? malScore = null,
        int? durationSeconds = null, WatchStatus status = WatchStatus.Watching,
        int rewatchCount = 0, int? totalEpisodes = null) =>
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
                TotalEpisodes = totalEpisodes,
            },
            EpisodesWatched = episodesWatched,
            MyScore = myScore,
            Status = status,
            RewatchCount = rewatchCount,
        };

    [Fact]
    public void MoviesStayOutOfTheEpisodeCount()
    {
        var series = Entry(1, "tv", episodesWatched: 12);
        var movie = Entry(2, "movie", episodesWatched: 1);

        var stats = RecapStatsBuilder.Build([series, movie], [series, movie], [], new Dictionary<int, int>(), RecapTimeFilter.Aired);

        Assert.Equal(12, stats.EpisodesWatched);
        Assert.Equal(1, stats.MoviesWatched);
    }

    [Fact]
    public void UnscoredEntriesStayOutOfTheMean()
    {
        var scoredA = Entry(1, "tv", 12, myScore: 8);
        var scoredB = Entry(2, "tv", 12, myScore: 6);
        var unscored = Entry(3, "tv", 12, myScore: null);

        var stats = RecapStatsBuilder.Build([scoredA, scoredB, unscored], [scoredA, scoredB, unscored], [], new Dictionary<int, int>(), RecapTimeFilter.Aired);

        Assert.Equal(7.0, stats.MeanScore);
    }

    [Fact]
    public void NoScoresYieldsNullMean()
    {
        var unscored = Entry(1, "tv", 12, myScore: null);

        var stats = RecapStatsBuilder.Build([unscored], [unscored], [], new Dictionary<int, int>(), RecapTimeFilter.Aired);

        Assert.Null(stats.MeanScore);
    }

    [Fact]
    public void DurationFallbackMatchesTheAppWideAssumption()
    {
        var noCachedDuration = Entry(1, "tv", episodesWatched: 5, durationSeconds: null);

        var stats = RecapStatsBuilder.Build([noCachedDuration], [noCachedDuration], [], new Dictionary<int, int>(), RecapTimeFilter.Aired);

        Assert.Equal(5L * ProfileService.AssumedMinutesPerEpisode * 60, stats.TimeSpentSeconds);
    }

    [Fact]
    public void CachedDurationIsUsedOverTheFallback()
    {
        var cached = Entry(1, "tv", episodesWatched: 5, durationSeconds: 1500);

        var stats = RecapStatsBuilder.Build([cached], [cached], [], new Dictionary<int, int>(), RecapTimeFilter.Aired);

        Assert.Equal(5L * 1500, stats.TimeSpentSeconds);
    }

    [Fact]
    public void TimeSpentIncludesMoviesUnlikeTheEpisodeCount()
    {
        var movie = Entry(1, "movie", episodesWatched: 1, durationSeconds: 6000);

        var stats = RecapStatsBuilder.Build([movie], [movie], [], new Dictionary<int, int>(), RecapTimeFilter.Aired);

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
            [completed, dropped1, dropped2, watching], [completed, dropped1, dropped2, watching], [watching], new Dictionary<int, int>(), RecapTimeFilter.Aired);

        Assert.Equal(1, stats.Completed);
        Assert.Equal(2, stats.Dropped);
        Assert.Equal(1, stats.CurrentlyWatching);
    }

    [Fact]
    public void WatchingEntryAiredInsideThePeriodIsCountedUnderWhatAired()
    {
        var watching = Entry(1, "tv", 6, status: WatchStatus.Watching);

        var stats = RecapStatsBuilder.Build([watching], [watching], [watching], new Dictionary<int, int>(), RecapTimeFilter.Aired);

        Assert.Equal(1, stats.CurrentlyWatching);
    }

    [Fact]
    public void CurrentlyWatchingSurvivesTheWatchHistoryFilter()
    {
        // Under "What I watched", a Watching entry has no CompletedAt, so it
        // never lands in `included` — only in the aired-attributed list.
        var watching = Entry(1, "tv", 6, status: WatchStatus.Watching);

        var stats = RecapStatsBuilder.Build([], [watching], [watching], new Dictionary<int, int>(), RecapTimeFilter.Aired);

        Assert.Equal(1, stats.CurrentlyWatching);
    }

    [Fact]
    public void WatchingEntryAiredOutsideThePeriodIsNotCounted()
    {
        // Aired outside the period, so RecapEntrySelector never places it in
        // the aired-attributed list passed as airedIncluded.
        var completed = Entry(1, "tv", 12, status: WatchStatus.Completed);
        var watchingOutsidePeriod = Entry(2, "tv", 6, status: WatchStatus.Watching);

        var stats = RecapStatsBuilder.Build([completed], [completed, watchingOutsidePeriod], [completed], new Dictionary<int, int>(), RecapTimeFilter.Aired);

        Assert.Equal(0, stats.CurrentlyWatching);
    }

    [Fact]
    public void WatchingEntryWithNoAiredFromIsCountedForNoPeriod()
    {
        // No AiredFrom means RecapEntrySelector's "aired" attribution is
        // null, so it never lands in airedIncluded for any period.
        var watchingNoAiredFrom = Entry(1, "tv", 6, status: WatchStatus.Watching);

        var stats = RecapStatsBuilder.Build([], [watchingNoAiredFrom], [], new Dictionary<int, int>(), RecapTimeFilter.Aired);

        Assert.Equal(0, stats.CurrentlyWatching);
    }

    [Fact]
    public void CompletedDroppedAndOnHoldEntriesDoNotInflateCurrentlyWatching()
    {
        var completed = Entry(1, "tv", 12, status: WatchStatus.Completed);
        var dropped = Entry(2, "tv", 4, status: WatchStatus.Dropped);
        var onHold = Entry(3, "tv", 2, status: WatchStatus.OnHold);

        var stats = RecapStatsBuilder.Build(
            [completed, dropped], [completed, dropped, onHold], [completed, dropped, onHold], new Dictionary<int, int>(), RecapTimeFilter.Aired);

        Assert.Equal(0, stats.CurrentlyWatching);
    }

    [Fact]
    public void HotTakesDegradeToTwoWhenOnlyTwoEligible()
    {
        var wholeList = SpreadPopulation();
        var included = wholeList.Take(2).ToList();

        var stats = RecapStatsBuilder.Build(included, wholeList, [], new Dictionary<int, int>(), RecapTimeFilter.Aired);

        Assert.Equal(2, stats.HotTakes.Count);
    }

    [Fact]
    public void HotTakesDegradeToOneWhenOnlyOneEligible()
    {
        var wholeList = SpreadPopulation();
        var included = wholeList.Take(1).ToList();

        var stats = RecapStatsBuilder.Build(included, wholeList, [], new Dictionary<int, int>(), RecapTimeFilter.Aired);

        Assert.Single(stats.HotTakes);
    }

    [Fact]
    public void HotTakesAreEmptyWhenNoneEligible()
    {
        var wholeList = SpreadPopulation();
        var unscored = Entry(99, "tv", 12, myScore: null, malScore: 7.0);

        var stats = RecapStatsBuilder.Build([unscored], wholeList, [], new Dictionary<int, int>(), RecapTimeFilter.Aired);

        Assert.Empty(stats.HotTakes);
    }

    [Fact]
    public void HotTakesCapAtFiveOrderedByAbsoluteDivergence()
    {
        var wholeList = SpreadPopulation();

        var stats = RecapStatsBuilder.Build(wholeList, wholeList, [], new Dictionary<int, int>(), RecapTimeFilter.Aired);

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

        var stats = RecapStatsBuilder.Build(included, wholeList, [], new Dictionary<int, int>(), RecapTimeFilter.Aired);

        Assert.True(stats.HotTakes.Single(h => h.AnimeId == dropped.AnimeId).MalRevealed);
        Assert.False(stats.HotTakes.Single(h => h.AnimeId == watching.AnimeId).MalRevealed);
    }

    [Fact]
    public void HotTakesDegradeToFourWhenOnlyFourEligible()
    {
        var wholeList = SpreadPopulation();
        var included = wholeList.Take(4).ToList();

        var stats = RecapStatsBuilder.Build(included, wholeList, [], new Dictionary<int, int>(), RecapTimeFilter.Aired);

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

        var stats = RecapStatsBuilder.Build([neutralButDivergent], wholeList, [], new Dictionary<int, int>(), RecapTimeFilter.Aired);

        Assert.Empty(stats.HotTakes);
    }

    [Fact]
    public void HotTakesExcludeAHighScoreMalAlsoLiked()
    {
        var wholeList = SpreadPopulation();
        // I scored it 9 and MAL's average is 8.4 — both liked it, so neither
        // rule's like/dislike boundaries are crossed.
        var agreed = Entry(98, "tv", 12, myScore: 9, malScore: 8.4);

        var stats = RecapStatsBuilder.Build([agreed], wholeList, [], new Dictionary<int, int>(), RecapTimeFilter.Aired);

        Assert.Empty(stats.HotTakes);
    }

    [Fact]
    public void HotTakesExcludeDivergenceJustShortOfOneStandardDeviation()
    {
        var wholeList = SpreadPopulation();
        // my<=5 and mal>=7.5 clear both label gates; divergence is ≈0.95 SD —
        // just short of the 1.0 SD threshold.
        var almostDivergent = Entry(97, "tv", 12, myScore: 5, malScore: 8.36);

        var stats = RecapStatsBuilder.Build([almostDivergent], wholeList, [], new Dictionary<int, int>(), RecapTimeFilter.Aired);

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

        var stats = RecapStatsBuilder.Build([candidate], wholeList, [], new Dictionary<int, int>(), RecapTimeFilter.Aired);

        Assert.Contains(stats.HotTakes, h => h.AnimeId == candidate.AnimeId);
    }

    // The per-period episode figure under "watched" (design.md decisions
    // 5/6, tasks.md 5.5/5.8): a logged figure is used verbatim; an entry with
    // no log entry falls back to the rewatch-inclusive count; "aired" never
    // reads the log or the rewatch multiplier.

    [Fact]
    public void LoggedEpisodesAreUsedVerbatimUnderWatched()
    {
        var entry = Entry(1, "tv", episodesWatched: 20, totalEpisodes: 24, rewatchCount: 1, status: WatchStatus.Watching);
        var watchLog = new Dictionary<int, int> { [1] = 9 };

        var stats = RecapStatsBuilder.Build([entry], [entry], [], watchLog, RecapTimeFilter.Watched);

        Assert.Equal(9, stats.EpisodesWatched);
    }

    [Fact]
    public void APreTrackingCompletionFallsBackToTheRewatchInclusiveCount()
    {
        var entry = Entry(1, "tv", episodesWatched: 12, totalEpisodes: 12, rewatchCount: 1, status: WatchStatus.Completed);

        var stats = RecapStatsBuilder.Build([entry], [entry], [], new Dictionary<int, int>(), RecapTimeFilter.Watched);

        Assert.Equal(24, stats.EpisodesWatched);
    }

    [Fact]
    public void TheStoredRewatchCountIsNotReadTwiceWhenTheLogAlsoHoldsProgress()
    {
        var entry = Entry(1, "tv", episodesWatched: 12, totalEpisodes: 12, rewatchCount: 1, status: WatchStatus.Completed);
        var watchLog = new Dictionary<int, int> { [1] = 24 };

        var stats = RecapStatsBuilder.Build([entry], [entry], [], watchLog, RecapTimeFilter.Watched);

        Assert.Equal(24, stats.EpisodesWatched);
    }

    [Fact]
    public void RewatchesDoNotCountUnderAired()
    {
        var entry = Entry(1, "tv", episodesWatched: 12, totalEpisodes: 12, rewatchCount: 1, status: WatchStatus.Completed);

        var stats = RecapStatsBuilder.Build([entry], [entry], [], new Dictionary<int, int>(), RecapTimeFilter.Aired);

        Assert.Equal(12, stats.EpisodesWatched);
    }

    [Fact]
    public void TimeSpentUsesTheSameLoggedFigureAsEpisodesWatched()
    {
        var entry = Entry(1, "tv", episodesWatched: 20, totalEpisodes: 24, durationSeconds: 1400, status: WatchStatus.Watching);
        var watchLog = new Dictionary<int, int> { [1] = 9 };

        var stats = RecapStatsBuilder.Build([entry], [entry], [], watchLog, RecapTimeFilter.Watched);

        Assert.Equal(9L * 1400, stats.TimeSpentSeconds);
    }
}
