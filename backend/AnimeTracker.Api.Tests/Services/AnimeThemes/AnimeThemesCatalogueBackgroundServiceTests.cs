using AnimeTracker.Api.Data;
using AnimeTracker.Api.Services.AnimeThemes;
using AnimeTracker.Api.Tests.Services.IdMapping;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AnimeTracker.Api.Tests.Services.AnimeThemes;

// anime-themes spec, "The catalogue is refreshed once a week, slowly": the hourly
// tick that runs it is a hosted service of its own, in a scope of its own, and a
// failure in it is logged and the loop carries on. Driven over a real DI container
// so the scopes, and the DbContext each one owns, are the production ones. The
// hourly delay is the same code the other services' loops use; these tests cover
// what an iteration does and that the loop survives a failure.
public class AnimeThemesCatalogueBackgroundServiceTests
{
    private sealed class Calls
    {
        public List<Guid> ContextIds { get; } = [];
    }

    // The sync is a fake, resolved per scope: what it does is the sync's own tests'
    // business. Nothing from first-run setup, MyAnimeList or AniList is registered, so
    // resolving any of it would fail.
    private static ServiceProvider BuildProvider(Calls calls, Func<AnimeThemesCatalogueSyncOutcome>? sync)
    {
        var services = new ServiceCollection();
        services.AddDbContext<AnimeTrackerDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        if (sync is not null)
        {
            services.AddScoped<IAnimeThemesCatalogueSyncService>(sp =>
                new FakeSync(sp.GetRequiredService<AnimeTrackerDbContext>(), calls, sync));
        }

        return services.BuildServiceProvider();
    }

    private static AnimeThemesCatalogueBackgroundService CreateService(
        ServiceProvider provider, CapturingLogger<AnimeThemesCatalogueBackgroundService>? logger = null) =>
        new(provider.GetRequiredService<IServiceScopeFactory>(), logger ?? new CapturingLogger<AnimeThemesCatalogueBackgroundService>());

    private static async Task WaitForAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition() && !timeout.IsCancellationRequested)
            await Task.Delay(10, CancellationToken.None);
    }

    [Fact]
    public async Task AnIterationRunsTheSyncInAScopeOfItsOwn()
    {
        var calls = new Calls();
        using var provider = BuildProvider(calls, () => AnimeThemesCatalogueSyncOutcome.Synced);
        var service = CreateService(provider);

        await service.RunIterationAsync(CancellationToken.None);
        await service.RunIterationAsync(CancellationToken.None);

        // Its own DbContext every time, so a failed save in one tick can't leave
        // tracked entities behind for the next.
        Assert.Equal(2, calls.ContextIds.Count);
        Assert.NotEqual(calls.ContextIds[0], calls.ContextIds[1]);
    }

    [Theory]
    [InlineData(AnimeThemesCatalogueSyncOutcome.Synced)]
    [InlineData(AnimeThemesCatalogueSyncOutcome.NotDue)]
    [InlineData(AnimeThemesCatalogueSyncOutcome.Failed)]
    public async Task AnyOutcomeIsAQuietIteration(AnimeThemesCatalogueSyncOutcome outcome)
    {
        var calls = new Calls();
        using var provider = BuildProvider(calls, () => outcome);

        await CreateService(provider).RunIterationAsync(CancellationToken.None);

        Assert.Single(calls.ContextIds);
    }

    [Fact]
    public async Task ItNeedsNothingFromSetupMyAnimeListOrAniList()
    {
        // The provider registers none of them, and the iteration still runs: it calls
        // AnimeThemes and nothing else, so it doesn't wait for first-run setup.
        var calls = new Calls();
        using var provider = BuildProvider(calls, () => AnimeThemesCatalogueSyncOutcome.Synced);

        await CreateService(provider).RunIterationAsync(CancellationToken.None);

        Assert.Single(calls.ContextIds);
    }

    [Fact]
    public async Task TheFirstIterationRunsAtOnceOnStart()
    {
        var calls = new Calls();
        using var provider = BuildProvider(calls, () => AnimeThemesCatalogueSyncOutcome.Synced);
        var service = CreateService(provider);

        await service.StartAsync(CancellationToken.None);
        try
        {
            await WaitForAsync(() => calls.ContextIds.Count > 0);

            Assert.Single(calls.ContextIds);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task ACancellationOfTheCallerEndsAnIteration()
    {
        using var cts = new CancellationTokenSource();
        var calls = new Calls();
        using var provider = BuildProvider(calls, () =>
        {
            cts.Cancel();
            throw new OperationCanceledException(cts.Token);
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateService(provider).RunIterationAsync(cts.Token));
    }

    // --- The loop ---

    [Fact]
    public async Task AFailureIsLoggedAndTheLoopCarriesOn()
    {
        var calls = new Calls();
        var logger = new CapturingLogger<AnimeThemesCatalogueBackgroundService>();
        using var provider = BuildProvider(calls, () => throw new InvalidOperationException("the sync itself blew up"));
        var service = CreateService(provider, logger);

        await service.StartAsync(CancellationToken.None);
        try
        {
            await WaitForAsync(() => logger.Lines.Any(l => l.StartsWith("Error", StringComparison.Ordinal)));

            Assert.Contains(logger.Lines, l => l.StartsWith("Error", StringComparison.Ordinal) && l.Contains("AnimeThemes catalogue sync", StringComparison.Ordinal));

            // Give an exiting loop time to finish; a live one sits in its hourly delay.
            await Task.WhenAny(service.ExecuteTask!, Task.Delay(500, CancellationToken.None));
            Assert.False(service.ExecuteTask!.IsCompleted);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task ASyncThatCannotBeResolvedIsLoggedAndTheLoopCarriesOn()
    {
        var calls = new Calls();
        var logger = new CapturingLogger<AnimeThemesCatalogueBackgroundService>();
        using var provider = BuildProvider(calls, sync: null); // resolving it fails like a missing dependency
        var service = CreateService(provider, logger);

        await service.StartAsync(CancellationToken.None);
        try
        {
            await WaitForAsync(() => logger.Lines.Any(l => l.StartsWith("Error", StringComparison.Ordinal)));

            Assert.Contains(logger.Lines, l => l.StartsWith("Error", StringComparison.Ordinal));
            await Task.WhenAny(service.ExecuteTask!, Task.Delay(500, CancellationToken.None));
            Assert.False(service.ExecuteTask!.IsCompleted);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    // An HttpClient timeout is a TaskCanceledException while the stopping token is
    // still live. It must count as a failed tick, not as a shutdown that ends the
    // loop until the next restart.
    [Fact]
    public async Task ATimeoutDuringATickDoesNotStopTheHourlyLoop()
    {
        var calls = new Calls();
        using var provider = BuildProvider(calls, () => throw new TaskCanceledException(
            "The request was canceled due to the configured HttpClient.Timeout of 60 seconds elapsing.", new TimeoutException()));
        var service = CreateService(provider);

        await service.StartAsync(CancellationToken.None);
        try
        {
            await WaitForAsync(() => calls.ContextIds.Count > 0);
            Assert.Single(calls.ContextIds);

            await Task.WhenAny(service.ExecuteTask!, Task.Delay(500, CancellationToken.None));
            Assert.False(service.ExecuteTask!.IsCompleted);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task StoppingEndsTheLoop()
    {
        var calls = new Calls();
        using var provider = BuildProvider(calls, () => AnimeThemesCatalogueSyncOutcome.Synced);
        var service = CreateService(provider);

        await service.StartAsync(CancellationToken.None);
        await WaitForAsync(() => calls.ContextIds.Count > 0);
        await service.StopAsync(CancellationToken.None);

        Assert.True(service.ExecuteTask!.IsCompleted);
    }

    private sealed class FakeSync(AnimeTrackerDbContext db, Calls calls, Func<AnimeThemesCatalogueSyncOutcome> run)
        : IAnimeThemesCatalogueSyncService
    {
        public Task<AnimeThemesCatalogueSyncOutcome> SyncIfDueAsync(CancellationToken ct = default)
        {
            calls.ContextIds.Add(db.ContextId.InstanceId);
            return Task.FromResult(run());
        }
    }
}
