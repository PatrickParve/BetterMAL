using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Series;

namespace AnimeTracker.Api.Tests.Services.Series;

public class SeriesServiceBuildStatsTests
{
    private static AnimeMetadata Anime(
        int id, int? totalEpisodes, int? averageEpisodeDurationSeconds = null, int? rewatchCount = null,
        double? myScore = null, WatchStatus? status = null) =>
        new()
        {
            Id = id,
            Title = $"Anime {id}",
            TotalEpisodes = totalEpisodes,
            AverageEpisodeDurationSeconds = averageEpisodeDurationSeconds,
            UserEntry = rewatchCount is null && myScore is null && status is null
                ? null
                : new UserAnimeEntry
                {
                    AnimeId = id,
                    RewatchCount = rewatchCount ?? 0,
                    MyScore = myScore is { } s ? (int)s : null,
                    Status = status ?? WatchStatus.Watching,
                },
        };

    private static SeriesMember Member(AnimeMetadata anime, bool isMainLine, int order) =>
        new() { AnimeId = anime.Id, SeriesId = 1, IsMainLine = isMainLine, Order = order, Anime = anime };

    [Fact]
    public void UnknownTotalWithKnownAiredCountContributesAiredCount()
    {
        var airing = Anime(1, totalEpisodes: null, averageEpisodeDurationSeconds: 1500); // 25 min
        var members = new List<SeriesMember> { Member(airing, isMainLine: true, order: 0) };
        var airedByAnimeId = new Dictionary<int, int?> { [1] = 1100 };

        var stats = SeriesService.BuildStats(members, [], [airing], mainLineAiredEpisodes: 1100, airedByAnimeId);

        Assert.Equal(1100, stats.MainLineEpisodeTotal);
        Assert.Equal(1100L * 1500, stats.MainLineRuntimeSeconds);
        Assert.True(stats.HasUnknownEpisodeCounts);
    }

    [Fact]
    public void UnknownTotalWithNoAiredCountContributesZero()
    {
        var notYetAired = Anime(1, totalEpisodes: null, averageEpisodeDurationSeconds: 1500);
        var members = new List<SeriesMember> { Member(notYetAired, isMainLine: true, order: 0) };
        var airedByAnimeId = new Dictionary<int, int?> { [1] = null };

        var stats = SeriesService.BuildStats(members, [], [notYetAired], mainLineAiredEpisodes: 0, airedByAnimeId);

        Assert.Equal(0, stats.MainLineEpisodeTotal);
        Assert.Equal(0L, stats.MainLineRuntimeSeconds);
        Assert.True(stats.HasUnknownEpisodeCounts);
    }

    [Fact]
    public void KnownTotalDoesNotFlipHasUnknown()
    {
        var finished = Anime(1, totalEpisodes: 12, averageEpisodeDurationSeconds: 1500);
        var members = new List<SeriesMember> { Member(finished, isMainLine: true, order: 0) };
        var airedByAnimeId = new Dictionary<int, int?> { [1] = 12 };

        var stats = SeriesService.BuildStats(members, [], [finished], mainLineAiredEpisodes: 12, airedByAnimeId);

        Assert.Equal(12, stats.MainLineEpisodeTotal);
        Assert.False(stats.HasUnknownEpisodeCounts);
    }

    [Fact]
    public void NoRewatchedMemberYieldsEmptyMostRewatchedList()
    {
        var a = Anime(1, totalEpisodes: 12, rewatchCount: 0);
        var members = new List<SeriesMember> { Member(a, isMainLine: true, order: 0) };

        var stats = SeriesService.BuildStats(members, [], [a], mainLineAiredEpisodes: 12, new Dictionary<int, int?> { [1] = 12 });

        Assert.Empty(stats.MostRewatchedAnimeIds);
    }

    [Fact]
    public void OneRewatchedMemberIsNamed()
    {
        var a = Anime(1, totalEpisodes: 12, rewatchCount: 3);
        var b = Anime(2, totalEpisodes: 12, rewatchCount: 0);
        var members = new List<SeriesMember> { Member(a, isMainLine: true, order: 0), Member(b, isMainLine: true, order: 1) };
        var airedByAnimeId = new Dictionary<int, int?> { [1] = 12, [2] = 12 };

        var stats = SeriesService.BuildStats(members, [], [a, b], mainLineAiredEpisodes: 24, airedByAnimeId);

        Assert.Equal([1], stats.MostRewatchedAnimeIds);
    }

    [Fact]
    public void ExtrasCompletedCountsOnlyCompletedExtrasSeparatelyFromMainLine()
    {
        var mainLine = Anime(1, totalEpisodes: 12, status: WatchStatus.Completed);
        var completedExtra = Anime(2, totalEpisodes: 1, status: WatchStatus.Completed);
        var watchingExtra = Anime(3, totalEpisodes: 1, status: WatchStatus.Watching);
        var mainLineMembers = new List<SeriesMember> { Member(mainLine, isMainLine: true, order: 0) };
        var extraMembers = new List<SeriesMember>
        {
            Member(completedExtra, isMainLine: false, order: 0),
            Member(watchingExtra, isMainLine: false, order: 1),
        };
        var airedByAnimeId = new Dictionary<int, int?> { [1] = 12, [2] = 1, [3] = 1 };

        var stats = SeriesService.BuildStats(
            mainLineMembers, extraMembers, [mainLine, completedExtra, watchingExtra],
            mainLineAiredEpisodes: 12, airedByAnimeId);

        Assert.Equal(1, stats.EntriesCompleted);
        Assert.Equal(1, stats.ExtrasCompleted);
    }

    [Fact]
    public void ExtrasCompletedIsZeroForSeriesWithNoExtras()
    {
        var mainLine = Anime(1, totalEpisodes: 12, status: WatchStatus.Completed);
        var mainLineMembers = new List<SeriesMember> { Member(mainLine, isMainLine: true, order: 0) };

        var stats = SeriesService.BuildStats(
            mainLineMembers, [], [mainLine], mainLineAiredEpisodes: 12, new Dictionary<int, int?> { [1] = 12 });

        Assert.Equal(1, stats.EntriesCompleted);
        Assert.Equal(0, stats.ExtrasCompleted);
    }

    [Fact]
    public void TiedRewatchCountsListBothInWatchOrder()
    {
        var a = Anime(1, totalEpisodes: 12, rewatchCount: 2);
        var b = Anime(2, totalEpisodes: 12, rewatchCount: 2);
        var members = new List<SeriesMember> { Member(a, isMainLine: true, order: 0), Member(b, isMainLine: true, order: 1) };
        var airedByAnimeId = new Dictionary<int, int?> { [1] = 12, [2] = 12 };

        var stats = SeriesService.BuildStats(members, [], [a, b], mainLineAiredEpisodes: 24, airedByAnimeId);

        Assert.Equal([1, 2], stats.MostRewatchedAnimeIds);
    }
}
