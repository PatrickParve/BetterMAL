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

// ApplyDates: neither start nor finish date may be set into the future.
public class UserAnimeEntryEditServiceDateTests
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

    private static async Task SeedAsync(AnimeTrackerDbContext db, int animeId)
    {
        var anime = new AnimeMetadata { Id = animeId, Title = $"Anime {animeId}", TotalEpisodes = 24, AiringStatus = "finished_airing" };
        var entry = new UserAnimeEntry { AnimeId = animeId, Anime = anime, Status = WatchStatus.Watching, EpisodesWatched = 5 };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(entry);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task AFutureStartDateIsRejected()
    {
        using var db = CreateDb();
        await SeedAsync(db, 1);
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

        var ex = await Assert.ThrowsAsync<EntryEditRejectedException>(() =>
            CreateService(db).UpdateEntryAsync(1, new UserAnimeEntryEditRequest { StartedAt = tomorrow }));

        Assert.Equal("Start date cannot be in the future.", ex.Message);
    }

    [Fact]
    public async Task AFutureFinishDateIsRejected()
    {
        using var db = CreateDb();
        await SeedAsync(db, 1);
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

        var ex = await Assert.ThrowsAsync<EntryEditRejectedException>(() =>
            CreateService(db).UpdateEntryAsync(1, new UserAnimeEntryEditRequest { CompletedAt = tomorrow }));

        Assert.Equal("Finish date cannot be in the future.", ex.Message);
    }

    [Fact]
    public async Task TodayIsAllowedForBothDates()
    {
        using var db = CreateDb();
        await SeedAsync(db, 1);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var result = await CreateService(db).UpdateEntryAsync(
            1, new UserAnimeEntryEditRequest { StartedAt = today, CompletedAt = today });

        Assert.Equal(today, result.StartedAt);
        Assert.Equal(today, result.CompletedAt);
    }

    [Fact]
    public async Task ClearingADateIsNeverRejected()
    {
        using var db = CreateDb();
        await SeedAsync(db, 1);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await CreateService(db).UpdateEntryAsync(1, new UserAnimeEntryEditRequest { StartedAt = today });

        var result = await CreateService(db).UpdateEntryAsync(1, new UserAnimeEntryEditRequest { StartedAt = null });

        Assert.Null(result.StartedAt);
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
            throw new NotImplementedException();
        public Task<Dictionary<int, int>> EpisodesAiredAsOfAsync(IReadOnlyCollection<int> animeIds, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }
}
