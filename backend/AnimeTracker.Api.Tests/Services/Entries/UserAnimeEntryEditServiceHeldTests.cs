using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Entries;
using AnimeTracker.Api.Services.Ranking;
using AnimeTracker.Api.Services.Sync;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimeTracker.Api.Tests.Services.Entries;

// hold-startup-pending-sync-for-review tasks.md 9.3 / design.md D15: a
// deliberate edit is current by definition and releases a hold, unlike an
// automatic transition (covered separately in AiringWatchStatusServiceTests).
public class UserAnimeEntryEditServiceHeldTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static UserAnimeEntryEditService CreateService(AnimeTrackerDbContext db) =>
        new(
            db,
            new FakeEntrySyncScheduler(),
            new FakeEpisodeScheduleService(),
            new FakeAiringRefreshTrigger(),
            new AnimeRankingService(new UserAnimeEntryRepository(db), new TopAnimeSelectionRepository(db)),
            new FakeServiceScopeFactory(),
            NullLogger<UserAnimeEntryEditService>.Instance);

    [Fact]
    public async Task EditingAHeldAnimeClearsItsHold()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1", TotalEpisodes = 24, AiringStatus = "finished_airing" };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry
        {
            AnimeId = 1, Anime = anime, Status = WatchStatus.Watching, EpisodesWatched = 3,
            PendingSync = true, HeldForReviewAt = DateTimeOffset.UtcNow.AddDays(-1),
        });
        await db.SaveChangesAsync();

        var result = await CreateService(db).UpdateEntryAsync(1, new UserAnimeEntryEditRequest { EpisodesWatched = 5 });

        Assert.Equal(5, result.EpisodesWatched);
        var stored = await db.UserAnimeEntries.AsNoTracking().SingleAsync(e => e.AnimeId == 1);
        Assert.True(stored.PendingSync);
        Assert.Null(stored.HeldForReviewAt);
    }

    private sealed class FakeEntrySyncScheduler : IEntrySyncScheduler
    {
        public void ScheduleSync(int animeId) { }
    }

    private sealed class FakeAiringRefreshTrigger : IAiringRefreshTrigger
    {
        public void Enqueue(int animeId) { }
        public Task<int> WaitAsync(CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class FakeServiceScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => throw new NotImplementedException();
    }

    private sealed class FakeEpisodeScheduleService : IEpisodeScheduleService
    {
        public Task<ResolvedEpisode?> ResolveOnLocalDateAsync(AnimeMetadata anime, DateOnly localDate, CancellationToken ct = default) =>
            throw new NotImplementedException();
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
