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

// add-rewatching-status list-editing requirements (tasks.md 5.1-5.3, 5.6):
// entering/exiting Rewatching, and the strict Completed rule's count-drop
// fallback.
public class UserAnimeEntryEditServiceRewatchingTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static UserAnimeEntryEditService CreateService(AnimeTrackerDbContext db, int? airedSoFar = null) =>
        new(
            db,
            new FakeEntrySyncScheduler(),
            new FakeEpisodeScheduleService(airedSoFar),
            new FakeAiringRefreshTrigger(),
            new AnimeRankingService(new UserAnimeEntryRepository(db), new TopAnimeSelectionRepository(db)),
            new FakeServiceScopeFactory(),
            NullLogger<UserAnimeEntryEditService>.Instance);

    private static async Task<AnimeMetadata> SeedAsync(
        AnimeTrackerDbContext db, int animeId, int? totalEpisodes, WatchStatus status, int episodesWatched,
        string? airingStatus = null, DateOnly? completedAt = null, DateOnly? startedAt = null,
        int rewatchCount = 0, int? myScore = null)
    {
        var anime = new AnimeMetadata { Id = animeId, Title = $"Anime {animeId}", TotalEpisodes = totalEpisodes, AiringStatus = airingStatus };
        var entry = new UserAnimeEntry
        {
            AnimeId = animeId,
            Anime = anime,
            Status = status,
            EpisodesWatched = episodesWatched,
            CompletedAt = completedAt,
            StartedAt = startedAt,
            RewatchCount = rewatchCount,
            MyScore = myScore,
        };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(entry);
        await db.SaveChangesAsync();
        return anime;
    }

    [Fact]
    public async Task EnteringRewatchingResetsProgressAndLeavesDatesScoreAndRewatchCountAlone()
    {
        using var db = CreateDb();
        var finishDate = new DateOnly(2024, 1, 1);
        var startDate = new DateOnly(2023, 12, 1);
        await SeedAsync(db, 1, totalEpisodes: 24, WatchStatus.Completed, episodesWatched: 24,
            airingStatus: "finished_airing", completedAt: finishDate, startedAt: startDate, rewatchCount: 1, myScore: 8);

        var result = await CreateService(db).UpdateEntryAsync(1, new UserAnimeEntryEditRequest { Status = WatchStatus.Rewatching });

        Assert.Equal(WatchStatus.Rewatching, result.Status);
        Assert.Equal(0, result.EpisodesWatched);
        Assert.Equal(startDate, result.StartedAt);
        Assert.Equal(finishDate, result.CompletedAt);
        Assert.Equal(8, result.MyScore);
        Assert.Equal(1, result.RewatchCount);
    }

    [Fact]
    public async Task RewatchReachingTheTotalReCompletesIncrementsRewatchCountAndKeepsTheOriginalFinishDate()
    {
        using var db = CreateDb();
        var finishDate = new DateOnly(2024, 1, 1);
        await SeedAsync(db, 1, totalEpisodes: 24, WatchStatus.Rewatching, episodesWatched: 23,
            airingStatus: "finished_airing", completedAt: finishDate, rewatchCount: 1);

        var result = await CreateService(db).UpdateEntryAsync(1, new UserAnimeEntryEditRequest { EpisodesWatched = 24 });

        Assert.Equal(WatchStatus.Completed, result.Status);
        Assert.Equal(2, result.RewatchCount);
        Assert.Equal(finishDate, result.CompletedAt);
    }

    [Fact]
    public async Task LoweringACompletedEntrysCountOnAFinishedAnimeStartsARewatch()
    {
        using var db = CreateDb();
        await SeedAsync(db, 1, totalEpisodes: 24, WatchStatus.Completed, episodesWatched: 24, airingStatus: "finished_airing");

        var result = await CreateService(db).UpdateEntryAsync(1, new UserAnimeEntryEditRequest { EpisodesWatched = 5 });

        Assert.Equal(WatchStatus.Rewatching, result.Status);
        Assert.Equal(5, result.EpisodesWatched);
    }

    [Fact]
    public async Task LoweringToZeroStartsARewatchRatherThanStayingCompleted()
    {
        using var db = CreateDb();
        await SeedAsync(db, 1, totalEpisodes: 24, WatchStatus.Completed, episodesWatched: 24, airingStatus: "finished_airing");

        var result = await CreateService(db).UpdateEntryAsync(1, new UserAnimeEntryEditRequest { EpisodesWatched = 0 });

        Assert.Equal(WatchStatus.Rewatching, result.Status);
        Assert.Equal(0, result.EpisodesWatched);
    }

    [Fact]
    public async Task LoweringTheCountOnAStillAiringAnimeYieldsWatchingInsteadOfRewatching()
    {
        using var db = CreateDb();
        // Completed at the aired-so-far count (gate-editing-on-aired-episodes
        // D4's "caught up" completion for a currently-airing anime), then
        // lowered further — still below what's aired, so it's a genuine drop.
        await SeedAsync(db, 1, totalEpisodes: 24, WatchStatus.Completed, episodesWatched: 12, airingStatus: "currently_airing");

        var result = await CreateService(db, airedSoFar: 12).UpdateEntryAsync(1, new UserAnimeEntryEditRequest { EpisodesWatched = 10 });

        Assert.Equal(WatchStatus.Watching, result.Status);
        Assert.Equal(10, result.EpisodesWatched);
    }

    [Fact]
    public async Task ConfirmingTheUnchangedCountLeavesACompletedEntrysStatusAlone()
    {
        using var db = CreateDb();
        await SeedAsync(db, 1, totalEpisodes: 24, WatchStatus.Completed, episodesWatched: 24, airingStatus: "finished_airing");

        var result = await CreateService(db).UpdateEntryAsync(1, new UserAnimeEntryEditRequest { EpisodesWatched = 24 });

        Assert.Equal(WatchStatus.Completed, result.Status);
    }

    [Fact]
    public async Task SettingRewatchingIsRejectedOnACurrentlyAiringAnimeAndLeavesTheEntryUnchanged()
    {
        using var db = CreateDb();
        var finishDate = new DateOnly(2024, 1, 1);
        await SeedAsync(db, 1, totalEpisodes: 12, WatchStatus.Completed, episodesWatched: 12,
            airingStatus: "currently_airing", completedAt: finishDate);

        await Assert.ThrowsAsync<RewatchingNotEligibleException>(() =>
            CreateService(db).UpdateEntryAsync(1, new UserAnimeEntryEditRequest { Status = WatchStatus.Rewatching }));

        var stored = await db.UserAnimeEntries.AsNoTracking().SingleAsync(e => e.AnimeId == 1);
        Assert.Equal(WatchStatus.Completed, stored.Status);
        Assert.Equal(12, stored.EpisodesWatched);
    }

    [Fact]
    public async Task SettingRewatchingIsRejectedWhenTheEntryHasNeverBeenFinished()
    {
        using var db = CreateDb();
        await SeedAsync(db, 1, totalEpisodes: 12, WatchStatus.Watching, episodesWatched: 3,
            airingStatus: "finished_airing", completedAt: null, rewatchCount: 0);

        await Assert.ThrowsAsync<RewatchingNotEligibleException>(() =>
            CreateService(db).UpdateEntryAsync(1, new UserAnimeEntryEditRequest { Status = WatchStatus.Rewatching }));

        var stored = await db.UserAnimeEntries.AsNoTracking().SingleAsync(e => e.AnimeId == 1);
        Assert.Equal(WatchStatus.Watching, stored.Status);
    }

    [Fact]
    public async Task ResumingARewatchFromWatchingWithAFinishDateSucceedsWithoutPassingThroughCompleted()
    {
        using var db = CreateDb();
        var finishDate = new DateOnly(2023, 5, 1);
        await SeedAsync(db, 1, totalEpisodes: 12, WatchStatus.Watching, episodesWatched: 3,
            airingStatus: "finished_airing", completedAt: finishDate, rewatchCount: 0);

        var result = await CreateService(db).UpdateEntryAsync(1, new UserAnimeEntryEditRequest { Status = WatchStatus.Rewatching });

        Assert.Equal(WatchStatus.Rewatching, result.Status);
        Assert.Equal(0, result.EpisodesWatched);
    }

    [Fact]
    public async Task EndingARewatchEarlyLeavesTheRewatchCountUnchangedWhenItDoesNotCount()
    {
        using var db = CreateDb();
        await SeedAsync(db, 1, totalEpisodes: 24, WatchStatus.Rewatching, episodesWatched: 6,
            airingStatus: "finished_airing", rewatchCount: 1);

        var result = await CreateService(db).UpdateEntryAsync(
            1, new UserAnimeEntryEditRequest { Status = WatchStatus.Completed, CountsAsRewatch = false });

        Assert.Equal(WatchStatus.Completed, result.Status);
        Assert.Equal(24, result.EpisodesWatched);
        Assert.Equal(1, result.RewatchCount);
    }

    [Fact]
    public async Task EndingARewatchEarlyIncreasesTheRewatchCountWhenItDoesCount()
    {
        using var db = CreateDb();
        await SeedAsync(db, 1, totalEpisodes: 24, WatchStatus.Rewatching, episodesWatched: 6,
            airingStatus: "finished_airing", rewatchCount: 1);

        var result = await CreateService(db).UpdateEntryAsync(
            1, new UserAnimeEntryEditRequest { Status = WatchStatus.Completed, CountsAsRewatch = true });

        Assert.Equal(WatchStatus.Completed, result.Status);
        Assert.Equal(24, result.EpisodesWatched);
        Assert.Equal(2, result.RewatchCount);
    }

    [Fact]
    public async Task SettingCompletedFromAnyOtherStatusAsksNothingAndIgnoresCountsAsRewatch()
    {
        using var db = CreateDb();
        await SeedAsync(db, 1, totalEpisodes: 12, WatchStatus.Watching, episodesWatched: 12, rewatchCount: 0);

        // A stray CountsAsRewatch=true (e.g. stale client state) must have no
        // effect unless the entry was actually Rewatching beforehand.
        var result = await CreateService(db).UpdateEntryAsync(
            1, new UserAnimeEntryEditRequest { Status = WatchStatus.Completed, CountsAsRewatch = true });

        Assert.Equal(WatchStatus.Completed, result.Status);
        Assert.Equal(0, result.RewatchCount);
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

    // Defaults to null: no stored airing rows, so ApplyEpisodesWatchedAsync's
    // cap falls back to anime.TotalEpisodes — most of these tests aren't
    // exercising the aired-so-far gating that belongs to
    // gate-editing-on-aired-episodes. LoweringTheCountOnAStillAiringAnime...
    // below is the one exception and supplies a real value.
    private sealed class FakeEpisodeScheduleService(int? airedSoFar = null) : IEpisodeScheduleService
    {
        public Task<ResolvedEpisode?> ResolveOnLocalDateAsync(AnimeMetadata anime, DateOnly localDate, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<DateTimeOffset?> NextAiringInstantAsync(AnimeMetadata anime, DateTimeOffset afterUtc, CancellationToken ct = default) =>
            Task.FromResult<DateTimeOffset?>(null);
        public Task<int?> EpisodesAiredAsOfAsync(AnimeMetadata anime, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            Task.FromResult(airedSoFar);
        public Task<Dictionary<int, int>> EpisodesAiredAsOfAsync(IReadOnlyCollection<AnimeMetadata> anime, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            Task.FromResult(new Dictionary<int, int>());
        public Task<Dictionary<int, int>> EpisodesAiredAsOfAsync(IReadOnlyCollection<int> animeIds, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            Task.FromResult(new Dictionary<int, int>());
    }
}
