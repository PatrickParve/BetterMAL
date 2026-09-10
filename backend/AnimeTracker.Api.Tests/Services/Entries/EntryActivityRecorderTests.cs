using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Entries;

namespace AnimeTracker.Api.Tests.Services.Entries;

// EntryActivityRecorder in isolation, asserting the exact ChangeDetail
// strings ActivityFeedComposer parses and the emission order
// FindCompletionScoreMerges depends on (design D2/D4/D5).
public class EntryActivityRecorderTests
{
    private static UserAnimeEntry EntryWith(WatchStatus status, int episodesWatched, int? myScore, DateOnly? startedAt, DateOnly? completedAt, int rewatchCount) =>
        new()
        {
            AnimeId = 1,
            Status = status,
            EpisodesWatched = episodesWatched,
            MyScore = myScore,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            RewatchCount = rewatchCount,
        };

    [Fact]
    public void ANewEntryRecordsOneAddedRowNamingItsStatus()
    {
        var now = DateTimeOffset.UtcNow;
        var log = EntryActivityRecorder.Added(1, WatchStatus.Watching, now);

        Assert.Equal(ActivityChangeType.Added, log.ChangeType);
        Assert.Equal("Added as Watching", log.ChangeDetail);
        Assert.Equal(now, log.Timestamp);
        Assert.Equal(1, log.AnimeId);
    }

    [Fact]
    public void NoChangeRecordsNothing()
    {
        var before = EntrySnapshot.Of(EntryWith(WatchStatus.Watching, 3, null, null, null, 0));
        var after = EntryWith(WatchStatus.Watching, 3, null, null, null, 0);

        var rows = EntryActivityRecorder.Diff(1, before, after, DateTimeOffset.UtcNow);

        Assert.Empty(rows);
    }

    [Fact]
    public void EachChangedFieldProducesOneRowWithTheExactChangeDetailStrings()
    {
        var before = EntrySnapshot.Of(EntryWith(WatchStatus.Watching, 3, null, null, null, 0));
        var after = EntryWith(WatchStatus.Dropped, 7, 8, new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1), 1);

        var rows = EntryActivityRecorder.Diff(1, before, after, DateTimeOffset.UtcNow);

        var episode = Assert.Single(rows, r => r.ChangeType == ActivityChangeType.EpisodeIncremented);
        Assert.Equal("Episode 7", episode.ChangeDetail);
        Assert.Equal(3, episode.PreviousEpisodesWatched);

        var status = Assert.Single(rows, r => r.ChangeType == ActivityChangeType.StatusChanged);
        Assert.Equal("Watching -> Dropped", status.ChangeDetail);

        var startDate = Assert.Single(rows, r => r.ChangeType == ActivityChangeType.StartDateChanged);
        Assert.Equal("Start date 2026-01-01", startDate.ChangeDetail);

        var finishDate = Assert.Single(rows, r => r.ChangeType == ActivityChangeType.FinishDateChanged);
        Assert.Equal("Finish date 2026-02-01", finishDate.ChangeDetail);

        var score = Assert.Single(rows, r => r.ChangeType == ActivityChangeType.ScoreChanged);
        Assert.Equal("Score 8", score.ChangeDetail);

        var rewatch = Assert.Single(rows, r => r.ChangeType == ActivityChangeType.RewatchCountChanged);
        Assert.Equal("Rewatch count 1", rewatch.ChangeDetail);
    }

    [Fact]
    public void ClearingScoreOrDatesRecordsTheClearedWording()
    {
        var before = EntrySnapshot.Of(EntryWith(WatchStatus.Watching, 3, 8, new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1), 0));
        var after = EntryWith(WatchStatus.Watching, 3, null, null, null, 0);

        var rows = EntryActivityRecorder.Diff(1, before, after, DateTimeOffset.UtcNow);

        Assert.Equal("Score cleared", Assert.Single(rows, r => r.ChangeType == ActivityChangeType.ScoreChanged).ChangeDetail);
        Assert.Equal("Start date cleared", Assert.Single(rows, r => r.ChangeType == ActivityChangeType.StartDateChanged).ChangeDetail);
        Assert.Equal("Finish date cleared", Assert.Single(rows, r => r.ChangeType == ActivityChangeType.FinishDateChanged).ChangeDetail);
    }

    [Fact]
    public void AStatusLandingOnCompletedIsRecordedAsACompletion()
    {
        var before = EntrySnapshot.Of(EntryWith(WatchStatus.Watching, 12, null, null, null, 0));
        var after = EntryWith(WatchStatus.Completed, 12, null, null, null, 0);

        var rows = EntryActivityRecorder.Diff(1, before, after, DateTimeOffset.UtcNow);

        var row = Assert.Single(rows);
        Assert.Equal(ActivityChangeType.Completed, row.ChangeType);
        Assert.Equal("Completed", row.ChangeDetail);
    }

    [Fact]
    public void EveryOtherStatusTransitionIsRecordedAsAStatusChange()
    {
        var before = EntrySnapshot.Of(EntryWith(WatchStatus.Watching, 3, null, null, null, 0));
        var after = EntryWith(WatchStatus.OnHold, 3, null, null, null, 0);

        var rows = EntryActivityRecorder.Diff(1, before, after, DateTimeOffset.UtcNow);

        var row = Assert.Single(rows);
        Assert.Equal(ActivityChangeType.StatusChanged, row.ChangeType);
        Assert.Equal("Watching -> OnHold", row.ChangeDetail);
    }

    [Fact]
    public void ALoweredEpisodeCountIsStillRecordedWithItsPreviousValue()
    {
        var before = EntrySnapshot.Of(EntryWith(WatchStatus.Watching, 10, null, null, null, 0));
        var after = EntryWith(WatchStatus.Watching, 4, null, null, null, 0);

        var rows = EntryActivityRecorder.Diff(1, before, after, DateTimeOffset.UtcNow);

        var row = Assert.Single(rows);
        Assert.Equal(ActivityChangeType.EpisodeIncremented, row.ChangeType);
        Assert.Equal("Episode 4", row.ChangeDetail);
        Assert.Equal(10, row.PreviousEpisodesWatched);
    }

    [Fact]
    public void ACompletionAppliedWithAScoreEmitsTheScoreRowAfterTheCompletionRow()
    {
        var before = EntrySnapshot.Of(EntryWith(WatchStatus.Watching, 12, null, null, null, 0));
        var after = EntryWith(WatchStatus.Completed, 12, 9, null, new DateOnly(2026, 3, 1), 0);

        var rows = EntryActivityRecorder.Diff(1, before, after, DateTimeOffset.UtcNow);

        var completedIndex = rows.FindIndex(r => r.ChangeType == ActivityChangeType.Completed);
        var scoreIndex = rows.FindIndex(r => r.ChangeType == ActivityChangeType.ScoreChanged);
        Assert.True(completedIndex >= 0 && scoreIndex >= 0);
        Assert.True(scoreIndex > completedIndex, "the score row must be emitted after the completion row so it takes the higher identity (design D5)");
    }

    [Fact]
    public void AddedProducesARowWithANonEmptyEventId()
    {
        var log = EntryActivityRecorder.Added(1, WatchStatus.Watching, DateTimeOffset.UtcNow);

        Assert.NotEqual(Guid.Empty, log.EventId);
    }

    [Fact]
    public void EveryRowDiffProducesCarriesAPairwiseDistinctNonEmptyEventId()
    {
        var before = EntrySnapshot.Of(EntryWith(WatchStatus.Watching, 3, null, null, null, 0));
        var after = EntryWith(WatchStatus.Dropped, 7, 8, new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1), 1);

        var rows = EntryActivityRecorder.Diff(1, before, after, DateTimeOffset.UtcNow);

        Assert.All(rows, r => Assert.NotEqual(Guid.Empty, r.EventId));
        Assert.Equal(rows.Count, rows.Select(r => r.EventId).Distinct().Count());
    }
}
