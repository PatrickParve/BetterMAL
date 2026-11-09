using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Recap;

namespace AnimeTracker.Api.Tests.Services.Recap;

// RecapWatchLog keeps its own rule (the sum of the raises, for the recap's selection and stat block)
// while the repository now also returns the rows that start a viewing for the watching days: those
// rows must add nothing to it.
public class RecapWatchLogTests
{
    private static readonly DateTimeOffset At = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RowsThatAreNotEpisodeProgressContributeNothing()
    {
        List<ActivityLog> rows =
        [
            new() { AnimeId = 1, Timestamp = At, ChangeType = ActivityChangeType.EpisodeIncremented, ChangeDetail = "Episode 3", PreviousEpisodesWatched = 0 },
            new() { AnimeId = 1, Timestamp = At.AddMinutes(1), ChangeType = ActivityChangeType.StatusChanged, ChangeDetail = "Completed -> Rewatching" },
            new() { AnimeId = 1, Timestamp = At.AddMinutes(2), ChangeType = ActivityChangeType.Removed, ChangeDetail = "Removed from list" },
            // A detail that merely reads like episode progress, on a row of another kind, is still not progress.
            new() { AnimeId = 2, Timestamp = At, ChangeType = ActivityChangeType.StatusChanged, ChangeDetail = "Episode 9", PreviousEpisodesWatched = 0 },
        ];

        var map = RecapWatchLog.BuildMap(rows);

        Assert.Equal([(1, 3)], map.Select(p => (p.Key, p.Value)));
    }

    [Fact]
    public void EveryRaiseCountsWhateverTheLoweringsBetweenThem()
    {
        // The recap's stat block keeps summing raises: 7 to 10, 10 to 9, 9 to 10 is 4.
        List<ActivityLog> rows =
        [
            new() { AnimeId = 1, Timestamp = At, ChangeType = ActivityChangeType.EpisodeIncremented, ChangeDetail = "Episode 10", PreviousEpisodesWatched = 7 },
            new() { AnimeId = 1, Timestamp = At.AddMinutes(1), ChangeType = ActivityChangeType.EpisodeIncremented, ChangeDetail = "Episode 9", PreviousEpisodesWatched = 10 },
            new() { AnimeId = 1, Timestamp = At.AddMinutes(2), ChangeType = ActivityChangeType.EpisodeIncremented, ChangeDetail = "Episode 10", PreviousEpisodesWatched = 9 },
        ];

        Assert.Equal(4, RecapWatchLog.BuildMap(rows)[1]);
    }
}
