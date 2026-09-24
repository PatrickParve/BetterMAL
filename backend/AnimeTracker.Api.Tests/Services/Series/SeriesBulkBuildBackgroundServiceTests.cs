using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Jobs;
using AnimeTracker.Api.Services.Series;
using AnimeTracker.Api.Services.Tmdb;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SeriesModel = AnimeTracker.Api.Models.Series;

namespace AnimeTracker.Api.Tests.Services.Series;

// The settings page's "build all series from my list" run (design.md
// decision 6/task 5): targets are resolved from my list minus anime already
// in a stored series, a target that becomes covered mid-run (because an
// earlier target's build stored its membership too) is counted as processed
// without a build of its own, and a failing target logs and continues rather
// than aborting.
public class SeriesBulkBuildBackgroundServiceTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static UserAnimeEntry Entry(int animeId) => new() { AnimeId = animeId, Status = WatchStatus.Watching };

    // Starts the service, signals a run, and waits (bounded) for the tracker
    // to report Complete before stopping the service. TryBegin() before
    // Signal() mirrors SeriesController.TriggerBulkBuild (design.md D2): the
    // tracker is the start gate, and the background service itself no longer
    // moves it into Running.
    private static async Task RunOnceAsync(
        AnimeTrackerDbContext db, List<UserAnimeEntry> entries, ISeriesService seriesService,
        SeriesBulkBuildTrigger trigger, SeriesBulkBuildProgress tracker, JobPhase expectedPhase = JobPhase.Complete)
    {
        var service = new SeriesBulkBuildBackgroundService(
            new FakeServiceScopeFactory(new FakeServiceProvider(db, new FakeUserAnimeEntryRepository(entries), seriesService)),
            trigger,
            tracker,
            NullLogger<SeriesBulkBuildBackgroundService>.Instance);

        await service.StartAsync(CancellationToken.None);
        try
        {
            tracker.TryBegin();
            trigger.Signal();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (tracker.Snapshot.Phase == JobPhase.Running && !cts.IsCancellationRequested)
                await Task.Delay(10, CancellationToken.None);

            Assert.Equal(expectedPhase, tracker.Snapshot.Phase); // fail loudly on a test timeout, not a hang
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task TargetsAreResolvedFromMyListExcludingAlreadyCoveredAnime()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 2, BuiltAt = DateTimeOffset.UtcNow });
        db.SeriesMembers.Add(new SeriesMember { AnimeId = 2, SeriesId = 2, IsMainLine = true, Order = 0, IsPrimary = true });
        await db.SaveChangesAsync();

        var seriesService = new FakeSeriesService();
        var trigger = new SeriesBulkBuildTrigger();
        var tracker = new SeriesBulkBuildProgress();

        await RunOnceAsync(db, [Entry(1), Entry(2), Entry(3)], seriesService, trigger, tracker);

        Assert.Equal([1, 3], seriesService.CalledFor.OrderBy(id => id));
        Assert.Equal(2, tracker.Snapshot.Total); // anime 2 was never a target
        Assert.Equal(2, tracker.Snapshot.Done);
    }

    [Fact]
    public async Task ATargetCoveredMidRunIsCountedWithoutBeingBuiltAgain()
    {
        using var db = CreateDb();
        await db.SaveChangesAsync();

        // Building anime 1's franchise also stores anime 2 as one of its
        // members — simulates one build covering several targets at once.
        var seriesService = new FakeSeriesService();
        seriesService.OnCall(1, () =>
        {
            db.Series.Add(new SeriesModel { Id = 1, BuiltAt = DateTimeOffset.UtcNow });
            db.SeriesMembers.Add(new SeriesMember { AnimeId = 1, SeriesId = 1, IsMainLine = true, Order = 0, IsPrimary = true });
            db.SeriesMembers.Add(new SeriesMember { AnimeId = 2, SeriesId = 1, IsMainLine = true, Order = 1, IsPrimary = true });
            db.SaveChanges();
        });

        var trigger = new SeriesBulkBuildTrigger();
        var tracker = new SeriesBulkBuildProgress();

        await RunOnceAsync(db, [Entry(1), Entry(2)], seriesService, trigger, tracker);

        // Anime 2 was already covered by the time the loop reached it, so it
        // must not trigger a second build.
        Assert.Equal([1], seriesService.CalledFor);
        Assert.Equal(2, tracker.Snapshot.Total);
        Assert.Equal(2, tracker.Snapshot.Done);
    }

    [Fact]
    public async Task AFailingTargetIsLoggedAndTheRunContinues()
    {
        using var db = CreateDb();
        await db.SaveChangesAsync();

        var seriesService = new FakeSeriesService();
        seriesService.OnCall(2, () => throw new InvalidOperationException("MAL fetch failed"));

        var trigger = new SeriesBulkBuildTrigger();
        var tracker = new SeriesBulkBuildProgress();

        await RunOnceAsync(db, [Entry(1), Entry(2), Entry(3)], seriesService, trigger, tracker, expectedPhase: JobPhase.Failed);

        Assert.Equal([1, 2, 3], seriesService.CalledFor.OrderBy(id => id));
        Assert.Equal(3, tracker.Snapshot.Total);
        Assert.Equal(3, tracker.Snapshot.Done); // the failure still counts as processed
        Assert.Equal(JobPhase.Failed, tracker.Snapshot.Phase);
        Assert.Equal("1 of 3 series couldn't be built.", tracker.Snapshot.Error);
    }

    [Fact]
    public async Task ASeriesNotFoundTargetIsANormalOutcomeNotAnError()
    {
        using var db = CreateDb();
        await db.SaveChangesAsync();

        var seriesService = new FakeSeriesService();
        seriesService.OnCall(1, () => throw new SeriesNotFoundException(1));

        var trigger = new SeriesBulkBuildTrigger();
        var tracker = new SeriesBulkBuildProgress();

        await RunOnceAsync(db, [Entry(1)], seriesService, trigger, tracker);

        Assert.Equal(1, tracker.Snapshot.Done);
        Assert.Equal(JobPhase.Complete, tracker.Snapshot.Phase);
    }

    [Fact]
    public async Task TargetsIncludeMembersOfOutOfDateSeries()
    {
        using var db = CreateDb();
        var staleBuiltAt = SeriesGraphBuilder.ClassificationRevisedAt - TimeSpan.FromDays(1);
        db.Series.Add(new SeriesModel { Id = 2, BuiltAt = staleBuiltAt });
        db.SeriesMembers.Add(new SeriesMember { AnimeId = 2, SeriesId = 2, IsMainLine = true, Order = 0, IsPrimary = true });
        await db.SaveChangesAsync();

        var seriesService = new FakeSeriesService();
        var trigger = new SeriesBulkBuildTrigger();
        var tracker = new SeriesBulkBuildProgress();

        await RunOnceAsync(db, [Entry(2)], seriesService, trigger, tracker);

        // Anime 2 already has a SeriesMembers row, but its series predates
        // ClassificationRevisedAt, so it's a target rather than skipped as
        // already covered.
        Assert.Equal([2], seriesService.CalledFor);
        Assert.Equal(1, tracker.Snapshot.Total);
        Assert.Equal(1, tracker.Snapshot.Done);
    }

    // split-series-by-version task 8.5/series-versions spec "A boundary-only
    // membership does not count as covered", reconfirmed by
    // rebuild-series-by-story-component task 7.5 now that IsPrimary is
    // derived from MembershipKind rather than anchor distance: an anime held
    // only as another series' NeighbourTelling (never primary, design.md D8)
    // still needs its own series built, even though a SeriesMembers row for
    // it already exists and is up to date.
    [Fact]
    public async Task ANeighbourTellingOnlyMembershipDoesNotCountAsCovered()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 2, BuiltAt = DateTimeOffset.UtcNow });
        // Anime 1 is a NeighbourTelling of series 2 (another telling's own
        // story component) — up to date, but never primary, so it isn't its
        // series.
        db.SeriesMembers.Add(new SeriesMember { AnimeId = 1, SeriesId = 2, IsMainLine = false, Order = 0, IsPrimary = false, MembershipKind = nameof(MembershipKind.NeighbourTelling) });
        db.SeriesMembers.Add(new SeriesMember { AnimeId = 2, SeriesId = 2, IsMainLine = true, Order = 0, IsPrimary = true });
        await db.SaveChangesAsync();

        var seriesService = new FakeSeriesService();
        var trigger = new SeriesBulkBuildTrigger();
        var tracker = new SeriesBulkBuildProgress();

        await RunOnceAsync(db, [Entry(1)], seriesService, trigger, tracker);

        Assert.Equal([1], seriesService.CalledFor); // built its own series rather than skipped as covered
    }

    [Fact]
    public async Task ASeriesRebuiltEarlierInTheRunShortCircuitsItsOtherMembers()
    {
        using var db = CreateDb();
        var staleBuiltAt = SeriesGraphBuilder.ClassificationRevisedAt - TimeSpan.FromDays(1);
        db.Series.Add(new SeriesModel { Id = 1, BuiltAt = staleBuiltAt });
        db.SeriesMembers.Add(new SeriesMember { AnimeId = 1, SeriesId = 1, IsMainLine = true, Order = 0, IsPrimary = true });
        db.SeriesMembers.Add(new SeriesMember { AnimeId = 2, SeriesId = 1, IsMainLine = true, Order = 1, IsPrimary = true });
        await db.SaveChangesAsync();

        // Building anime 1's franchise re-stamps the whole series — including
        // anime 2's membership — as freshly built.
        var seriesService = new FakeSeriesService();
        seriesService.OnCall(1, () =>
        {
            db.Series.Single(s => s.Id == 1).BuiltAt = DateTimeOffset.UtcNow;
            db.SaveChanges();
        });

        var trigger = new SeriesBulkBuildTrigger();
        var tracker = new SeriesBulkBuildProgress();

        await RunOnceAsync(db, [Entry(1), Entry(2)], seriesService, trigger, tracker);

        // Anime 2's series was rebuilt (via anime 1's target) before the loop
        // reached anime 2, so anime 2 must not trigger a second build.
        Assert.Equal([1], seriesService.CalledFor);
        Assert.Equal(2, tracker.Snapshot.Total);
        Assert.Equal(2, tracker.Snapshot.Done);
    }

    [Fact]
    public async Task ARunThatThrowsDuringTargetResolutionReportsFailed()
    {
        using var db = CreateDb();
        await db.SaveChangesAsync();

        var trigger = new SeriesBulkBuildTrigger();
        var tracker = new SeriesBulkBuildProgress();
        var service = new SeriesBulkBuildBackgroundService(
            new FakeServiceScopeFactory(new FakeServiceProvider(db, new ThrowingUserAnimeEntryRepository(), new FakeSeriesService())),
            trigger,
            tracker,
            NullLogger<SeriesBulkBuildBackgroundService>.Instance);

        await service.StartAsync(CancellationToken.None);
        try
        {
            tracker.TryBegin();
            trigger.Signal();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (tracker.Snapshot.Phase != JobPhase.Failed && !cts.IsCancellationRequested)
                await Task.Delay(10, CancellationToken.None);

            Assert.Equal(JobPhase.Failed, tracker.Snapshot.Phase); // fail loudly on a test timeout, not a hang
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task TrackerTransitionsThroughRunningBeforeComplete()
    {
        using var db = CreateDb();
        await db.SaveChangesAsync();

        var gate = new TaskCompletionSource();
        var seriesService = new FakeSeriesService();
        seriesService.OnCall(1, () => gate.Task.Wait(TimeSpan.FromSeconds(5)));

        var trigger = new SeriesBulkBuildTrigger();
        var tracker = new SeriesBulkBuildProgress();

        Assert.Equal(JobPhase.NotStarted, tracker.Snapshot.Phase);

        var runTask = RunOnceAsync(db, [Entry(1), Entry(2)], seriesService, trigger, tracker);

        // TryBegin() (inside RunOnceAsync) moves the tracker to Running with
        // Total = null at once; the background service sets the real total
        // only once it has resolved targets, so wait for that rather than
        // just for Running (design.md D2 decouples the two).
        using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
        {
            while (tracker.Snapshot.Total is null && !cts.IsCancellationRequested)
                await Task.Delay(10, CancellationToken.None);
        }
        Assert.Equal(JobPhase.Running, tracker.Snapshot.Phase);
        Assert.Equal(2, tracker.Snapshot.Total);

        gate.SetResult();
        await runTask;

        Assert.Equal(JobPhase.Complete, tracker.Snapshot.Phase);
        Assert.Equal(2, tracker.Snapshot.Done);
    }

    private sealed class FakeSeriesService : ISeriesService
    {
        private readonly Dictionary<int, Action> _onCall = new();
        public List<int> CalledFor { get; } = [];

        public void OnCall(int animeId, Action action) => _onCall[animeId] = action;

        public Task<SeriesDto> GetSeriesAsync(int animeId, CancellationToken ct = default)
        {
            CalledFor.Add(animeId);
            if (_onCall.TryGetValue(animeId, out var action))
                action();
            return Task.FromResult(EmptySeriesDto(animeId));
        }

        public Task<SeriesDto> RebuildSeriesAsync(int animeId, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<int?> FindSeriesIdAsync(int animeId, CancellationToken ct = default) =>
            throw new NotImplementedException();

        private static SeriesDto EmptySeriesDto(int animeId) => new(
            animeId, null, [], "Series", null, null, "Finished", null, null,
            DateTimeOffset.UtcNow, false, false,
            new SeriesScoresDto(
                new SeriesAverageDto(null, 0, 0), new SeriesAverageDto(null, 0, 0),
                new SeriesAverageDto(null, 0, 0), new SeriesAverageDto(null, 0, 0)),
            new SeriesStatsDto(0, 0, false, 0, 0, 0, 0, 0, 0, 0, 0, false, 0, 0, null, null, null, [], [], [], [], []),
            [], [],
            [], [],
            null, null, [], [], 0,
            new SeriesTmdbPicturesDto(false, false, [], 0));
    }

    private sealed class FakeUserAnimeEntryRepository(List<UserAnimeEntry> entries) : IUserAnimeEntryRepository
    {
        public Task<UserAnimeEntry?> GetByAnimeIdAsync(int animeId, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<List<UserAnimeEntry>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(entries);
        public Task<(int PendingCount, int HeldCount, DateTimeOffset? LastSyncedAt)> GetSyncStatusAsync(CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    private sealed class ThrowingUserAnimeEntryRepository : IUserAnimeEntryRepository
    {
        public Task<UserAnimeEntry?> GetByAnimeIdAsync(int animeId, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<List<UserAnimeEntry>> GetAllAsync(CancellationToken ct = default) =>
            throw new InvalidOperationException("Target resolution failed.");
        public Task<(int PendingCount, int HeldCount, DateTimeOffset? LastSyncedAt)> GetSyncStatusAsync(CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    private sealed class FakeServiceProvider(
        AnimeTrackerDbContext db, IUserAnimeEntryRepository entryRepository, ISeriesService seriesService) : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(AnimeTrackerDbContext)) return db;
            if (serviceType == typeof(IUserAnimeEntryRepository)) return entryRepository;
            if (serviceType == typeof(ISeriesService)) return seriesService;
            return null;
        }
    }

    private sealed class FakeServiceScopeFactory(IServiceProvider provider) : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new FakeServiceScope(provider);
    }

    private sealed class FakeServiceScope(IServiceProvider provider) : IServiceScope
    {
        public IServiceProvider ServiceProvider { get; } = provider;
        public void Dispose() { }
    }
}
