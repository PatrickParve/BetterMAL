using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Dashboard;
using AnimeTracker.Api.Services.Entries;
using AnimeTracker.Api.Services.Scheduling;

namespace AnimeTracker.Api.Tests.Services.Dashboard;

// main-dashboard "Rewatches appear in the currently-watching carousel"
// (tasks.md 5.9).
public class MainDashboardServiceRewatchingTests
{
    private static MainDashboardService CreateService(List<UserAnimeEntry> entries) =>
        new(new FakeUserAnimeEntryRepository(entries), new FakeEpisodeScheduleService(), new NullAiringWatchStatusService(), new FakeBroadcastLocalTimeConverter());

    [Fact]
    public async Task ARewatchAppearsInCurrentlyWatchingAlongsideWatching()
    {
        List<UserAnimeEntry> entries =
        [
            new() { AnimeId = 1, Status = WatchStatus.Watching, EpisodesWatched = 3, Anime = new AnimeMetadata { Id = 1, Title = "Watching" } },
            new() { AnimeId = 2, Status = WatchStatus.Rewatching, EpisodesWatched = 5, Anime = new AnimeMetadata { Id = 2, Title = "Rewatching" } },
        ];

        var dashboard = await CreateService(entries).GetDashboardAsync();

        Assert.Equal(2, dashboard.CurrentlyWatching.Count);
        var rewatch = dashboard.CurrentlyWatching.Single(i => i.AnimeId == 2);
        Assert.Equal(WatchStatus.Rewatching, rewatch.Status);
    }

    [Fact]
    public async Task ARewatchLeavesCurrentlyWatchingOnceItHasReCompleted()
    {
        List<UserAnimeEntry> entries =
        [
            new() { AnimeId = 1, Status = WatchStatus.Completed, EpisodesWatched = 24, Anime = new AnimeMetadata { Id = 1, Title = "Rewatch", TotalEpisodes = 24 } },
        ];

        var dashboard = await CreateService(entries).GetDashboardAsync();

        Assert.Empty(dashboard.CurrentlyWatching);
    }

    // gate-editing-on-aired-episodes task 5.8: the dashboard's
    // currently-watching items carry the raw airing status.
    [Fact]
    public async Task CurrentlyWatchingItemsCarryAiringStatus()
    {
        List<UserAnimeEntry> entries =
        [
            new() { AnimeId = 1, Status = WatchStatus.Watching, EpisodesWatched = 3, Anime = new AnimeMetadata { Id = 1, Title = "Airing", AiringStatus = "currently_airing" } },
        ];

        var dashboard = await CreateService(entries).GetDashboardAsync();

        Assert.Equal("currently_airing", Assert.Single(dashboard.CurrentlyWatching).AiringStatus);
    }

    // main-dashboard "A completion left in Currently watching can be undone
    // from its card" (tasks.md 1.3): the payload carries each item's score
    // and finish date, since the undo and the rewatch prompt gate both need
    // them.
    [Fact]
    public async Task CurrentlyWatchingItemsCarryScoreAndFinishDate()
    {
        List<UserAnimeEntry> entries =
        [
            new() { AnimeId = 1, Status = WatchStatus.Watching, EpisodesWatched = 3, MyScore = 7, CompletedAt = new DateOnly(2024, 1, 1), Anime = new AnimeMetadata { Id = 1, Title = "Scored" } },
            new() { AnimeId = 2, Status = WatchStatus.Watching, EpisodesWatched = 5, MyScore = null, CompletedAt = null, Anime = new AnimeMetadata { Id = 2, Title = "Unscored" } },
        ];

        var dashboard = await CreateService(entries).GetDashboardAsync();

        var scored = dashboard.CurrentlyWatching.Single(i => i.AnimeId == 1);
        Assert.Equal(7, scored.MyScore);
        Assert.Equal(new DateOnly(2024, 1, 1), scored.CompletedAt);

        var unscored = dashboard.CurrentlyWatching.Single(i => i.AnimeId == 2);
        Assert.Null(unscored.MyScore);
        Assert.Null(unscored.CompletedAt);
    }

    private sealed class NullAiringWatchStatusService : IAiringWatchStatusService
    {
        public Task SettleAsync(IReadOnlyCollection<UserAnimeEntry> entries, IReadOnlyDictionary<int, int> airedSoFarByAnimeId, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeUserAnimeEntryRepository(List<UserAnimeEntry> entries) : IUserAnimeEntryRepository
    {
        public Task<UserAnimeEntry?> GetByAnimeIdAsync(int animeId, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<List<UserAnimeEntry>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(entries);
        public Task<(int PendingCount, int HeldCount, DateTimeOffset? LastSyncedAt)> GetSyncStatusAsync(CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    private sealed class FakeEpisodeScheduleService : IEpisodeScheduleService
    {
        public Task<ResolvedEpisode?> ResolveOnLocalDateAsync(AnimeMetadata anime, DateOnly localDate, CancellationToken ct = default) =>
            Task.FromResult<ResolvedEpisode?>(null);
        public Task<DateTimeOffset?> NextAiringInstantAsync(AnimeMetadata anime, DateTimeOffset afterUtc, CancellationToken ct = default) =>
            Task.FromResult<DateTimeOffset?>(null);
        public Task<int?> EpisodesAiredAsOfAsync(AnimeMetadata anime, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            Task.FromResult<int?>(null);
        public Task<Dictionary<int, int>> EpisodesAiredAsOfAsync(IReadOnlyCollection<AnimeMetadata> anime, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            Task.FromResult(new Dictionary<int, int>());
        public Task<Dictionary<int, int>> EpisodesAiredAsOfAsync(IReadOnlyCollection<int> animeIds, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            Task.FromResult(new Dictionary<int, int>());
    }

    private sealed class FakeBroadcastLocalTimeConverter : IBroadcastLocalTimeConverter
    {
        public DateOnly GetStartOfWeek(DateOnly referenceDate) => referenceDate;
        public DateOnly GetLocalDate(DateTimeOffset instantUtc) => DateOnly.FromDateTime(instantUtc.UtcDateTime);
        public TimeOnly GetLocalTime(DateTimeOffset instantUtc) => TimeOnly.FromDateTime(instantUtc.UtcDateTime);
        public DateTimeOffset LocalMidnightUtc(DateOnly localDate) =>
            new(localDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        public (DayOfWeek LocalDayOfWeek, TimeOnly LocalTime) ConvertBroadcastSlot(DayOfWeek jstDayOfWeek, TimeOnly jstTime, DateTimeOffset referenceUtc) =>
            (jstDayOfWeek, jstTime);
    }
}
