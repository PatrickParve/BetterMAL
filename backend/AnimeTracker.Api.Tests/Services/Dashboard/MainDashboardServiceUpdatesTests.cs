using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Dashboard;
using AnimeTracker.Api.Services.Entries;
using AnimeTracker.Api.Services.Relations;
using AnimeTracker.Api.Services.Scheduling;
using AnimeTracker.Api.Services.Updates;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Services.Dashboard;

// main-dashboard + anime-updates: the Updates section rides the dashboard
// payload with a 30-day window (design.md D12; tasks 6.5/6.7).
public class MainDashboardServiceUpdatesTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static MainDashboardService CreateService(AnimeTrackerDbContext db, List<UserAnimeEntry> entries) =>
        new(
            new FakeUserAnimeEntryRepository(entries),
            new FakeEpisodeScheduleService(),
            new NullCompletedEntryReopenService(),
            new BroadcastLocalTimeConverter(),
            new AnimeUpdateService(db, new RelationResolver(db), new BroadcastLocalTimeConverter()));

    [Fact]
    public async Task A31DayOldUpdateIsExcludedFromTheDashboardButRemainsInHistory()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "Solo" };
        db.AnimeMetadata.Add(anime);
        var entry = new UserAnimeEntry { AnimeId = 1, Anime = anime, Status = WatchStatus.Watching };
        db.UserAnimeEntries.Add(entry);
        var now = DateTimeOffset.UtcNow;
        db.AnimeUpdates.Add(new AnimeUpdate { AnimeId = 1, DetectedAt = now.AddDays(-31), Kinds = AnimeUpdateKinds.StartDateReleased });
        db.AnimeUpdates.Add(new AnimeUpdate { AnimeId = 1, DetectedAt = now.AddDays(-1), Kinds = AnimeUpdateKinds.EpisodeCountReleased });
        await db.SaveChangesAsync();

        var dashboard = await CreateService(db, [entry]).GetDashboardAsync();
        var history = await new AnimeUpdateService(db, new RelationResolver(db), new BroadcastLocalTimeConverter()).GetHistoryAsync();

        Assert.Single(dashboard.Updates);
        Assert.Equal(2, history.Count);
    }

    private sealed class NullCompletedEntryReopenService : ICompletedEntryReopenService
    {
        public Task ReopenAsync(IReadOnlyCollection<UserAnimeEntry> entries, IReadOnlyDictionary<int, int> airedSoFarByAnimeId, CancellationToken ct = default) =>
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
}
