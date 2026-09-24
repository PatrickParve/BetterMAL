using System.Net;
using System.Text;
using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.IdMapping;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimeTracker.Api.Tests.Services.IdMapping;

// external-id-mapping spec: AnimeIdMappingSyncService over EF InMemory and a
// fake HttpMessageHandler. Time passing is simulated by rewinding the stored
// stamps, as the rest of this suite seeds times relative to UtcNow rather than
// injecting a clock.
public class AnimeIdMappingSyncServiceTests
{
    private static DbContextOptions<AnimeTrackerDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    // One TV entry per MAL id. The TV id derives from the MAL id, so a test can
    // change one by naming it.
    private static string MappingFile(IEnumerable<int> malIds, IReadOnlyDictionary<int, int>? tvIdOverrides = null) =>
        "[" + string.Join(",", malIds.Select(malId =>
        {
            var tvId = tvIdOverrides is not null && tvIdOverrides.TryGetValue(malId, out var overridden) ? overridden : malId + 1000;
            return $$"""{"mal_id":{{malId}},"themoviedb_id":{"tv":{{tvId}}},"season":{"tmdb":1},"imdb_id":["tt{{malId:D7}}"]}""";
        })) + "]";

    private static async Task<AnimeIdMappingSyncOutcome> SyncAsync(
        DbContextOptions<AnimeTrackerDbContext> options,
        FakeMappingHandler handler,
        CancellationToken ct = default,
        Func<AnimeTrackerDbContext>? createDb = null,
        ICustomIdMappings? custom = null,
        ILogger<AnimeIdMappingSyncService>? logger = null)
    {
        // A new context per call, as each hourly tick gets a scope of its own.
        using var db = createDb?.Invoke() ?? new AnimeTrackerDbContext(options);
        var service = new AnimeIdMappingSyncService(
            db, new FakeHttpClientFactory(handler), custom ?? new FakeCustomIdMappings(),
            logger ?? NullLogger<AnimeIdMappingSyncService>.Instance);
        return await service.SyncIfDueAsync(ct);
    }

    private static async Task<AnimeIdMappingSyncState> ReadStateAsync(DbContextOptions<AnimeTrackerDbContext> options)
    {
        using var db = new AnimeTrackerDbContext(options);
        return await db.AnimeIdMappingSyncStates.AsNoTracking().SingleAsync();
    }

    // "The last successful sync, and the last attempt, were both this long ago."
    private static Task AgeAsync(DbContextOptions<AnimeTrackerDbContext> options, TimeSpan age) =>
        SetStampsAsync(options, lastSyncedAgo: age, lastAttemptAgo: age);

    private static async Task SetStampsAsync(
        DbContextOptions<AnimeTrackerDbContext> options, TimeSpan lastSyncedAgo, TimeSpan lastAttemptAgo)
    {
        using var db = new AnimeTrackerDbContext(options);
        var state = await db.AnimeIdMappingSyncStates.SingleAsync();
        state.LastSyncedAt = DateTimeOffset.UtcNow - lastSyncedAgo;
        state.LastAttemptAt = DateTimeOffset.UtcNow - lastAttemptAgo;
        await db.SaveChangesAsync();
    }

    private static async Task<Dictionary<int, AnimeIdMapping>> ReadMappingsAsync(DbContextOptions<AnimeTrackerDbContext> options)
    {
        using var db = new AnimeTrackerDbContext(options);
        return await db.AnimeIdMappings.AsNoTracking().ToDictionaryAsync(m => m.AnimeId);
    }

    private static async Task<string> DescribeMappingsAsync(DbContextOptions<AnimeTrackerDbContext> options) =>
        string.Join("|", (await ReadMappingsAsync(options)).Values.OrderBy(m => m.AnimeId).Select(m =>
            $"{m.AnimeId}:{m.TmdbTvId}:{m.TmdbSeasonNumber}:[{string.Join(",", m.TmdbMovieIds)}]:[{string.Join(",", m.ImdbIds)}]"));

    private static void AssertJustNow(DateTimeOffset? stamp) =>
        Assert.InRange(stamp!.Value, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddSeconds(1));

    // --- Cadence ---

    [Fact]
    public async Task TheFirstSyncStoresTheRowsAndStampsTheState()
    {
        var options = CreateOptions();
        var handler = new FakeMappingHandler();
        handler.ServeFile(MappingFile([1, 2, 3]));

        var outcome = await SyncAsync(options, handler);

        Assert.Equal(AnimeIdMappingSyncOutcome.Synced, outcome);
        var rows = await ReadMappingsAsync(options);
        Assert.Equal([1, 2, 3], rows.Keys.Order());
        Assert.Equal(1002, rows[2].TmdbTvId);
        Assert.Equal(1, rows[2].TmdbSeasonNumber);
        Assert.Equal(["tt0000002"], rows[2].ImdbIds);
        var state = await ReadStateAsync(options);
        AssertJustNow(state.LastSyncedAt);
        Assert.Equal(state.LastSyncedAt, state.LastAttemptAt);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ThreeDaysLaterThereIsNoRequest()
    {
        var options = CreateOptions();
        var handler = new FakeMappingHandler();
        handler.ServeFile(MappingFile([1, 2, 3]));
        await SyncAsync(options, handler);
        await AgeAsync(options, TimeSpan.FromDays(3));
        handler.Requests.Clear();

        var outcome = await SyncAsync(options, handler);

        Assert.Equal(AnimeIdMappingSyncOutcome.NotDue, outcome);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task EightDaysLaterItSyncs()
    {
        var options = CreateOptions();
        var handler = new FakeMappingHandler();
        handler.ServeFile(MappingFile([1, 2, 3]));
        await SyncAsync(options, handler);
        await AgeAsync(options, TimeSpan.FromDays(8));
        handler.Requests.Clear();
        handler.ServeFile(MappingFile([1, 2, 3, 4]));

        var outcome = await SyncAsync(options, handler);

        Assert.Equal(AnimeIdMappingSyncOutcome.Synced, outcome);
        Assert.Single(handler.Requests);
        Assert.Equal([1, 2, 3, 4], (await ReadMappingsAsync(options)).Keys.Order());
        AssertJustNow((await ReadStateAsync(options)).LastSyncedAt);
    }

    // --- Failure and backoff ---

    // The shared shape of every failed download: the rows and LastSyncedAt stay
    // as they were, only LastAttemptAt moves, and the sync reports Failed.
    private static async Task AssertDownloadFailureIsRecordedAsAFailedAttemptAsync(Action<FakeMappingHandler> breakDownload)
    {
        var options = CreateOptions();
        var handler = new FakeMappingHandler();
        handler.ServeFile(MappingFile([1, 2, 3]));
        await SyncAsync(options, handler);
        await AgeAsync(options, TimeSpan.FromDays(8));
        var rowsBefore = await DescribeMappingsAsync(options);
        var stateBefore = await ReadStateAsync(options);
        breakDownload(handler);

        var outcome = await SyncAsync(options, handler);

        Assert.Equal(AnimeIdMappingSyncOutcome.Failed, outcome);
        Assert.Equal(rowsBefore, await DescribeMappingsAsync(options));
        var state = await ReadStateAsync(options);
        Assert.Equal(stateBefore.LastSyncedAt, state.LastSyncedAt);
        AssertJustNow(state.LastAttemptAt);
    }

    [Fact]
    public Task AFailedDownloadLeavesTheRowsAndLastSyncedAtUntouchedAndStampsLastAttemptAt() =>
        AssertDownloadFailureIsRecordedAsAFailedAttemptAsync(h => h.Respond = () => new HttpResponseMessage(HttpStatusCode.InternalServerError));

    [Fact]
    public Task ANetworkErrorIsAFailedAttempt() =>
        AssertDownloadFailureIsRecordedAsAFailedAttemptAsync(h => h.Respond = () => throw new HttpRequestException("network down"));

    [Fact]
    public Task ATimeoutIsAFailedAttemptNotACancellation() =>
        // HttpClient's own timeout surfaces as a TaskCanceledException while the
        // caller's token is still live. Treating it as a cancellation would let
        // it escape, leave no attempt recorded and re-download every hour.
        AssertDownloadFailureIsRecordedAsAFailedAttemptAsync(h => h.Respond = () => throw new TaskCanceledException("timed out"));

    [Fact]
    public async Task ARetryWithinSixHoursIsSkippedAndSevenHoursLaterItRuns()
    {
        var options = CreateOptions();
        var handler = new FakeMappingHandler();
        handler.ServeFile(MappingFile([1, 2, 3]));
        await SyncAsync(options, handler);
        await AgeAsync(options, TimeSpan.FromDays(8));
        handler.Respond = () => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        await SyncAsync(options, handler); // fails, stamping LastAttemptAt now
        handler.ServeFile(MappingFile([1, 2, 3, 4]));
        handler.Requests.Clear();

        Assert.Equal(AnimeIdMappingSyncOutcome.NotDue, await SyncAsync(options, handler));
        Assert.Empty(handler.Requests);

        await SetStampsAsync(options, lastSyncedAgo: TimeSpan.FromDays(8), lastAttemptAgo: TimeSpan.FromHours(5));
        Assert.Equal(AnimeIdMappingSyncOutcome.NotDue, await SyncAsync(options, handler));
        Assert.Empty(handler.Requests);

        await SetStampsAsync(options, lastSyncedAgo: TimeSpan.FromDays(8), lastAttemptAgo: TimeSpan.FromHours(7));
        Assert.Equal(AnimeIdMappingSyncOutcome.Synced, await SyncAsync(options, handler));
        Assert.Single(handler.Requests);
        Assert.Equal([1, 2, 3, 4], (await ReadMappingsAsync(options)).Keys.Order());
    }

    [Fact]
    public async Task AFailedFirstSyncIsRetriedOnlyAfterTheBackoffToo()
    {
        // Before any success there is no LastSyncedAt to compare the attempt
        // against, but the attempt is still a failed one.
        var options = CreateOptions();
        var handler = new FakeMappingHandler();
        handler.Respond = () => new HttpResponseMessage(HttpStatusCode.InternalServerError);
        Assert.Equal(AnimeIdMappingSyncOutcome.Failed, await SyncAsync(options, handler));
        var state = await ReadStateAsync(options);
        Assert.Null(state.LastSyncedAt);
        AssertJustNow(state.LastAttemptAt);
        handler.ServeFile(MappingFile([1, 2]));
        handler.Requests.Clear();

        Assert.Equal(AnimeIdMappingSyncOutcome.NotDue, await SyncAsync(options, handler));
        Assert.Empty(handler.Requests);

        using (var db = new AnimeTrackerDbContext(options))
        {
            var stored = await db.AnimeIdMappingSyncStates.SingleAsync();
            stored.LastAttemptAt = DateTimeOffset.UtcNow.AddHours(-7);
            await db.SaveChangesAsync();
        }

        Assert.Equal(AnimeIdMappingSyncOutcome.Synced, await SyncAsync(options, handler));
        Assert.Equal([1, 2], (await ReadMappingsAsync(options)).Keys.Order());
    }

    // --- A damaged file ---

    [Fact]
    public async Task ATruncatedFileChangesNothing()
    {
        var options = CreateOptions();
        var handler = new FakeMappingHandler();
        handler.ServeFile(MappingFile([1, 2, 3]));
        await SyncAsync(options, handler);
        await AgeAsync(options, TimeSpan.FromDays(8));
        var rowsBefore = await DescribeMappingsAsync(options);
        var stateBefore = await ReadStateAsync(options);
        var complete = MappingFile([1, 2, 3, 4, 5]);
        handler.ServeFile(complete[..(complete.Length - 40)]); // cut off inside the last entry

        var outcome = await SyncAsync(options, handler);

        Assert.Equal(AnimeIdMappingSyncOutcome.Failed, outcome);
        Assert.Equal(rowsBefore, await DescribeMappingsAsync(options));
        var state = await ReadStateAsync(options);
        Assert.Equal(stateBefore.LastSyncedAt, state.LastSyncedAt);
        AssertJustNow(state.LastAttemptAt);
    }

    [Fact]
    public async Task AFileUnderHalfTheStoredCountChangesNothing()
    {
        var options = CreateOptions();
        var handler = new FakeMappingHandler();
        handler.ServeFile(MappingFile(Enumerable.Range(1, 8000)));
        await SyncAsync(options, handler);
        await AgeAsync(options, TimeSpan.FromDays(8));
        var stateBefore = await ReadStateAsync(options);
        handler.ServeFile(MappingFile(Enumerable.Range(1, 2000)));

        var outcome = await SyncAsync(options, handler);

        Assert.Equal(AnimeIdMappingSyncOutcome.Failed, outcome);
        Assert.Equal(8000, (await ReadMappingsAsync(options)).Count);
        var state = await ReadStateAsync(options);
        Assert.Equal(stateBefore.LastSyncedAt, state.LastSyncedAt);
        AssertJustNow(state.LastAttemptAt);
    }

    [Theory]
    [InlineData(10, 5, AnimeIdMappingSyncOutcome.Synced)] // exactly half is not "fewer than half"
    [InlineData(10, 4, AnimeIdMappingSyncOutcome.Failed)]
    public async Task TheGuardAbandonsOnlyBelowHalfTheStoredCount(int stored, int incoming, AnimeIdMappingSyncOutcome expected)
    {
        var options = CreateOptions();
        var handler = new FakeMappingHandler();
        handler.ServeFile(MappingFile(Enumerable.Range(1, stored)));
        await SyncAsync(options, handler);
        await AgeAsync(options, TimeSpan.FromDays(8));
        handler.ServeFile(MappingFile(Enumerable.Range(1, incoming)));

        var outcome = await SyncAsync(options, handler);

        Assert.Equal(expected, outcome);
        Assert.Equal(expected == AnimeIdMappingSyncOutcome.Synced ? incoming : stored, (await ReadMappingsAsync(options)).Count);
    }

    [Fact]
    public async Task TheFirstSyncIsNeverHeldBackByTheGuard()
    {
        var options = CreateOptions();
        var handler = new FakeMappingHandler();
        handler.ServeFile(MappingFile([1]));

        Assert.Equal(AnimeIdMappingSyncOutcome.Synced, await SyncAsync(options, handler));
        Assert.Equal([1], (await ReadMappingsAsync(options)).Keys);
    }

    // --- Applying a refresh ---

    [Fact]
    public async Task AChangedTvIdIsUpdated()
    {
        var options = CreateOptions();
        var handler = new FakeMappingHandler();
        handler.ServeFile(MappingFile([1, 2, 3]));
        await SyncAsync(options, handler);
        await AgeAsync(options, TimeSpan.FromDays(8));
        handler.ServeFile(MappingFile([1, 2, 3], new Dictionary<int, int> { [2] = 5555 }));

        var outcome = await SyncAsync(options, handler);

        Assert.Equal(AnimeIdMappingSyncOutcome.Synced, outcome);
        var rows = await ReadMappingsAsync(options);
        Assert.Equal(5555, rows[2].TmdbTvId);
        Assert.Equal(1001, rows[1].TmdbTvId);
        Assert.Equal(1003, rows[3].TmdbTvId);
        Assert.Equal(3, rows.Count);
    }

    [Fact]
    public async Task AChangedArrayIsUpdated()
    {
        var options = CreateOptions();
        var handler = new FakeMappingHandler();
        handler.ServeFile("""[{"mal_id":7,"themoviedb_id":{"movie":[10]},"imdb_id":["tt0000007"]}]""");
        await SyncAsync(options, handler);
        await AgeAsync(options, TimeSpan.FromDays(8));
        handler.ServeFile("""[{"mal_id":7,"themoviedb_id":{"movie":[10,11]},"imdb_id":["tt0000007","tt0000008"]}]""");

        await SyncAsync(options, handler);

        var row = (await ReadMappingsAsync(options))[7];
        Assert.Equal([10, 11], row.TmdbMovieIds);
        Assert.Equal(["tt0000007", "tt0000008"], row.ImdbIds);
    }

    [Fact]
    public async Task AVanishedMalIdIsRemoved()
    {
        var options = CreateOptions();
        var handler = new FakeMappingHandler();
        handler.ServeFile(MappingFile([1, 2, 3]));
        await SyncAsync(options, handler);
        await AgeAsync(options, TimeSpan.FromDays(8));
        handler.ServeFile(MappingFile([1, 3]));

        var outcome = await SyncAsync(options, handler);

        Assert.Equal(AnimeIdMappingSyncOutcome.Synced, outcome);
        Assert.Equal([1, 3], (await ReadMappingsAsync(options)).Keys.Order());
    }

    [Fact]
    public async Task AFailedSaveKeepsTheOldMappingAndLastSyncedAtAndStillRecordsTheAttempt()
    {
        // The save fails with the whole diff, and the success stamp, still
        // staged on the context. Recording the failure must not let either
        // ride along: no half-applied mapping, and no LastSyncedAt for a sync
        // that never landed.
        var options = CreateOptions();
        var handler = new FakeMappingHandler();
        handler.ServeFile(MappingFile([1, 2, 3]));
        await SyncAsync(options, handler);
        await AgeAsync(options, TimeSpan.FromDays(8));
        var rowsBefore = await DescribeMappingsAsync(options);
        var stateBefore = await ReadStateAsync(options);
        handler.ServeFile(MappingFile([1, 2, 4]));

        var outcome = await SyncAsync(options, handler,
            createDb: () => new FailingSaveContext(options) { FailNextMappingSave = true });

        Assert.Equal(AnimeIdMappingSyncOutcome.Failed, outcome);
        Assert.Equal(rowsBefore, await DescribeMappingsAsync(options));
        var state = await ReadStateAsync(options);
        Assert.Equal(stateBefore.LastSyncedAt, state.LastSyncedAt);
        AssertJustNow(state.LastAttemptAt);
    }

    // --- Cancellation ---

    [Fact]
    public async Task ACancellationThrowsAndIsNotRecordedAsAFailedAttempt()
    {
        var options = CreateOptions();
        var handler = new FakeMappingHandler();
        using var cts = new CancellationTokenSource();
        handler.Respond = () =>
        {
            cts.Cancel();
            throw new OperationCanceledException(cts.Token);
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => SyncAsync(options, handler, cts.Token));

        var state = await ReadStateAsync(options);
        Assert.Null(state.LastSyncedAt);
        Assert.Null(state.LastAttemptAt);
    }

    // --- What it calls ---

    [Fact]
    public async Task OnlyTheMappingUrlIsEverRequestedAndNoTmdbHost()
    {
        var options = CreateOptions();
        var handler = new FakeMappingHandler();
        handler.ServeFile(MappingFile([1, 2, 3]));
        await SyncAsync(options, handler); // a first sync
        await AgeAsync(options, TimeSpan.FromDays(8));
        await SyncAsync(options, handler); // a due sync
        await AgeAsync(options, TimeSpan.FromDays(8));
        handler.Respond = () => new HttpResponseMessage(HttpStatusCode.InternalServerError);
        await SyncAsync(options, handler); // a failed one

        Assert.Equal(3, handler.Requests.Count);
        Assert.All(handler.Requests, uri => Assert.Equal(AnimeIdMappingSyncService.MappingUrl, uri.ToString()));
        Assert.All(handler.Requests, uri => Assert.Equal("raw.githubusercontent.com", uri.Host));
        Assert.DoesNotContain(handler.Requests, uri => uri.Host.Contains("themoviedb") || uri.Host.Contains("tmdb"));
    }

    // --- The custom mapping (design.md D19) ---

    [Fact]
    public async Task ARefreshLeavesTheCustomEntriesAloneAndWritesNoneIntoTheTable()
    {
        // MAL 999 is a show the source does not list: the situation a hand-inserted
        // row used to lose at the next refresh. Its entry lives in a file, not in
        // the table, so a refresh has nothing to remove.
        var options = CreateOptions();
        var handler = new FakeMappingHandler();
        handler.ServeFile(MappingFile([1, 2, 3]));
        var custom = new FakeCustomIdMappings(FakeCustomIdMappings.Fill(999, tv: 5000, season: 1, imdb: ["tt0009999"]));

        await SyncAsync(options, handler, custom: custom);

        Assert.Equal([1, 2, 3], (await ReadMappingsAsync(options)).Keys.Order()); // no row for 999
        Assert.Equal([999], custom.Current.Keys);                                  // and the entry is still there
        using var db = new AnimeTrackerDbContext(options);
        var mapping = await new AnimeIdMappingResolver(db, custom).FindAsync(999);
        Assert.Equal(5000, mapping!.TmdbTvId);
    }

    [Fact]
    public async Task ADefaultEntryTheRefreshHasFilledInIsReportedAsNoLongerNeeded()
    {
        var options = CreateOptions();
        var handler = new FakeMappingHandler();
        handler.ServeFile(MappingFile([1, 2, 3]));   // the source now maps MAL 2 (TV 1002)
        var logger = new CapturingLogger<AnimeIdMappingSyncService>();
        var custom = new FakeCustomIdMappings(FakeCustomIdMappings.Fill(2, tv: 7777, season: 1, note: "Two, before the source had it"));

        await SyncAsync(options, handler, custom: custom, logger: logger);

        Assert.Contains(logger.Lines, line =>
            line.StartsWith("Information:", StringComparison.Ordinal)
            && line.Contains("MAL id 2 (Two, before the source had it) is no longer needed", StringComparison.Ordinal)
            && line.Contains("custom/id-mapping.json", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AnOverrideTheRefreshedMappingNowAgreesWithIsReported()
    {
        var options = CreateOptions();
        var handler = new FakeMappingHandler();
        handler.ServeFile(MappingFile([1, 2, 3]));   // MAL 2 is TV 1002, season 1
        var logger = new CapturingLogger<AnimeIdMappingSyncService>();
        var custom = new FakeCustomIdMappings(FakeCustomIdMappings.Override(2, tv: 1002, season: 1));

        await SyncAsync(options, handler, custom: custom, logger: logger);

        Assert.Contains(logger.Lines, line => line.Contains("MAL id 2 is no longer needed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AnEntryThatStillDoesSomethingIsNotReported()
    {
        var options = CreateOptions();
        var handler = new FakeMappingHandler();
        handler.ServeFile(MappingFile([1, 2, 3]));
        var logger = new CapturingLogger<AnimeIdMappingSyncService>();
        var custom = new FakeCustomIdMappings(
            FakeCustomIdMappings.Fill(999, tv: 5000),                        // the source still lacks it
            FakeCustomIdMappings.Override(2, tv: 1002, season: 4));          // still corrects a season

        await SyncAsync(options, handler, custom: custom, logger: logger);

        Assert.DoesNotContain(logger.Lines, line => line.Contains("no longer needed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AFailedRefreshReportsNothingAboutTheCustomEntries()
    {
        var options = CreateOptions();
        var handler = new FakeMappingHandler();
        handler.ServeFile("[ this is not json");
        var logger = new CapturingLogger<AnimeIdMappingSyncService>();
        var custom = new FakeCustomIdMappings(FakeCustomIdMappings.Fill(2, tv: 7777));

        var outcome = await SyncAsync(options, handler, custom: custom, logger: logger);

        Assert.Equal(AnimeIdMappingSyncOutcome.Failed, outcome);
        Assert.DoesNotContain(logger.Lines, line => line.Contains("no longer needed", StringComparison.Ordinal));
    }

    // --- Fakes ---

    private sealed class FakeMappingHandler : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        // A delegate rather than a response: each request needs a fresh
        // message, since a response body can be read only once.
        public Func<HttpResponseMessage> Respond { get; set; } = () => new HttpResponseMessage(HttpStatusCode.InternalServerError);

        public void ServeFile(string json) =>
            Respond = () => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            return Task.FromResult(Respond());
        }
    }

    private sealed class FakeHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            // The name Program.cs registers the 2-minute-timeout client under.
            Assert.Equal(AnimeIdMappingSyncService.HttpClientName, name);
            return new HttpClient(handler, disposeHandler: false);
        }
    }

    // Fails the first save that carries a mapping change, the way a database
    // error mid-write would.
    private sealed class FailingSaveContext(DbContextOptions<AnimeTrackerDbContext> options) : AnimeTrackerDbContext(options)
    {
        public bool FailNextMappingSave { get; set; }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            if (FailNextMappingSave
                && ChangeTracker.Entries<AnimeIdMapping>().Any(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                FailNextMappingSave = false;
                throw new DbUpdateException("simulated write failure");
            }

            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
    }
}
