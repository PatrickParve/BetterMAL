using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Dashboard;
using AnimeTracker.Api.Services.Entries;
using AnimeTracker.Api.Services.Scheduling;
using AnimeTracker.Api.Services.Updates;

namespace AnimeTracker.Api.Tests.Services.Dashboard;

// design.md D7 (refine-sync-status-and-episode-totals tasks.md 6.2/9.7): the
// Currently watching carousel drops an entry that has watched everything
// aired so far — no airing-status condition, so a finished-but-mislabelled
// show is filtered exactly like a genuinely airing one, and the entry stays
// Watching everywhere else (this is a display-only filter, not a write).
public class MainDashboardServiceCaughtUpTests
{
    private static MainDashboardService CreateService(List<UserAnimeEntry> entries, Dictionary<int, int> airedSoFarByAnimeId) =>
        new(new FakeUserAnimeEntryRepository(entries), new FakeEpisodeScheduleService(airedSoFarByAnimeId),
            new NullAiringWatchStatusService(), new FakeBroadcastLocalTimeConverter(), new FakeAnimeUpdateService());

    private static UserAnimeEntry Entry(int animeId, WatchStatus status, int episodesWatched, string? airingStatus = "currently_airing") =>
        new()
        {
            AnimeId = animeId,
            Status = status,
            EpisodesWatched = episodesWatched,
            Anime = new AnimeMetadata { Id = animeId, Title = $"Anime {animeId}", AiringStatus = airingStatus },
        };

    [Theory]
    [InlineData("currently_airing")]
    [InlineData("finished_airing")]
    public async Task ACaughtUpEntryIsAbsentFromCurrentlyWatchingRegardlessOfAiringStatus(string airingStatus)
    {
        var entries = new List<UserAnimeEntry> { Entry(1, WatchStatus.Watching, episodesWatched: 7, airingStatus) };
        var dashboard = await CreateService(entries, new Dictionary<int, int> { [1] = 7 }).GetDashboardAsync();

        Assert.Empty(dashboard.CurrentlyWatching);
    }

    [Fact]
    public async Task ACaughtUpCurrentlyAiringEntryIsStillPresentInCurrentSeason()
    {
        var entries = new List<UserAnimeEntry> { Entry(1, WatchStatus.Watching, episodesWatched: 7) };
        var dashboard = await CreateService(entries, new Dictionary<int, int> { [1] = 7 }).GetDashboardAsync();

        Assert.Contains(dashboard.CurrentSeason, i => i.AnimeId == 1);
    }

    [Fact]
    public async Task ItReturnsToCurrentlyWatchingOnceTheAiredCountGrows()
    {
        var entries = new List<UserAnimeEntry> { Entry(1, WatchStatus.Watching, episodesWatched: 7) };
        var dashboard = await CreateService(entries, new Dictionary<int, int> { [1] = 8 }).GetDashboardAsync();

        Assert.Contains(dashboard.CurrentlyWatching, i => i.AnimeId == 1);
    }

    [Fact]
    public async Task AnUnknownAiredCountIsNeverFilteredOut()
    {
        var entries = new List<UserAnimeEntry> { Entry(1, WatchStatus.Watching, episodesWatched: 7) };
        var dashboard = await CreateService(entries, new Dictionary<int, int>()).GetDashboardAsync(); // no entry for anime 1

        Assert.Contains(dashboard.CurrentlyWatching, i => i.AnimeId == 1);
    }

    [Fact]
    public async Task APartWayRewatchIsKept()
    {
        var entries = new List<UserAnimeEntry> { Entry(1, WatchStatus.Rewatching, episodesWatched: 3) };
        var dashboard = await CreateService(entries, new Dictionary<int, int> { [1] = 7 }).GetDashboardAsync();

        Assert.Contains(dashboard.CurrentlyWatching, i => i.AnimeId == 1);
    }

    [Fact]
    public async Task TheFilterWritesNothingToTheEntryItself()
    {
        var entry = Entry(1, WatchStatus.Watching, episodesWatched: 7);
        var entries = new List<UserAnimeEntry> { entry };

        await CreateService(entries, new Dictionary<int, int> { [1] = 7 }).GetDashboardAsync();

        Assert.Equal(WatchStatus.Watching, entry.Status);
        Assert.False(entry.PendingSync);
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
        public Task<(int PendingCount, DateTimeOffset? LastSyncedAt)> GetSyncStatusAsync(CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    private sealed class FakeEpisodeScheduleService(Dictionary<int, int> airedSoFarByAnimeId) : IEpisodeScheduleService
    {
        public Task<ResolvedEpisode?> ResolveOnLocalDateAsync(AnimeMetadata anime, DateOnly localDate, CancellationToken ct = default) =>
            Task.FromResult<ResolvedEpisode?>(null);
        public Task<DateTimeOffset?> NextAiringInstantAsync(AnimeMetadata anime, DateTimeOffset afterUtc, CancellationToken ct = default) =>
            Task.FromResult<DateTimeOffset?>(null);
        public Task<int?> EpisodesAiredAsOfAsync(AnimeMetadata anime, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            Task.FromResult(airedSoFarByAnimeId.TryGetValue(anime.Id, out var v) ? (int?)v : null);
        public Task<Dictionary<int, int>> EpisodesAiredAsOfAsync(IReadOnlyCollection<AnimeMetadata> anime, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            Task.FromResult(anime.Where(a => airedSoFarByAnimeId.ContainsKey(a.Id)).ToDictionary(a => a.Id, a => airedSoFarByAnimeId[a.Id]));
        public Task<Dictionary<int, int>> EpisodesAiredAsOfAsync(IReadOnlyCollection<int> animeIds, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            Task.FromResult(animeIds.Where(airedSoFarByAnimeId.ContainsKey).ToDictionary(id => id, id => airedSoFarByAnimeId[id]));
    }

    private sealed class FakeBroadcastLocalTimeConverter : IBroadcastLocalTimeConverter
    {
        public DateOnly GetStartOfWeek(DateOnly referenceDate) => referenceDate;
        public DateOnly GetLocalDate(DateTimeOffset instantUtc) => DateOnly.FromDateTime(instantUtc.UtcDateTime);
        public TimeOnly GetLocalTime(DateTimeOffset instantUtc) => TimeOnly.FromDateTime(instantUtc.UtcDateTime);
        public DateTimeOffset LocalMidnightUtc(DateOnly localDate) => new(localDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        public (DayOfWeek LocalDayOfWeek, TimeOnly LocalTime) ConvertBroadcastSlot(DayOfWeek jstDayOfWeek, TimeOnly jstTime, DateTimeOffset referenceUtc) =>
            (jstDayOfWeek, jstTime);
    }

    private sealed class FakeAnimeUpdateService : IAnimeUpdateService
    {
        public Task<List<AnimeUpdateDto>> GetRecentAsync(DateTimeOffset since, CancellationToken ct = default) =>
            Task.FromResult(new List<AnimeUpdateDto>());
        public Task<List<AnimeUpdateDto>> GetHistoryAsync(CancellationToken ct = default) =>
            Task.FromResult(new List<AnimeUpdateDto>());
    }
}
