using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Recap;
using AnimeTracker.Api.Services.Scheduling;

namespace AnimeTracker.Api.Tests.Services.Recap;

// RecapService.ToRow's per-row MalRevealed flag (tasks.md 3.1): widened
// alongside score-visibility's always-show setting to also cover Dropped
// entries, not just Completed ones.
public class RecapServiceTests
{
    private static UserAnimeEntry Entry(int animeId, WatchStatus status, DateOnly completedAt, double malScore) =>
        new()
        {
            AnimeId = animeId,
            Anime = new AnimeMetadata { Id = animeId, Title = $"Anime {animeId}", MalScore = malScore },
            Status = status,
            CompletedAt = completedAt,
        };

    private static ActivityLog LogRow(int animeId, DateTimeOffset timestamp, int previousEpisodesWatched, int newEpisodesWatched) =>
        new()
        {
            AnimeId = animeId,
            Timestamp = timestamp,
            ChangeType = ActivityChangeType.EpisodeIncremented,
            ChangeDetail = $"Episode {newEpisodesWatched}",
            PreviousEpisodesWatched = previousEpisodesWatched,
        };

    [Fact]
    public async Task DroppedEntryRowIsRevealed()
    {
        var entries = new List<UserAnimeEntry>
        {
            Entry(1, WatchStatus.Dropped, new DateOnly(2021, 6, 1), 7.5),
        };
        var service = new RecapService(new FakeUserAnimeEntryRepository(entries), new FakeActivityLogRepository(), new FakeBroadcastLocalTimeConverter());

        var dto = await service.GetRecapAsync(RecapPeriod.Yearly(2021), RecapTimeFilter.Watched, CancellationToken.None);

        var row = Assert.Single(dto.Items);
        Assert.True(row.MalRevealed);
    }

    [Fact]
    public async Task WatchingEntryRowIsNotRevealed()
    {
        // The "aired" filter (unlike "watched") includes Watching entries
        // (RecapEntrySelector.AiredStatuses), so this isolates the reveal
        // flag itself from the filter's own status gate.
        var entry = Entry(1, WatchStatus.Watching, new DateOnly(2021, 6, 1), 7.5);
        entry.Anime.AiredFrom = new DateOnly(2021, 6, 1);
        var service = new RecapService(new FakeUserAnimeEntryRepository([entry]), new FakeActivityLogRepository(), new FakeBroadcastLocalTimeConverter());

        var dto = await service.GetRecapAsync(RecapPeriod.Yearly(2021), RecapTimeFilter.Aired, CancellationToken.None);

        var row = Assert.Single(dto.Items);
        Assert.False(row.MalRevealed);
    }

    [Fact]
    public async Task MultiYearCurrentlyWatchingSpansEveryYearOfTheRange()
    {
        var early = Entry(1, WatchStatus.Watching, new DateOnly(2020, 3, 1), 7.5);
        early.Anime.AiredFrom = new DateOnly(2020, 3, 1);
        var late = Entry(2, WatchStatus.Watching, new DateOnly(2024, 9, 1), 8.0);
        late.Anime.AiredFrom = new DateOnly(2024, 9, 1);
        var service = new RecapService(new FakeUserAnimeEntryRepository([early, late]), new FakeActivityLogRepository(), new FakeBroadcastLocalTimeConverter());

        var dto = await service.GetRecapAsync(RecapPeriod.MultiYear(2020, 2024), RecapTimeFilter.Aired, CancellationToken.None);

        Assert.Equal(2, dto.Stats.CurrentlyWatching);
    }

    // The logged-progress arm's per-period episode figure (design.md
    // decisions 3/5, tasks.md 5.4/5.8).

    [Fact]
    public async Task AFullWatchPlusARewatchInsideOnePeriodSumsToTwentyFourEpisodesInOnePlace()
    {
        var entry = Entry(1, WatchStatus.Completed, new DateOnly(2024, 3, 1), malScore: 8.0);
        entry.MyScore = 8;
        List<ActivityLog> logRows =
        [
            LogRow(1, new DateTimeOffset(2024, 1, 15, 12, 0, 0, TimeSpan.Zero), previousEpisodesWatched: 0, newEpisodesWatched: 12),
            LogRow(1, new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.Zero), previousEpisodesWatched: 0, newEpisodesWatched: 12),
        ];
        var service = new RecapService(
            new FakeUserAnimeEntryRepository([entry]), new FakeActivityLogRepository(logRows), new FakeBroadcastLocalTimeConverter());

        var dto = await service.GetRecapAsync(RecapPeriod.Yearly(2024), RecapTimeFilter.Watched, CancellationToken.None);

        Assert.Equal(24, dto.Stats.EpisodesWatched);
        Assert.Equal(1, dto.Stats.AnimeCounted);
        Assert.Equal(8.0, dto.Stats.MeanScore);
    }

    [Fact]
    public async Task AnAnimeWatchedAcrossANewYearAppearsInBothYearsWithEachYearsOwnEpisodes()
    {
        var entry = new UserAnimeEntry
        {
            AnimeId = 1,
            Anime = new AnimeMetadata { Id = 1, Title = "Anime 1" },
            Status = WatchStatus.Watching,
        };
        List<ActivityLog> logRows =
        [
            LogRow(1, new DateTimeOffset(2023, 12, 20, 12, 0, 0, TimeSpan.Zero), previousEpisodesWatched: 0, newEpisodesWatched: 8),
            LogRow(1, new DateTimeOffset(2024, 1, 10, 12, 0, 0, TimeSpan.Zero), previousEpisodesWatched: 8, newEpisodesWatched: 15),
        ];
        var service = new RecapService(
            new FakeUserAnimeEntryRepository([entry]), new FakeActivityLogRepository(logRows), new FakeBroadcastLocalTimeConverter());

        var dto2023 = await service.GetRecapAsync(RecapPeriod.Yearly(2023), RecapTimeFilter.Watched, CancellationToken.None);
        var dto2024 = await service.GetRecapAsync(RecapPeriod.Yearly(2024), RecapTimeFilter.Watched, CancellationToken.None);

        Assert.Equal(8, dto2023.Stats.EpisodesWatched);
        Assert.Contains(dto2023.Items, i => i.AnimeId == 1);
        Assert.Equal(7, dto2024.Stats.EpisodesWatched);
        Assert.Contains(dto2024.Items, i => i.AnimeId == 1);
    }

    [Fact]
    public async Task ALoggedDecreaseContributesNothing()
    {
        var entry = new UserAnimeEntry
        {
            AnimeId = 1,
            Anime = new AnimeMetadata { Id = 1, Title = "Anime 1" },
            Status = WatchStatus.Watching,
        };
        List<ActivityLog> logRows =
        [
            LogRow(1, new DateTimeOffset(2024, 3, 1, 12, 0, 0, TimeSpan.Zero), previousEpisodesWatched: 10, newEpisodesWatched: 3),
        ];
        var service = new RecapService(
            new FakeUserAnimeEntryRepository([entry]), new FakeActivityLogRepository(logRows), new FakeBroadcastLocalTimeConverter());

        var dto = await service.GetRecapAsync(RecapPeriod.Yearly(2024), RecapTimeFilter.Watched, CancellationToken.None);

        Assert.Empty(dto.Items);
    }

    [Fact]
    public async Task WatchedCountReflectsLoggedProgressAloneEvenUnderTheAiredFilter()
    {
        var entry = new UserAnimeEntry
        {
            AnimeId = 1,
            Anime = new AnimeMetadata { Id = 1, Title = "Anime 1" },
            Status = WatchStatus.Watching,
        };
        List<ActivityLog> logRows =
            [LogRow(1, new DateTimeOffset(2024, 3, 1, 12, 0, 0, TimeSpan.Zero), previousEpisodesWatched: 0, newEpisodesWatched: 5)];
        var service = new RecapService(
            new FakeUserAnimeEntryRepository([entry]), new FakeActivityLogRepository(logRows), new FakeBroadcastLocalTimeConverter());

        var dto = await service.GetRecapAsync(RecapPeriod.Yearly(2024), RecapTimeFilter.Aired, CancellationToken.None);

        Assert.Equal(1, dto.WatchedCount);
    }

    private sealed class FakeUserAnimeEntryRepository(List<UserAnimeEntry> entries) : IUserAnimeEntryRepository
    {
        public Task<UserAnimeEntry?> GetByAnimeIdAsync(int animeId, CancellationToken ct = default) =>
            Task.FromResult(entries.FirstOrDefault(e => e.AnimeId == animeId));

        public Task<List<UserAnimeEntry>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(entries);

        public Task<(int PendingCount, DateTimeOffset? LastSyncedAt)> GetSyncStatusAsync(CancellationToken ct = default) =>
            Task.FromResult((0, (DateTimeOffset?)null));
    }

    private sealed class FakeActivityLogRepository(List<ActivityLog>? rows = null) : IActivityLogRepository
    {
        private readonly List<ActivityLog> rows = rows ?? [];

        public Task<List<ActivityLog>> GetRecentAsync(int count, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<List<ActivityLog>> GetAllAsync(CancellationToken ct = default) => throw new NotImplementedException();

        public Task<List<ActivityLog>> GetEpisodeProgressInRangeAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct = default) =>
            Task.FromResult(rows
                .Where(r => r.ChangeType == ActivityChangeType.EpisodeIncremented && r.Timestamp >= fromUtc && r.Timestamp < toUtc)
                .OrderBy(r => r.Timestamp)
                .ToList());
    }

    private sealed class FakeBroadcastLocalTimeConverter : IBroadcastLocalTimeConverter
    {
        public DateOnly GetStartOfWeek(DateOnly referenceDate) => referenceDate;
        public DateOnly GetLocalDate(DateTimeOffset instantUtc) => DateOnly.FromDateTime(instantUtc.UtcDateTime);
        public TimeOnly GetLocalTime(DateTimeOffset instantUtc) => TimeOnly.FromDateTime(instantUtc.UtcDateTime);
        public DateTimeOffset LocalMidnightUtc(DateOnly localDate) => new(localDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
    }
}
