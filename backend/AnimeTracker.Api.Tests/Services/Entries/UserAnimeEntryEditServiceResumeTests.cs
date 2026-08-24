using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Entries;
using AnimeTracker.Api.Services.Sync;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimeTracker.Api.Tests.Services.Entries;

// polish-more-progress-and-overlays list-editing requirement "Raising
// progress resumes an entry" (tasks.md 1.1-1.3, 2.1-2.4): the resume-to-
// Watching arm in ApplyEpisodesWatched and the HasStatus override.
public class UserAnimeEntryEditServiceResumeTests
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
            new FakeServiceScopeFactory(),
            NullLogger<UserAnimeEntryEditService>.Instance);

    private static async Task<AnimeMetadata> SeedAsync(
        AnimeTrackerDbContext db, int animeId, int? totalEpisodes, WatchStatus status, int episodesWatched,
        string? airingStatus = null, int rewatchCount = 0)
    {
        var anime = new AnimeMetadata { Id = animeId, Title = $"Anime {animeId}", TotalEpisodes = totalEpisodes, AiringStatus = airingStatus };
        var entry = new UserAnimeEntry
        {
            AnimeId = animeId,
            Anime = anime,
            Status = status,
            EpisodesWatched = episodesWatched,
            RewatchCount = rewatchCount,
        };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(entry);
        await db.SaveChangesAsync();
        return anime;
    }

    // --- 2.1: the resumptions ---

    [Theory]
    [InlineData(WatchStatus.PlanToWatch)]
    [InlineData(WatchStatus.OnHold)]
    [InlineData(WatchStatus.Dropped)]
    public async Task RaisingTheCountShortOfTheTotalResumesToWatching(WatchStatus originalStatus)
    {
        using var db = CreateDb();
        await SeedAsync(db, 1, totalEpisodes: 24, originalStatus, episodesWatched: 5, airingStatus: "finished_airing");

        var result = await CreateService(db).UpdateEntryAsync(1, new UserAnimeEntryEditRequest { EpisodesWatched = 7 });

        Assert.Equal(WatchStatus.Watching, result.Status);
        Assert.Equal(7, result.EpisodesWatched);

        var activity = await db.ActivityLogs.SingleAsync(a => a.AnimeId == 1 && a.ChangeType == ActivityChangeType.StatusChanged);
        Assert.Equal($"{originalStatus} -> Watching", activity.ChangeDetail);
    }

    // --- 2.2: the exclusions ---

    [Fact]
    public async Task RaisingToTheCompletionTargetCompletesInsteadOfResuming()
    {
        using var db = CreateDb();
        await SeedAsync(db, 1, totalEpisodes: 12, WatchStatus.PlanToWatch, episodesWatched: 5, airingStatus: "finished_airing");

        var result = await CreateService(db).UpdateEntryAsync(1, new UserAnimeEntryEditRequest { EpisodesWatched = 12 });

        Assert.Equal(WatchStatus.Completed, result.Status);
    }

    [Fact]
    public async Task LoweringTheCountLeavesTheStatusAlone()
    {
        using var db = CreateDb();
        await SeedAsync(db, 1, totalEpisodes: 24, WatchStatus.Dropped, episodesWatched: 5, airingStatus: "finished_airing");

        var result = await CreateService(db).UpdateEntryAsync(1, new UserAnimeEntryEditRequest { EpisodesWatched = 3 });

        Assert.Equal(WatchStatus.Dropped, result.Status);
        Assert.Equal(3, result.EpisodesWatched);
    }

    [Fact]
    public async Task AnUnchangedCountLeavesTheStatusAlone()
    {
        using var db = CreateDb();
        await SeedAsync(db, 1, totalEpisodes: 24, WatchStatus.OnHold, episodesWatched: 5, airingStatus: "finished_airing");

        var result = await CreateService(db).UpdateEntryAsync(1, new UserAnimeEntryEditRequest { EpisodesWatched = 5 });

        Assert.Equal(WatchStatus.OnHold, result.Status);
    }

    [Fact]
    public async Task AWatchingEntryStaysWatching()
    {
        using var db = CreateDb();
        await SeedAsync(db, 1, totalEpisodes: 24, WatchStatus.Watching, episodesWatched: 5, airingStatus: "finished_airing");

        var result = await CreateService(db).UpdateEntryAsync(1, new UserAnimeEntryEditRequest { EpisodesWatched = 7 });

        Assert.Equal(WatchStatus.Watching, result.Status);
    }

    [Fact]
    public async Task ARewatchingEntryStaysRewatching()
    {
        using var db = CreateDb();
        await SeedAsync(db, 1, totalEpisodes: 24, WatchStatus.Rewatching, episodesWatched: 5, airingStatus: "finished_airing");

        var result = await CreateService(db).UpdateEntryAsync(1, new UserAnimeEntryEditRequest { EpisodesWatched = 7 });

        Assert.Equal(WatchStatus.Rewatching, result.Status);
    }

    [Fact]
    public async Task ACompletedEntryFollowsTheStrictCompletedRuleInstead()
    {
        // Completed is excluded from the resume arm entirely — a Completed
        // entry falling short of the total is owned by the existing
        // strict-Completed count-drop arm, which only ever fires on a drop,
        // never a raise. Raising a Completed entry is impossible without
        // first lowering it (it's already capped at the total), so this
        // covers the drop path landing in Watching rather than Rewatching
        // when the entry has never actually finished airing.
        using var db = CreateDb();
        await SeedAsync(db, 1, totalEpisodes: 12, WatchStatus.Completed, episodesWatched: 12, airingStatus: "currently_airing");

        var result = await CreateService(db, airedSoFar: 12).UpdateEntryAsync(1, new UserAnimeEntryEditRequest { EpisodesWatched = 10 });

        Assert.Equal(WatchStatus.Watching, result.Status);
    }

    // --- 2.3: the unknown-target case ---

    [Fact]
    public async Task AnUnknownTotalAndAiredCountStillResumes()
    {
        using var db = CreateDb();
        await SeedAsync(db, 1, totalEpisodes: null, WatchStatus.Dropped, episodesWatched: 5, airingStatus: null);

        var result = await CreateService(db, airedSoFar: null).UpdateEntryAsync(1, new UserAnimeEntryEditRequest { EpisodesWatched = 7 });

        Assert.Equal(WatchStatus.Watching, result.Status);
    }

    // --- 2.4: HasStatus ---

    [Fact]
    public async Task ResendingTheStoredStatusAlongsideARaiseSuppressesTheResumeRule()
    {
        using var db = CreateDb();
        await SeedAsync(db, 1, totalEpisodes: 24, WatchStatus.Dropped, episodesWatched: 5, airingStatus: "finished_airing");

        var result = await CreateService(db).UpdateEntryAsync(
            1, new UserAnimeEntryEditRequest { EpisodesWatched = 7, Status = WatchStatus.Dropped });

        Assert.Equal(WatchStatus.Dropped, result.Status);
        Assert.Equal(7, result.EpisodesWatched);
    }

    [Fact]
    public async Task ADifferentExplicitStatusAlongsideARaiseSavesThatStatus()
    {
        using var db = CreateDb();
        await SeedAsync(db, 1, totalEpisodes: 24, WatchStatus.Dropped, episodesWatched: 5, airingStatus: "finished_airing");

        var result = await CreateService(db).UpdateEntryAsync(
            1, new UserAnimeEntryEditRequest { EpisodesWatched = 7, Status = WatchStatus.OnHold });

        Assert.Equal(WatchStatus.OnHold, result.Status);
        Assert.Equal(7, result.EpisodesWatched);
    }

    [Fact]
    public async Task NoStatusKeyAtAllResumes()
    {
        using var db = CreateDb();
        await SeedAsync(db, 1, totalEpisodes: 24, WatchStatus.Dropped, episodesWatched: 5, airingStatus: "finished_airing");

        var request = new UserAnimeEntryEditRequest { EpisodesWatched = 7 };
        Assert.False(request.HasStatus);

        var result = await CreateService(db).UpdateEntryAsync(1, request);

        Assert.Equal(WatchStatus.Watching, result.Status);
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

    private sealed class FakeEpisodeScheduleService(int? airedSoFar) : IEpisodeScheduleService
    {
        public Task<ResolvedEpisode?> ResolveOnLocalDateAsync(AnimeMetadata anime, DateOnly localDate, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<DateTimeOffset?> NextAiringInstantAsync(AnimeMetadata anime, DateTimeOffset afterUtc, CancellationToken ct = default) =>
            Task.FromResult<DateTimeOffset?>(null);
        public Task<int?> EpisodesAiredAsOfAsync(AnimeMetadata anime, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            Task.FromResult(airedSoFar);
        public Task<Dictionary<int, int>> EpisodesAiredAsOfAsync(IReadOnlyCollection<AnimeMetadata> anime, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<Dictionary<int, int>> EpisodesAiredAsOfAsync(IReadOnlyCollection<int> animeIds, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }
}
