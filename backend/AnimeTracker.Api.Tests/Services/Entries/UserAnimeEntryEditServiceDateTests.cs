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

    private static async Task SeedAsync(
        AnimeTrackerDbContext db, int animeId,
        WatchStatus status = WatchStatus.Watching, int episodesWatched = 5,
        DateOnly? startedAt = null, DateOnly? completedAt = null,
        int? totalEpisodes = 24, string? airingStatus = "finished_airing")
    {
        var anime = new AnimeMetadata { Id = animeId, Title = $"Anime {animeId}", TotalEpisodes = totalEpisodes, AiringStatus = airingStatus };
        var entry = new UserAnimeEntry
        {
            AnimeId = animeId,
            Anime = anime,
            Status = status,
            EpisodesWatched = episodesWatched,
            StartedAt = startedAt,
            CompletedAt = completedAt,
        };
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

    // ApplyEpisodesWatched's started-date fill (design.md D1/D2 of
    // fix-auto-date-fill-and-episode-cap): fills only where the user has said
    // nothing about either date, so it never hands ApplyDates's out-of-order
    // check a pair the user never entered.

    [Fact]
    public async Task FirstEpisodeOfARewatchLeavesBothDatesUntouched()
    {
        using var db = CreateDb();
        var completedAt = new DateOnly(2020, 1, 1);
        await SeedAsync(db, 1, status: WatchStatus.Rewatching, episodesWatched: 0, completedAt: completedAt);

        var result = await CreateService(db).UpdateEntryAsync(1, new UserAnimeEntryEditRequest { EpisodesWatched = 1 });

        Assert.Equal(1, result.EpisodesWatched);
        Assert.Null(result.StartedAt);
        Assert.Equal(completedAt, result.CompletedAt);
    }

    [Fact]
    public async Task BackfillingWithTheEpisodeCountInTheSameSaveEndsWithNoStartDate()
    {
        using var db = CreateDb();
        await SeedAsync(db, 1, status: WatchStatus.PlanToWatch, episodesWatched: 0);
        var finishDate = new DateOnly(2024, 1, 1);

        var result = await CreateService(db).UpdateEntryAsync(
            1, new UserAnimeEntryEditRequest { Status = WatchStatus.Completed, EpisodesWatched = 24, CompletedAt = finishDate });

        Assert.Equal(WatchStatus.Completed, result.Status);
        Assert.Equal(24, result.EpisodesWatched);
        Assert.Equal(finishDate, result.CompletedAt);
        Assert.Null(result.StartedAt);
    }

    [Fact]
    public async Task BackfillingWithoutTouchingEpisodesStillSaves()
    {
        using var db = CreateDb();
        await SeedAsync(db, 1, status: WatchStatus.PlanToWatch, episodesWatched: 0);
        var finishDate = new DateOnly(2024, 1, 1);

        var result = await CreateService(db).UpdateEntryAsync(
            1, new UserAnimeEntryEditRequest { Status = WatchStatus.Completed, CompletedAt = finishDate });

        Assert.Equal(WatchStatus.Completed, result.Status);
        Assert.Equal(24, result.EpisodesWatched);
        Assert.Equal(finishDate, result.CompletedAt);
        Assert.Null(result.StartedAt);
    }

    [Fact]
    public async Task AStartDateSuppliedAlongsideTheFirstEpisodeStandsAsSupplied()
    {
        using var db = CreateDb();
        await SeedAsync(db, 1, status: WatchStatus.Watching, episodesWatched: 0);
        var startDate = new DateOnly(2023, 5, 1);

        var result = await CreateService(db).UpdateEntryAsync(
            1, new UserAnimeEntryEditRequest { EpisodesWatched = 1, StartedAt = startDate });

        Assert.Equal(startDate, result.StartedAt);
    }

    [Fact]
    public async Task FreshEntryWithNoDatesGetsTodayAsStartDate()
    {
        using var db = CreateDb();
        await SeedAsync(db, 1, status: WatchStatus.PlanToWatch, episodesWatched: 0);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var result = await CreateService(db).UpdateEntryAsync(1, new UserAnimeEntryEditRequest { EpisodesWatched = 1 });

        Assert.Equal(today, result.StartedAt);
    }

    [Fact]
    public async Task AnExistingStartDateIsNeverOverwrittenByTheFill()
    {
        using var db = CreateDb();
        var startedAt = new DateOnly(2022, 3, 1);
        await SeedAsync(db, 1, status: WatchStatus.Watching, episodesWatched: 0, startedAt: startedAt);

        var result = await CreateService(db).UpdateEntryAsync(1, new UserAnimeEntryEditRequest { EpisodesWatched = 1 });

        Assert.Equal(startedAt, result.StartedAt);
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
