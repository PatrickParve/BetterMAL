using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Series;

namespace AnimeTracker.Api.Tests.Services.Series;

public class SeriesServiceBuildStatsTests
{
    private static AnimeMetadata Anime(
        int id, int? totalEpisodes, int? averageEpisodeDurationSeconds = null, int? rewatchCount = null,
        double? myScore = null, WatchStatus? status = null, int? episodesWatched = null) =>
        new()
        {
            Id = id,
            Title = $"Anime {id}",
            TotalEpisodes = totalEpisodes,
            AverageEpisodeDurationSeconds = averageEpisodeDurationSeconds,
            UserEntry = rewatchCount is null && myScore is null && status is null && episodesWatched is null
                ? null
                : new UserAnimeEntry
                {
                    AnimeId = id,
                    RewatchCount = rewatchCount ?? 0,
                    MyScore = myScore is { } s ? (int)s : null,
                    Status = status ?? WatchStatus.Watching,
                    EpisodesWatched = episodesWatched ?? 0,
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

        var stats = SeriesService.BuildStats(members, members, [], airedByAnimeId);

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

        var stats = SeriesService.BuildStats(members, members, [], airedByAnimeId);

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

        var stats = SeriesService.BuildStats(members, members, [], airedByAnimeId);

        Assert.Equal(12, stats.MainLineEpisodeTotal);
        Assert.False(stats.HasUnknownEpisodeCounts);
    }

    [Fact]
    public void NoRewatchedMemberYieldsEmptyMostRewatchedList()
    {
        var a = Anime(1, totalEpisodes: 12, rewatchCount: 0);
        var members = new List<SeriesMember> { Member(a, isMainLine: true, order: 0) };

        var stats = SeriesService.BuildStats(members, members, [], new Dictionary<int, int?> { [1] = 12 });

        Assert.Empty(stats.MostRewatchedAnimeIds);
    }

    [Fact]
    public void OneRewatchedMemberIsNamed()
    {
        var a = Anime(1, totalEpisodes: 12, rewatchCount: 3);
        var b = Anime(2, totalEpisodes: 12, rewatchCount: 0);
        var members = new List<SeriesMember> { Member(a, isMainLine: true, order: 0), Member(b, isMainLine: true, order: 1) };
        var airedByAnimeId = new Dictionary<int, int?> { [1] = 12, [2] = 12 };

        var stats = SeriesService.BuildStats(members, members, [], airedByAnimeId);

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

        var stats = SeriesService.BuildStats(mainLineMembers, mainLineMembers, extraMembers, airedByAnimeId);

        Assert.Equal(1, stats.EntriesCompleted);
        Assert.Equal(1, stats.ExtrasCompleted);
    }

    [Fact]
    public void ExtrasCompletedIsZeroForSeriesWithNoExtras()
    {
        var mainLine = Anime(1, totalEpisodes: 12, status: WatchStatus.Completed);
        var mainLineMembers = new List<SeriesMember> { Member(mainLine, isMainLine: true, order: 0) };

        var stats = SeriesService.BuildStats(mainLineMembers, mainLineMembers, [], new Dictionary<int, int?> { [1] = 12 });

        Assert.Equal(1, stats.EntriesCompleted);
        Assert.Equal(0, stats.ExtrasCompleted);
    }

    // score-visibility widens SeriesAverages.MainLineSettledByMe to treat
    // Dropped as settled, and BuildStats' MainLineCompletedByMe DTO field
    // follows that widening — but EntriesCompleted/ExtrasCompleted, which
    // back the header's Completed/Caught up badge, stay completed-only
    // (design.md Non-Goals). A rename touches code near the badge, so this
    // pins both behaviours in one place (design risk 2).
    [Fact]
    public void MainLineCompletedByMeWidensToDroppedAfterTheRename()
    {
        var completed = Anime(1, totalEpisodes: 12, status: WatchStatus.Completed);
        completed.AiringStatus = "finished_airing";
        var dropped = Anime(2, totalEpisodes: 12, status: WatchStatus.Dropped);
        dropped.AiringStatus = "finished_airing";
        var mainLineMembers = new List<SeriesMember>
        {
            Member(completed, isMainLine: true, order: 0),
            Member(dropped, isMainLine: true, order: 1),
        };
        var airedByAnimeId = new Dictionary<int, int?> { [1] = 12, [2] = 12 };

        var stats = SeriesService.BuildStats(mainLineMembers, mainLineMembers, [], airedByAnimeId);

        Assert.True(stats.MainLineCompletedByMe);
    }

    [Fact]
    public void EntriesCompletedStaysCompletedOnlyAfterTheRename()
    {
        var completed = Anime(1, totalEpisodes: 12, status: WatchStatus.Completed);
        completed.AiringStatus = "finished_airing";
        var dropped = Anime(2, totalEpisodes: 12, status: WatchStatus.Dropped);
        dropped.AiringStatus = "finished_airing";
        var mainLineMembers = new List<SeriesMember>
        {
            Member(completed, isMainLine: true, order: 0),
            Member(dropped, isMainLine: true, order: 1),
        };
        var airedByAnimeId = new Dictionary<int, int?> { [1] = 12, [2] = 12 };

        var stats = SeriesService.BuildStats(mainLineMembers, mainLineMembers, [], airedByAnimeId);

        Assert.Equal(1, stats.EntriesCompleted);
    }

    // --- A Rewatching entry counts as fully watched (polish-rewatch design.md D2, tasks.md 3.7) ---

    [Fact]
    public void RewatchingEntryKeepsTheProgressBarFull()
    {
        var finished = Anime(1, totalEpisodes: 50, status: WatchStatus.Completed, episodesWatched: 50);
        var rewatching = Anime(2, totalEpisodes: 12, status: WatchStatus.Rewatching, episodesWatched: 2);
        var members = new List<SeriesMember> { Member(finished, isMainLine: true, order: 0), Member(rewatching, isMainLine: true, order: 1) };
        var airedByAnimeId = new Dictionary<int, int?> { [1] = 50, [2] = 12 }; // both fully aired

        var stats = SeriesService.BuildStats(members, members, [], airedByAnimeId);

        // 50 (Completed) + 12 (Rewatching, effective = max(2 watched, 12 aired)) = 62, not 52.
        Assert.Equal(62, stats.MyWatchedEpisodes);
    }

    [Fact]
    public void EntriesCompletedCountsARewatchingEntry()
    {
        var completed = Anime(1, totalEpisodes: 12, status: WatchStatus.Completed, episodesWatched: 12);
        var rewatching = Anime(2, totalEpisodes: 12, status: WatchStatus.Rewatching, episodesWatched: 2);
        var members = new List<SeriesMember> { Member(completed, isMainLine: true, order: 0), Member(rewatching, isMainLine: true, order: 1) };
        var airedByAnimeId = new Dictionary<int, int?> { [1] = 12, [2] = 12 };

        var stats = SeriesService.BuildStats(members, members, [], airedByAnimeId);

        Assert.Equal(2, stats.EntriesCompleted);
        Assert.Equal(2, stats.MainLineCount);
    }

    [Fact]
    public void EpisodeTotalIsUnmovedByARewatch()
    {
        // Rewatching, watched reset to 2, but 5 episodes have aired so far —
        // the episode total/runtime describe the anime and must stay at 12,
        // independent of the watched figure (which reads 5, not 2 or 12).
        var rewatching = Anime(1, totalEpisodes: 12, averageEpisodeDurationSeconds: 1500, status: WatchStatus.Rewatching, episodesWatched: 2);
        var members = new List<SeriesMember> { Member(rewatching, isMainLine: true, order: 0) };
        var airedByAnimeId = new Dictionary<int, int?> { [1] = 5 };

        var stats = SeriesService.BuildStats(members, members, [], airedByAnimeId);

        Assert.Equal(12, stats.MainLineEpisodeTotal);
        Assert.Equal(12L * 1500, stats.MainLineRuntimeSeconds);
        Assert.Equal(5, stats.MyWatchedEpisodes);
    }

    [Fact]
    public void TiedRewatchCountsListBothInWatchOrder()
    {
        var a = Anime(1, totalEpisodes: 12, rewatchCount: 2);
        var b = Anime(2, totalEpisodes: 12, rewatchCount: 2);
        var members = new List<SeriesMember> { Member(a, isMainLine: true, order: 0), Member(b, isMainLine: true, order: 1) };
        var airedByAnimeId = new Dictionary<int, int?> { [1] = 12, [2] = 12 };

        var stats = SeriesService.BuildStats(members, members, [], airedByAnimeId);

        Assert.Equal([1, 2], stats.MostRewatchedAnimeIds);
    }

    // --- MyRewatchedSeconds is orthogonal to MyWatchedSeconds (design.md D10, tasks.md 8.4) ---

    [Fact]
    public void CompletedSeasonWithRewatchesContributesTwoFullRuns()
    {
        var completed = Anime(1, totalEpisodes: 12, status: WatchStatus.Completed, episodesWatched: 12, rewatchCount: 2);
        var members = new List<SeriesMember> { Member(completed, isMainLine: true, order: 0) };
        var airedByAnimeId = new Dictionary<int, int?> { [1] = 12 };

        var stats = SeriesService.BuildStats(members, members, [], airedByAnimeId);

        // 2 completed rewatches of a 12-episode season = 24 rewatch episodes.
        Assert.Equal(24, stats.MyRewatchedSeconds / EpisodeSeconds(completed));
    }

    [Fact]
    public void RewatchInProgressAddsPartialRunOnTopOfCompletedRuns()
    {
        var rewatching = Anime(1, totalEpisodes: 12, status: WatchStatus.Rewatching, episodesWatched: 2, rewatchCount: 2);
        var members = new List<SeriesMember> { Member(rewatching, isMainLine: true, order: 0) };
        var airedByAnimeId = new Dictionary<int, int?> { [1] = 12 }; // fully aired

        var stats = SeriesService.BuildStats(members, members, [], airedByAnimeId);

        // 2 completed rewatches (24) + 2 episodes into a third (2) = 26 rewatch episodes.
        Assert.Equal(26, stats.MyRewatchedSeconds / EpisodeSeconds(rewatching));
        // effective watched = max(2 watched, 12 aired) = 12, unchanged by the rewatch count.
        Assert.Equal(12, stats.MyWatchedEpisodes);
        // 26 rewatch + 12 watched = 38 total episodes of time watched.
        Assert.Equal(38, stats.MyRewatchedSeconds / EpisodeSeconds(rewatching) + stats.MyWatchedEpisodes);
    }

    [Fact]
    public void ExtraRewatchCountContributesNothingToSeriesRewatchedTime()
    {
        var mainLine = Anime(1, totalEpisodes: 12, status: WatchStatus.Completed, episodesWatched: 12);
        var extra = Anime(2, totalEpisodes: 1, status: WatchStatus.Completed, episodesWatched: 1, rewatchCount: 3);
        var mainLineMembers = new List<SeriesMember> { Member(mainLine, isMainLine: true, order: 0) };
        var extraMembers = new List<SeriesMember> { Member(extra, isMainLine: false, order: 0) };
        var airedByAnimeId = new Dictionary<int, int?> { [1] = 12, [2] = 1 };

        var stats = SeriesService.BuildStats(mainLineMembers, mainLineMembers, extraMembers, airedByAnimeId);

        Assert.Equal(0L, stats.MyRewatchedSeconds);
    }

    [Fact]
    public void MyWatchedSecondsAndEpisodesAreUnchangedByARewatch()
    {
        var completed = Anime(1, totalEpisodes: 12, averageEpisodeDurationSeconds: 1500, status: WatchStatus.Completed, episodesWatched: 12, rewatchCount: 2);
        var members = new List<SeriesMember> { Member(completed, isMainLine: true, order: 0) };
        var airedByAnimeId = new Dictionary<int, int?> { [1] = 12 };

        var stats = SeriesService.BuildStats(members, members, [], airedByAnimeId);

        Assert.Equal(12, stats.MyWatchedEpisodes);
        Assert.Equal(12L * 1500, stats.MyWatchedSeconds);
    }

    // --- Studios/Genres are scoped to the visible main line, not every member (design.md D3, tasks.md 3.1) ---

    [Fact]
    public void StudiosAndGenresExcludeExtras()
    {
        var mainLine = Anime(1, totalEpisodes: 12);
        mainLine.Studio = "Main Studio";
        mainLine.Genres = ["Action"];
        var extra = Anime(2, totalEpisodes: 1);
        extra.Studio = "Extra Studio";
        extra.Genres = ["Comedy"];
        var mainLineMembers = new List<SeriesMember> { Member(mainLine, isMainLine: true, order: 0) };
        var extraMembers = new List<SeriesMember> { Member(extra, isMainLine: false, order: 0) };
        var airedByAnimeId = new Dictionary<int, int?> { [1] = 12, [2] = 1 };

        var stats = SeriesService.BuildStats(mainLineMembers, mainLineMembers, extraMembers, airedByAnimeId);

        Assert.Equal(["Main Studio"], stats.Studios);
        Assert.Equal(["Action"], stats.Genres);
    }

    [Fact]
    public void StudiosAndGenresFollowTheVisibleMainLineOnAVersionSlotPick()
    {
        var alternativeA = Anime(1, totalEpisodes: 12);
        alternativeA.Studio = "Studio A";
        alternativeA.Genres = ["Action"];
        var alternativeB = Anime(2, totalEpisodes: 12);
        alternativeB.Studio = "Studio B";
        alternativeB.Genres = ["Comedy"];
        var mainLineMembers = new List<SeriesMember>
        {
            Member(alternativeA, isMainLine: true, order: 0),
            Member(alternativeB, isMainLine: true, order: 1),
        };
        var airedByAnimeId = new Dictionary<int, int?> { [1] = 12, [2] = 12 };

        var statsWithA = SeriesService.BuildStats(mainLineMembers, [mainLineMembers[0]], [], airedByAnimeId);
        var statsWithB = SeriesService.BuildStats(mainLineMembers, [mainLineMembers[1]], [], airedByAnimeId);

        Assert.Equal(["Studio A"], statsWithA.Studios);
        Assert.Equal(["Action"], statsWithA.Genres);
        Assert.Equal(["Studio B"], statsWithB.Studios);
        Assert.Equal(["Comedy"], statsWithB.Genres);
    }

    private static int EpisodeSeconds(AnimeMetadata anime) => anime.AverageEpisodeDurationSeconds ?? 24 * 60;
}
