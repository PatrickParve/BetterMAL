using AnimeTracker.Api.Services.Relations;
using AnimeTracker.Api.Tests.Services.Setup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimeTracker.Api.Tests.Services.Relations;

// The ten-minute AniList relation lookup. Its first pass is due at once, which on
// a fresh install would compete with setup's airing step for AniList's pace, so it
// waits for setup to finish (add-first-run-setup design D2).
public class RelationAdjudicationBackgroundServiceTests
{
    [Fact]
    public async Task NoLookupRunsUntilSetupHasFinished_ThenTheFirstPassRunsWithoutARestart()
    {
        var adjudication = new RecordingAdjudicationService();
        var services = new ServiceCollection();
        services.AddScoped<IRelationAdjudicationService>(_ => adjudication);
        using var provider = services.BuildServiceProvider();

        var gate = TestSetupGates.Unfinished();
        var service = new RelationAdjudicationBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(), gate, NullLogger<RelationAdjudicationBackgroundService>.Instance);

        await service.StartAsync(CancellationToken.None);
        try
        {
            await TestSetupGates.LetItRunAsync();
            Assert.Equal(0, adjudication.Passes);

            await gate.MarkFinishedAsync();
            await TestSetupGates.WaitForAsync(() => adjudication.Passes > 0);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task OnAFinishedInstallTheFirstPassRunsAtStart()
    {
        var adjudication = new RecordingAdjudicationService();
        var services = new ServiceCollection();
        services.AddScoped<IRelationAdjudicationService>(_ => adjudication);
        using var provider = services.BuildServiceProvider();

        var service = new RelationAdjudicationBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(), TestSetupGates.Finished(),
            NullLogger<RelationAdjudicationBackgroundService>.Instance);

        await service.StartAsync(CancellationToken.None);
        try
        {
            await TestSetupGates.WaitForAsync(() => adjudication.Passes > 0);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    private sealed class RecordingAdjudicationService : IRelationAdjudicationService
    {
        private int _passes;

        public int Passes => Volatile.Read(ref _passes);

        public Task<int> RunBatchAsync(int batchSize, CancellationToken ct = default)
        {
            Interlocked.Increment(ref _passes);
            return Task.FromResult(0);
        }
    }
}
