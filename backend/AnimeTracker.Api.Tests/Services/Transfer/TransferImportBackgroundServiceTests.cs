using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Services.Artwork;
using AnimeTracker.Api.Services.Infrastructure;
using AnimeTracker.Api.Services.Metadata;
using AnimeTracker.Api.Services.Series;
using AnimeTracker.Api.Services.Transfer;
using AnimeTracker.Api.Tests.Services.Tmdb;
using AnimeTracker.Api.Tests.Services.Setup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimeTracker.Api.Tests.Services.Transfer;

// TransferImportBackgroundService (device-transfer design.md D1, tasks.md
// 4.4): waits on the trigger, runs the scoped runner, and reports the
// tracker's outcome. The file offered is empty, so TransferImportRunner's
// own prepare stage (tasks 6-7) does no work and reaches apply, where a
// throwing IServiceScopeFactory is what lets this test exercise the
// background service's own exception handling in isolation.
public class TransferImportBackgroundServiceTests
{
    private static TransferFile File() => new(
        1, new TransferDevice(Guid.NewGuid(), "Device"), DateTimeOffset.UtcNow,
        new TransferRanking([], null), [], [], []);

    [Fact]
    public async Task TheBackgroundServiceFailsTheTrackerWhenTheRunnerThrows()
    {
        var trigger = new TransferImportTrigger();
        var tracker = new TransferImportProgressTracker();
        var service = new TransferImportBackgroundService(
            new FakeServiceScopeFactory(new FakeServiceProvider()),
            trigger,
            tracker,
            TestSetupGates.Finished(), NullLogger<TransferImportBackgroundService>.Instance);

        await service.StartAsync(CancellationToken.None);
        try
        {
            tracker.MarkPending("Device", DateTimeOffset.UtcNow);
            trigger.TryOffer(File());

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (tracker.Snapshot.Phase != TransferImportPhase.Failed && !cts.IsCancellationRequested)
                await Task.Delay(10, CancellationToken.None);

            Assert.Equal(TransferImportPhase.Failed, tracker.Snapshot.Phase); // fail loudly on a test timeout, not a hang
            Assert.Equal("Nothing from the file was applied.", tracker.Snapshot.Error);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task TheSlotIsReleasedOnceTheRunEnds()
    {
        var trigger = new TransferImportTrigger();
        var tracker = new TransferImportProgressTracker();
        var service = new TransferImportBackgroundService(
            new FakeServiceScopeFactory(new FakeServiceProvider()),
            trigger,
            tracker,
            TestSetupGates.Finished(), NullLogger<TransferImportBackgroundService>.Instance);

        await service.StartAsync(CancellationToken.None);
        try
        {
            tracker.MarkPending("Device", DateTimeOffset.UtcNow);
            trigger.TryOffer(File());

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (tracker.Snapshot.Phase != TransferImportPhase.Failed && !cts.IsCancellationRequested)
                await Task.Delay(10, CancellationToken.None);
            Assert.Equal(TransferImportPhase.Failed, tracker.Snapshot.Phase);

            Assert.True(trigger.TryOffer(File())); // the slot was released, so a new offer succeeds
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    // add-first-run-setup design D2: the API refuses an upload until setup has
    // finished; an offer that arrived anyway is held, not dropped, and runs once
    // setup has finished.
    [Fact]
    public async Task NoImportRunsUntilSetupHasFinished_ThenTheOfferedFileRuns()
    {
        var trigger = new TransferImportTrigger();
        var tracker = new TransferImportProgressTracker();
        var gate = TestSetupGates.Unfinished();
        var service = new TransferImportBackgroundService(
            new FakeServiceScopeFactory(new FakeServiceProvider()),
            trigger, tracker, gate, NullLogger<TransferImportBackgroundService>.Instance);

        await service.StartAsync(CancellationToken.None);
        try
        {
            tracker.MarkPending("Device", DateTimeOffset.UtcNow);
            Assert.True(trigger.TryOffer(File()));
            await TestSetupGates.LetItRunAsync();
            Assert.NotEqual(TransferImportPhase.Failed, tracker.Snapshot.Phase);
            Assert.False(trigger.TryOffer(File())); // the file is still held, so the slot is taken

            await gate.MarkFinishedAsync();
            await TestSetupGates.WaitForAsync(() => tracker.Snapshot.Phase == TransferImportPhase.Failed);
            Assert.True(trigger.TryOffer(File())); // ran (this fake runner always fails), and released the slot
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    private sealed class FakeServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            if (serviceType != typeof(TransferImportRunner)) return null;

            // File() is empty, so prepare fetches nothing and resolves no
            // series — every dependency below except the ranking repository
            // (read once regardless, to check ranking candidacy) goes
            // unused. Apply then opens its own fresh scope, which is where
            // ThrowingScopeFactory throws.
            var db = new AnimeTrackerDbContext(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
            return new TransferImportRunner(
                db,
                new ThrowingScopeFactory(),
                new UnusedMetadataRefreshService(),
                new UnusedSeriesService(),
                new UnusedArtworkSelectionService(),
                new UnusedPictureRefreshService(),
                new FakeTmdbArtworkService(),
                new TopAnimeSelectionRepository(db),
                new RefreshGate());
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

    private sealed class ThrowingScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => throw new InvalidOperationException("Apply's fresh scope is unavailable in this test.");
    }

    private sealed class UnusedMetadataRefreshService : IMetadataRefreshService
    {
        public Task RefreshStaleBatchAsync(int batchSize, int? skipAnimeId, MalCallTally tally, CancellationToken ct = default) => throw new NotImplementedException();
        public Task RefreshOneAsync(int animeId, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class UnusedSeriesService : ISeriesService
    {
        public Task<SetupBuildOutcome> BuildForSetupAsync(int animeId, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<SeriesDto> GetSeriesAsync(int animeId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<SeriesDto> RebuildSeriesAsync(int animeId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int?> FindSeriesIdAsync(int animeId, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class UnusedArtworkSelectionService : IArtworkSelectionService
    {
        public Task<string?> SetAnimePictureAsync(int animeId, string pictureUrl, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<string?> ResetAnimePictureAsync(int animeId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<string?> AdoptAnimePictureAsync(int animeId, string? pictureUrl, DateTimeOffset modifiedAt, CancellationToken ct = default) => throw new NotImplementedException();
        public Task CheckAnimePictureAsync(int animeId, string pictureUrl, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<string> SetSeriesTitleAsync(int seriesId, string title, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<string?> ResetSeriesTitleAsync(int seriesId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<string?> AdoptSeriesTitleAsync(int seriesId, string? title, DateTimeOffset modifiedAt, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<string?> SetSeriesPictureAsync(int seriesId, string pictureUrl, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<string?> ResetSeriesPictureAsync(int seriesId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<string?> AdoptSeriesPictureAsync(int seriesId, string? pictureUrl, DateTimeOffset modifiedAt, CancellationToken ct = default) => throw new NotImplementedException();
        public Task CheckSeriesPictureAsync(int seriesId, string pictureUrl, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class UnusedPictureRefreshService : IPictureRefreshService
    {
        public Task<bool> RefreshOneAsync(int animeId, bool evenIfFetched = false, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> RefreshSeriesMainLineAsync(int seriesId, int budget, CancellationToken ct = default) => throw new NotImplementedException();
    }
}
