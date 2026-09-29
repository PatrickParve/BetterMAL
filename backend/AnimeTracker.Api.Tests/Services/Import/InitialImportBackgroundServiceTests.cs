using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Import;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Mal.Auth;
using AnimeTracker.Api.Tests.Services.Setup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimeTracker.Api.Tests.Services.Import;

// InitialImportBackgroundService (design.md D9, tasks.md 6.3): retries a run
// that couldn't finish on the injected schedule, resets on a genuine trigger
// signal, and neither runs nor plans a retry while the connection is lost.
public class InitialImportBackgroundServiceTests
{
    private static OAuthToken HealthyToken() => new()
    {
        AccessToken = "access",
        RefreshToken = "refresh",
        ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
        UpdatedAt = DateTimeOffset.UtcNow,
        ConnectionLostAt = null,
    };

    private static List<TimeSpan> Schedule(params int[] milliseconds) =>
        milliseconds.Select(ms => TimeSpan.FromMilliseconds(ms)).ToList();

    private static async Task WaitForCallCountAsync(FakeInitialImportService service, int count, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        while (service.CallCount < count && !cts.IsCancellationRequested)
            await Task.Delay(5, CancellationToken.None);

        Assert.True(service.CallCount >= count, $"Expected at least {count} calls, saw {service.CallCount}.");
    }

    private static async Task WaitForAsync(Func<bool> condition, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        while (!condition() && !cts.IsCancellationRequested)
            await Task.Delay(5, CancellationToken.None);

        Assert.True(condition());
    }

    [Fact]
    public async Task AListReadFailureIsRetriedOnScheduleAndStopsAfterTheFifth()
    {
        var progress = new ListImportProgress();
        var importService = new FakeInitialImportService(progress); // always fails a read
        var trigger = new ImportTrigger();
        var scopeFactory = new FakeServiceScopeFactory(importService, new FakeMalTokenStore(HealthyToken()));
        var service = new InitialImportBackgroundService(
            scopeFactory, trigger, progress, TestSetupGates.Finished(), NullLogger<InitialImportBackgroundService>.Instance,
            Schedule(20, 25, 30, 35, 40));

        await service.StartAsync(CancellationToken.None);
        try
        {
            // The initial run plus five scheduled retries = six attempts.
            await WaitForCallCountAsync(importService, 6, TimeSpan.FromSeconds(5));

            await Task.Delay(150, CancellationToken.None); // well past the longest (40ms) delay
            Assert.Equal(6, importService.CallCount);
            Assert.Null(progress.Snapshot.RetryAt);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task ACleanRunEndsTheSequence()
    {
        var progress = new ListImportProgress();
        var importService = new FakeInitialImportService(progress)
        {
            OnRun = callNumber =>
            {
                progress.BeginRun(visibleFromStart: false);
                if (callNumber <= 2)
                {
                    progress.FailBeforeRead("MyAnimeList couldn't be reached.");
                    throw new HttpRequestException("boom");
                }

                progress.EndWithoutWork(); // a clean run: nothing missing
                return Task.CompletedTask;
            },
        };
        var trigger = new ImportTrigger();
        var scopeFactory = new FakeServiceScopeFactory(importService, new FakeMalTokenStore(HealthyToken()));
        var service = new InitialImportBackgroundService(
            scopeFactory, trigger, progress, TestSetupGates.Finished(), NullLogger<InitialImportBackgroundService>.Instance,
            Schedule(20, 20, 20, 20, 20));

        await service.StartAsync(CancellationToken.None);
        try
        {
            await WaitForCallCountAsync(importService, 3, TimeSpan.FromSeconds(5));

            await Task.Delay(150, CancellationToken.None); // no retry should follow a clean run
            Assert.Equal(3, importService.CallCount);
            Assert.Null(progress.Snapshot.RetryAt);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task ATriggerSignalResetsTheSequence()
    {
        var progress = new ListImportProgress();
        var importService = new FakeInitialImportService(progress); // always fails
        var trigger = new ImportTrigger();
        var scopeFactory = new FakeServiceScopeFactory(importService, new FakeMalTokenStore(HealthyToken()));
        // A short first delay, then a very long second one that the test
        // would never wait out — only a genuine signal can produce a third
        // run within the test's bound, proving the sequence restarted at
        // the schedule's first entry rather than continuing to the second.
        var service = new InitialImportBackgroundService(
            scopeFactory, trigger, progress, TestSetupGates.Finished(), NullLogger<InitialImportBackgroundService>.Instance,
            Schedule(20, 20_000));

        await service.StartAsync(CancellationToken.None);
        try
        {
            await WaitForCallCountAsync(importService, 2, TimeSpan.FromSeconds(5)); // initial run + first scheduled retry, both failing

            trigger.Signal(); // e.g. a re-authorization
            await WaitForCallCountAsync(importService, 3, TimeSpan.FromMilliseconds(500)); // responded to the signal at once, not the 20s delay

            // The reset run also fails; the next retry uses the schedule's
            // first (short) entry again rather than its second (long) one.
            await WaitForCallCountAsync(importService, 4, TimeSpan.FromSeconds(2));
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task ALostTokenRowMeansNoRunAndNoRetry()
    {
        var progress = new ListImportProgress();
        var importService = new FakeInitialImportService(progress);
        var trigger = new ImportTrigger();
        var lostToken = HealthyToken();
        lostToken.ConnectionLostAt = DateTimeOffset.UtcNow;
        var scopeFactory = new FakeServiceScopeFactory(importService, new FakeMalTokenStore(lostToken));
        var service = new InitialImportBackgroundService(
            scopeFactory, trigger, progress, TestSetupGates.Finished(), NullLogger<InitialImportBackgroundService>.Instance,
            Schedule(20, 20, 20, 20, 20));

        await service.StartAsync(CancellationToken.None);
        try
        {
            await Task.Delay(200, CancellationToken.None); // plenty of time for a (wrongly) scheduled run
            Assert.Equal(0, importService.CallCount);
            Assert.Null(progress.Snapshot.RetryAt);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task AMalAuthorizationRequiredExceptionPlansNoRetry()
    {
        var progress = new ListImportProgress();
        var importService = new FakeInitialImportService(progress)
        {
            OnRun = _ =>
            {
                progress.BeginRun(visibleFromStart: false);
                progress.FailBeforeRead("The connection to MyAnimeList was lost.");
                throw new MalAuthorizationRequiredException();
            },
        };
        var trigger = new ImportTrigger();
        var scopeFactory = new FakeServiceScopeFactory(importService, new FakeMalTokenStore(HealthyToken()));
        var service = new InitialImportBackgroundService(
            scopeFactory, trigger, progress, TestSetupGates.Finished(), NullLogger<InitialImportBackgroundService>.Instance,
            Schedule(20, 20, 20, 20, 20));

        await service.StartAsync(CancellationToken.None);
        try
        {
            await WaitForCallCountAsync(importService, 1, TimeSpan.FromSeconds(2));

            await Task.Delay(150, CancellationToken.None); // well past every scheduled delay
            Assert.Equal(1, importService.CallCount);
            Assert.Null(progress.Snapshot.RetryAt);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task RetryAtIsPublishedWhileARetryIsPlanned()
    {
        var progress = new ListImportProgress();
        var importService = new FakeInitialImportService(progress); // always fails
        var trigger = new ImportTrigger();
        var scopeFactory = new FakeServiceScopeFactory(importService, new FakeMalTokenStore(HealthyToken()));
        var service = new InitialImportBackgroundService(
            scopeFactory, trigger, progress, TestSetupGates.Finished(), NullLogger<InitialImportBackgroundService>.Instance,
            Schedule(30)); // a single retry, so the schedule is exhausted right after it

        await service.StartAsync(CancellationToken.None);
        try
        {
            await WaitForCallCountAsync(importService, 1, TimeSpan.FromSeconds(2)); // the initial run fails

            await WaitForAsync(() => progress.Snapshot.RetryAt is not null, TimeSpan.FromSeconds(2));
            Assert.True(progress.Snapshot.RetryAt > DateTimeOffset.UtcNow.AddMilliseconds(-50));

            await WaitForCallCountAsync(importService, 2, TimeSpan.FromSeconds(2)); // the one scheduled retry runs, and fails too

            await Task.Delay(100, CancellationToken.None); // the schedule is now exhausted
            Assert.Null(progress.Snapshot.RetryAt);
            Assert.Equal(2, importService.CallCount);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    // A run that finds nothing missing, so no retry follows it and the call counts
    // below are only the runs the service chose to make.
    private static FakeInitialImportService CleanRunningImportService(ListImportProgress progress) =>
        new(progress)
        {
            OnRun = _ =>
            {
                progress.BeginRun(visibleFromStart: false);
                progress.EndWithoutWork();
                return Task.CompletedTask;
            },
        };

    // initial-import delta, "The import runs at every start": a finished install
    // has no gate to wait at, so it signals itself as it always has.
    [Fact]
    public async Task OnAFinishedInstallTheImportRunsAtStart()
    {
        var progress = new ListImportProgress();
        var importService = CleanRunningImportService(progress);
        var scopeFactory = new FakeServiceScopeFactory(importService, new FakeMalTokenStore(HealthyToken()));
        var service = new InitialImportBackgroundService(
            scopeFactory, new ImportTrigger(), progress, TestSetupGates.Finished(), NullLogger<InitialImportBackgroundService>.Instance,
            Schedule(20));

        await service.StartAsync(CancellationToken.None);
        try
        {
            await WaitForCallCountAsync(importService, 1, TimeSpan.FromSeconds(5));
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    // initial-import delta, "No run during setup" and "No second read right after
    // setup": nothing runs while the gate is open, setup has just read the list when
    // it closes so the at-start signal is skipped, and a later re-authorization still
    // runs it.
    [Fact]
    public async Task NoImportRunsDuringSetupNorRightAfterIt_ButAReAuthorizationStillRunsIt()
    {
        var progress = new ListImportProgress();
        var importService = CleanRunningImportService(progress);
        var trigger = new ImportTrigger();
        var gate = TestSetupGates.Unfinished();
        var scopeFactory = new FakeServiceScopeFactory(importService, new FakeMalTokenStore(HealthyToken()));
        var service = new InitialImportBackgroundService(
            scopeFactory, trigger, progress, gate, NullLogger<InitialImportBackgroundService>.Instance,
            Schedule(20));

        await service.StartAsync(CancellationToken.None);
        try
        {
            await TestSetupGates.LetItRunAsync();
            Assert.Equal(0, importService.CallCount); // setup reads the list

            await gate.MarkFinishedAsync();
            await TestSetupGates.LetItRunAsync();
            Assert.Equal(0, importService.CallCount); // setup has just read it

            trigger.Signal(); // a re-authorization
            await WaitForCallCountAsync(importService, 1, TimeSpan.FromSeconds(5));
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    // The wait is what holds the import, not only the skipped at-start signal: a
    // signal that reaches it while setup is unfinished does not run it early.
    [Fact]
    public async Task ASignalDuringSetupWaitsForTheGate()
    {
        var progress = new ListImportProgress();
        var importService = CleanRunningImportService(progress);
        var trigger = new ImportTrigger();
        var gate = TestSetupGates.Unfinished();
        var scopeFactory = new FakeServiceScopeFactory(importService, new FakeMalTokenStore(HealthyToken()));
        var service = new InitialImportBackgroundService(
            scopeFactory, trigger, progress, gate, NullLogger<InitialImportBackgroundService>.Instance,
            Schedule(20));

        await service.StartAsync(CancellationToken.None);
        try
        {
            trigger.Signal();
            await TestSetupGates.LetItRunAsync();
            Assert.Equal(0, importService.CallCount);

            await gate.MarkFinishedAsync();
            await WaitForCallCountAsync(importService, 1, TimeSpan.FromSeconds(5));
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    private sealed class FakeInitialImportService(ListImportProgress progress) : IInitialImportService
    {
        private readonly List<DateTimeOffset> _callTimes = [];

        public Func<int, Task>? OnRun { get; set; }

        public int CallCount
        {
            get
            {
                lock (_callTimes) return _callTimes.Count;
            }
        }

        public async Task RunAsync(CancellationToken ct)
        {
            int callNumber;
            lock (_callTimes)
            {
                _callTimes.Add(DateTimeOffset.UtcNow);
                callNumber = _callTimes.Count;
            }

            if (OnRun is not null)
            {
                await OnRun(callNumber);
                return;
            }

            progress.BeginRun(visibleFromStart: false);
            progress.FailBeforeRead("MyAnimeList couldn't be reached.");
            throw new HttpRequestException("boom");
        }
    }

    private sealed class FakeMalTokenStore(OAuthToken? token) : IMalTokenStore
    {
        public Task<OAuthToken?> GetAsync(CancellationToken ct = default) => Task.FromResult(token);
        public Task SaveAsync(string accessToken, string refreshToken, DateTimeOffset expiresAt, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task MarkConnectionLostAsync(DateTimeOffset at, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    private sealed class FakeServiceScopeFactory(IInitialImportService importService, IMalTokenStore tokenStore) : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new FakeServiceScope(importService, tokenStore);
    }

    private sealed class FakeServiceScope(IInitialImportService importService, IMalTokenStore tokenStore) : IServiceScope
    {
        public IServiceProvider ServiceProvider { get; } = new FakeServiceProvider(importService, tokenStore);
        public void Dispose() { }
    }

    private sealed class FakeServiceProvider(IInitialImportService importService, IMalTokenStore tokenStore) : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(IInitialImportService)) return importService;
            if (serviceType == typeof(IMalTokenStore)) return tokenStore;
            return null;
        }
    }
}
