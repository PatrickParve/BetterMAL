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

    // Ten entries with both scores, evenly spread, clear the shared helper's
    // minimum-rated-pairs guard and give both scales a non-zero SD.
    private static List<UserAnimeEntry> WholeListWithSpread() =>
        Enumerable.Range(1, 10)
            .Select(id => Entry(id, "tv", 12, myScore: 1 + id % 10, malScore: 4.0 + id * 0.3))
            .ToList();

    [Fact]
    public void HotTakesDegradeToTwoWhenOnlyTwoEligible()
    {
        var wholeList = WholeListWithSpread();
        var included = wholeList.Take(2).ToList();

        var stats = RecapStatsBuilder.Build(included, wholeList);

        Assert.Equal(2, stats.HotTakes.Count);
    }

    [Fact]
    public void HotTakesDegradeToOneWhenOnlyOneEligible()
    {
        var wholeList = WholeListWithSpread();
        var included = wholeList.Take(1).ToList();

        var stats = RecapStatsBuilder.Build(included, wholeList);

        Assert.Single(stats.HotTakes);
    }

    [Fact]
    public void HotTakesAreEmptyWhenNoneEligible()
    {
        var wholeList = WholeListWithSpread();
        var unscored = Entry(99, "tv", 12, myScore: null, malScore: 7.0);

        var stats = RecapStatsBuilder.Build([unscored], wholeList);

        Assert.Empty(stats.HotTakes);
    }

    [Fact]
    public void HotTakesCapAtThreeOrderedByAbsoluteDivergence()
    {
        var wholeList = WholeListWithSpread();

        var stats = RecapStatsBuilder.Build(wholeList, wholeList);

        Assert.True(stats.HotTakes.Count <= 3);
        var divergences = stats.HotTakes.Select(h => Math.Abs(h.Divergence)).ToList();
        Assert.Equal(divergences.OrderByDescending(d => d), divergences);
    }
}
