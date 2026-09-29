using System.Collections.Concurrent;
using System.Net;
using System.Runtime.CompilerServices;
using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Mal.Auth;
using AnimeTracker.Api.Services.Mal.Dto;
using AnimeTracker.Api.Services.Scheduling;
using AnimeTracker.Api.Services.Search;
using AnimeTracker.Api.Services.Series;
using AnimeTracker.Api.Services.Setup;
using AnimeTracker.Api.Tests.Services.IdMapping;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AnimeTracker.Api.Tests.Services.Setup;

/// <summary>How MyAnimeList and AniList fail in the fakes below, as the real HTTP layer would
/// report each.</summary>
internal static class Failures
{
    // What HttpClient reports for its own timeout: a cancellation with the caller's token live.
    public static Exception Timeout() =>
        new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout.", new TimeoutException());

    public static Exception ServerError() => new HttpRequestException("MyAnimeList is down.", null, HttpStatusCode.InternalServerError);

    public static Exception NotFound() => new HttpRequestException("Not found.", null, HttpStatusCode.NotFound);

    /// <summary>A failure that counts as a strike against the service, the way
    /// <c>MalAuthPacingHandler</c> and <c>AniListClient</c> count one.</summary>
    public static bool IsTemporary(Exception ex) => ex switch
    {
        HttpRequestException { StatusCode: HttpStatusCode.NotFound } => false,
        HttpRequestException => true,
        OperationCanceledException => true,
        _ => false,
    };
}

/// <summary>MyAnimeList, scripted: the list a test puts in <see cref="Edges"/>, the details it
/// puts in <see cref="Details"/>, and the failures it queues. It feeds the service's health the
/// way the real HTTP layer does, one strike for a temporary failure and a reset for any answer,
/// so a test sees the queue pause and resume on real health state.</summary>
internal sealed class ScriptedMalClient(MalServiceHealth health) : IMalClient, IMalSetupClient
{
    public List<MalUserAnimeListEdge> Edges { get; } = [];
    public int PageSize { get; set; } = 100;
    public int? Count { get; set; }
    public bool CountFails { get; set; }
    public Exception? ListFailure { get; set; }
    public int ListReads { get; private set; }
    public int CountCalls { get; private set; }

    public Dictionary<int, MalAnimeNode> Details { get; } = [];
    public ConcurrentDictionary<int, ConcurrentQueue<Exception>> DetailsFailures { get; } = [];
    public ConcurrentQueue<int> DetailsCalls { get; } = [];

    /// <summary>Awaited before every details answer: a test blocks the step here.</summary>
    public Func<int, CancellationToken, Task>? BeforeDetails { get; set; }

    /// <summary>Awaited before every list page is handed over, with the page's number from 1: a test
    /// moves the clock or blocks the read here.</summary>
    public Func<int, CancellationToken, Task>? BeforePage { get; set; }

    public void FailDetails(int animeId, params Exception[] failures)
    {
        var queue = DetailsFailures.GetOrAdd(animeId, _ => new ConcurrentQueue<Exception>());
        foreach (var failure in failures)
            queue.Enqueue(failure);
    }

    public async IAsyncEnumerable<List<MalUserAnimeListEdge>> GetUserAnimeListPagesAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        ListReads++;
        if (ListFailure is { } failure)
        {
            if (Failures.IsTemporary(failure))
                health.RecordTemporaryFailure();
            throw failure;
        }

        var snapshot = Edges.ToList();
        for (var offset = 0; offset < snapshot.Count; offset += PageSize)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Yield();
            if (BeforePage is { } beforePage)
                await beforePage(offset / PageSize + 1, ct);

            health.RecordSuccess();
            yield return snapshot.Skip(offset).Take(PageSize).ToList();
        }

        if (snapshot.Count == 0)
            yield return [];
    }

    public Task<int?> GetUserAnimeCountAsync(CancellationToken ct = default)
    {
        CountCalls++;
        if (CountFails)
            throw Failures.ServerError();
        return Task.FromResult(Count);
    }

    public async Task<MalAnimeNode> GetAnimeDetailsAsync(int animeId, IReadOnlyCollection<string>? fields = null, CancellationToken ct = default)
    {
        DetailsCalls.Enqueue(animeId);
        if (BeforeDetails is { } before)
            await before(animeId, ct);

        if (DetailsFailures.TryGetValue(animeId, out var queue) && queue.TryDequeue(out var failure))
        {
            if (Failures.IsTemporary(failure))
                health.RecordTemporaryFailure();
            else
                health.RecordSuccess(); // a 404 is an answer
            throw failure;
        }

        health.RecordSuccess();
        if (Details.TryGetValue(animeId, out var node))
            return node;

        // What MyAnimeList would say about the same anime: what the list said, with a title that
        // tells a full row from a basic one.
        var listed = Edges.FirstOrDefault(e => e.Node.Id == animeId)?.Node;
        return new MalAnimeNode
        {
            Id = animeId,
            Title = $"Full {animeId}",
            Status = listed?.Status ?? "finished_airing",
            StartDate = listed?.StartDate,
            NumEpisodes = 12,
            Pictures = [],
        };
    }

    public Task<MalPagedResponse<MalAnimeListEdge>> SearchAnimeAsync(string query, int limit = 5, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<MalPagedResponse<MalAnimeListEdge>> GetSeasonAsync(int year, string season, int limit = 100, int offset = 0, string? sort = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<List<MalAnimeListEdge>?> GetFullSeasonAsync(int year, string season, string? sort = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<MalPagedResponse<MalAnimeListEdge>> GetRankingAsync(string rankingType = "all", int limit = 100, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<MalPagedResponse<MalUserAnimeListEdge>> GetUserAnimeListAsync(string? status = null, int limit = 100, int offset = 0, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<List<MalUserAnimeListEdge>> GetFullUserAnimeListAsync(Action<int>? onPageRead = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<MalListStatus> UpdateMyListStatusAsync(int animeId, MalListStatusUpdate update, CancellationToken ct = default) => throw new NotImplementedException();
    public Task DeleteMyListStatusAsync(int animeId, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<MalListStatus?> GetMyListStatusAsync(int animeId, CancellationToken ct = default) => throw new NotImplementedException();
}

/// <summary>The series service, scripted. By default a build settles the seed's own franchise: one
/// stored, non-partial series holding every member in <see cref="Franchises"/> (or just the seed).
/// A test says which anime are lone, which builds fail, and which come back partial.</summary>
internal sealed class ScriptedSeriesService(DbContextOptions<AnimeTrackerDbContext> store, MalServiceHealth health) : ISeriesService
{
    public ConcurrentQueue<int> BuildCalls { get; } = [];
    public Dictionary<int, int[]> Franchises { get; } = [];
    public HashSet<int> Lone { get; } = [];
    public ConcurrentDictionary<int, ConcurrentQueue<SetupBuildOutcome>> Scripted { get; } = [];
    public ConcurrentDictionary<int, ConcurrentQueue<Exception>> Failing { get; } = [];
    public Func<int, CancellationToken, Task>? BeforeBuild { get; set; }

    public void Script(int seed, params SetupBuildOutcome[] outcomes)
    {
        var queue = Scripted.GetOrAdd(seed, _ => new ConcurrentQueue<SetupBuildOutcome>());
        foreach (var outcome in outcomes)
            queue.Enqueue(outcome);
    }

    public void Fail(int seed, params Exception[] failures)
    {
        var queue = Failing.GetOrAdd(seed, _ => new ConcurrentQueue<Exception>());
        foreach (var failure in failures)
            queue.Enqueue(failure);
    }

    public async Task<SetupBuildOutcome> BuildForSetupAsync(int animeId, CancellationToken ct = default)
    {
        BuildCalls.Enqueue(animeId);
        if (BeforeBuild is { } before)
            await before(animeId, ct);

        if (Failing.TryGetValue(animeId, out var failures) && failures.TryDequeue(out var failure))
        {
            if (Failures.IsTemporary(failure))
                health.RecordTemporaryFailure();
            throw failure;
        }

        health.RecordSuccess();
        var outcome = Scripted.TryGetValue(animeId, out var scripted) && scripted.TryDequeue(out var next)
            ? next
            : Lone.Contains(animeId) ? SetupBuildOutcome.NoSeries : SetupBuildOutcome.Settled;

        if (outcome != SetupBuildOutcome.NoSeries)
            await StoreSeriesAsync(animeId, partial: outcome == SetupBuildOutcome.Partial, ct);
        return outcome;
    }

    private async Task StoreSeriesAsync(int seed, bool partial, CancellationToken ct)
    {
        var members = Franchises.TryGetValue(seed, out var franchise) ? franchise : [seed];
        var seriesId = members.Min();

        await using var db = new AnimeTrackerDbContext(store);
        var series = await db.Series.FirstOrDefaultAsync(s => s.Id == seriesId, ct);
        if (series is null)
        {
            db.Series.Add(new Models.Series { Id = seriesId, BuiltAt = DateTimeOffset.UtcNow, IsPartial = partial });
            foreach (var member in members)
                db.SeriesMembers.Add(new SeriesMember { SeriesId = seriesId, AnimeId = member, IsPrimary = true, IsMainLine = true });
        }
        else
        {
            series.IsPartial = partial;
            series.BuiltAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(ct);
    }

    public Task<SeriesDto> GetSeriesAsync(int animeId, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<SeriesDto> RebuildSeriesAsync(int animeId, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<int?> FindSeriesIdAsync(int animeId, CancellationToken ct = default) => throw new NotImplementedException();
}

/// <summary>The airing refresh service, scripted: every anime asked for is marked fetched, unless a
/// test fails it. A wholly failed batch is one strike against AniList, as one failed request is.</summary>
internal sealed class ScriptedAiringService(DbContextOptions<AnimeTrackerDbContext> store, AniListServiceHealth health)
    : IEpisodeScheduleRefreshService
{
    private readonly object _lock = new();
    public List<IReadOnlyList<int>> Batches { get; } = [];
    public HashSet<int> Failing { get; } = [];
    public int FailWholeBatches { get; set; }

    /// <summary>Awaited at the start of every batch: a test blocks the step here.</summary>
    public Func<IReadOnlyList<int>, CancellationToken, Task>? BeforeBatch { get; set; }

    public IReadOnlyList<int> AskedFor
    {
        get { lock (_lock) return Batches.SelectMany(b => b).ToList(); }
    }

    public async Task<RefreshBatchResult> RefreshBatchAsync(
        IReadOnlyList<int> animeIds, RefreshBatchOptions? options = null, CancellationToken ct = default)
    {
        lock (_lock)
            Batches.Add(animeIds.ToList());
        if (BeforeBatch is { } before)
            await before(animeIds, ct);

        bool wholeBatchFails;
        lock (_lock)
        {
            wholeBatchFails = FailWholeBatches > 0;
            if (wholeBatchFails)
                FailWholeBatches--;
        }

        if (wholeBatchFails)
            health.RecordTemporaryFailure();
        else
            health.RecordSuccess();

        int fetched = 0, failed = 0;
        await using var db = new AnimeTrackerDbContext(store);
        foreach (var animeId in animeIds)
        {
            AiringRefreshOutcome outcome;
            lock (_lock)
                outcome = wholeBatchFails || Failing.Contains(animeId) ? AiringRefreshOutcome.Failed : AiringRefreshOutcome.Fetched;

            if (outcome == AiringRefreshOutcome.Failed)
            {
                failed++;
            }
            else
            {
                fetched++;
                var sync = await db.AnimeAiringSyncs.FirstOrDefaultAsync(s => s.AnimeId == animeId, ct);
                if (sync is null)
                    db.AnimeAiringSyncs.Add(new AnimeAiringSync { AnimeId = animeId, AniListId = 900 + animeId, LastFetchedAt = DateTimeOffset.UtcNow });
                else
                    sync.LastFetchedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct);
            }

            options?.OnAnimeDone?.Invoke(animeId, outcome);
        }

        return new RefreshBatchResult(fetched, 0, failed);
    }

    public Task RefreshOneAsync(int animeId, bool relookupAbsent = false, CancellationToken ct = default) => throw new NotImplementedException();
    public Task CatchUpAsync(CancellationToken ct = default) => throw new NotImplementedException();
    public Task<List<int>> GetTrackedAnimeIdsAsync(CancellationToken ct = default) => throw new NotImplementedException();
    public Task<List<int>> GetFullRefreshTargetsAsync(bool force, CancellationToken ct = default) => throw new NotImplementedException();
}

internal sealed class CountingSearchIndex : IAnimeSearchIndex
{
    private int _invalidations;
    public int Invalidations => Volatile.Read(ref _invalidations);

    public Task<List<AnimeTitleProjection>> GetAsync(CancellationToken ct = default) => throw new NotImplementedException();
    public void Invalidate() => Interlocked.Increment(ref _invalidations);
}

/// <summary>Everything one process of setup stands on, over an in-memory store a test seeds and
/// reads directly: the scripted services, the gate, the run state, and both services' health on one
/// frozen clock. Two kits over the same <see cref="Store"/> are two processes over one database, which
/// is how a restart is tested.</summary>
internal sealed class SetupRunKit : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly List<SetupCoordinator> _started = [];
    private DateTimeOffset _now = new(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);

    public SetupRunKit(DbContextOptions<AnimeTrackerDbContext>? store = null)
    {
        Store = store ?? new DbContextOptionsBuilder<AnimeTrackerDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        MalHealth = new MalServiceHealth(() => _now);
        AniListHealth = new AniListServiceHealth(() => _now);
        Mal = new ScriptedMalClient(MalHealth);
        Series = new ScriptedSeriesService(Store, MalHealth);
        Airing = new ScriptedAiringService(Store, AniListHealth);

        var services = new ServiceCollection();
        services.AddScoped(_ => new AnimeTrackerDbContext(Store));
        services.AddSingleton<IMalClient>(Mal);
        services.AddSingleton<IMalSetupClient>(Mal);
        services.AddSingleton<ISeriesService>(Series);
        services.AddSingleton<IEpisodeScheduleRefreshService>(Airing);
        services.AddSingleton<IAnimeSearchIndex>(SearchIndex);
        services.AddScoped<IMalTokenStore>(_ => Tokens);
        services.AddSingleton<IBroadcastLocalTimeConverter, BroadcastLocalTimeConverter>();
        _provider = services.BuildServiceProvider();

        ScopeFactory = _provider.GetRequiredService<IServiceScopeFactory>();
        Gate = new SetupGate(ScopeFactory);
        Context = new SetupWorkerContext(ScopeFactory, State, Wake, Progress, () => _now, Log);
    }

    public DbContextOptions<AnimeTrackerDbContext> Store { get; }
    public IServiceScopeFactory ScopeFactory { get; }
    public MalServiceHealth MalHealth { get; }
    public AniListServiceHealth AniListHealth { get; }
    public ScriptedMalClient Mal { get; }
    public ScriptedSeriesService Series { get; }
    public ScriptedAiringService Airing { get; }
    public CountingSearchIndex SearchIndex { get; } = new();
    public FakeTokenStore Tokens { get; } = new() { Token = FakeTokenStore.StoredToken() };
    public MalOptions MalOptions { get; } = new() { ClientId = "id", ClientSecret = "secret" };
    public SetupGate Gate { get; }
    public SetupTrigger Trigger { get; } = new();
    public SetupRunState State { get; } = new();
    public SetupWake Wake { get; } = new();
    public SetupWake Progress { get; } = new();
    public CapturingLogger<SetupCoordinator> Log { get; } = new();
    public SetupWorkerContext Context { get; }

    public DateTimeOffset Now
    {
        get => _now;
        set => _now = value;
    }

    public AnimeTrackerDbContext NewDb() => new(Store);

    public SetupCoordinator NewCoordinator() =>
        new(Gate, Trigger, State, Options.Create(MalOptions), ScopeFactory, MalHealth, AniListHealth, Log, () => _now);

    public SetupStatusService NewStatusService(AnimeTrackerDbContext db, ISetupCoordinator coordinator) =>
        new(db, Gate, coordinator, Options.Create(MalOptions), Tokens, MalHealth, AniListHealth, new BroadcastLocalTimeConverter());

    /// <summary>Starts a coordinator and remembers it, so <see cref="Dispose"/> stops it.</summary>
    public async Task<SetupCoordinator> StartAsync(SetupCoordinator? coordinator = null)
    {
        coordinator ??= NewCoordinator();
        await coordinator.StartAsync(CancellationToken.None);
        _started.Add(coordinator);
        return coordinator;
    }

    // --- seeding ---

    /// <summary>A list edge as MyAnimeList's list read returns it: the node the basic row is built
    /// from and the list status.</summary>
    public static MalUserAnimeListEdge Edge(
        int id, string status = "completed", string? airing = "finished_airing", string? startDate = null, string? title = null) => new()
    {
        Node = new MalAnimeNode
        {
            Id = id,
            Title = title ?? $"Basic {id}",
            Status = airing,
            StartDate = startDate,
            NumEpisodes = 12,
            Genres = [new MalGenre { Id = 1, Name = "Action" }],
            Synopsis = "From the list.",
            Source = "manga",
        },
        ListStatus = new MalListStatus { Status = status, Score = 8, NumEpisodesWatched = 3 },
    };

    /// <summary>A list anime already stored: a basic row, or with <paramref name="fetched"/> a fully
    /// fetched one, and its entry.</summary>
    public async Task SeedAnimeAsync(
        int id, WatchStatus status = WatchStatus.Completed, bool fetched = false, bool notOnMal = false, bool marked = false,
        string? airing = "finished_airing", DateOnly? airedFrom = null, string? title = null)
    {
        await using var db = NewDb();
        db.AnimeMetadata.Add(new AnimeMetadata
        {
            Id = id,
            Title = title ?? $"Anime {id}",
            AiringStatus = airing,
            AiredFrom = airedFrom,
            LastSyncedAt = fetched ? DateTimeOffset.UtcNow : default,
            LastRefreshFailedAt = notOnMal ? DateTimeOffset.UtcNow : null,
        });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = id, Status = status });
        if (marked)
            db.AnimeAiringSyncs.Add(new AnimeAiringSync { AnimeId = id, AniListId = 900 + id, LastFetchedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
    }

    /// <summary>A stored, up-to-date series holding these anime, primary and not partial.</summary>
    public async Task SeedSeriesAsync(int seriesId, params int[] memberIds)
    {
        await using var db = NewDb();
        db.Series.Add(new Models.Series { Id = seriesId, BuiltAt = SeriesGraphBuilder.ClassificationRevisedAt.AddDays(30) });
        foreach (var animeId in memberIds)
            db.SeriesMembers.Add(new SeriesMember { SeriesId = seriesId, AnimeId = animeId, IsPrimary = true, IsMainLine = true });
        await db.SaveChangesAsync();
    }

    // --- waiting ---

    public static async Task WaitUntilAsync(Func<bool> condition, string what = "the condition")
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!condition() && !timeout.IsCancellationRequested)
            await Task.Delay(10, CancellationToken.None);
        Assert.True(condition(), $"Timed out waiting for {what}.");
    }

    public static async Task WaitUntilAsync(Func<Task<bool>> condition, string what = "the condition")
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!await condition() && !timeout.IsCancellationRequested)
            await Task.Delay(10, CancellationToken.None);
        Assert.True(await condition(), $"Timed out waiting for {what}.");
    }

    /// <summary>True when nothing more happens for a moment: what a test waits out before it asserts
    /// that something did NOT happen.</summary>
    public static Task Settle() => Task.Delay(TimeSpan.FromMilliseconds(250));

    public void Dispose()
    {
        foreach (var coordinator in _started)
            coordinator.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
        _provider.Dispose();
    }
}
