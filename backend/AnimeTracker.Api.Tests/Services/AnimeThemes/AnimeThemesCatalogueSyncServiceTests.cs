using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.AnimeThemes;
using AnimeTracker.Api.Tests.Services.IdMapping;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimeTracker.Api.Tests.Services.AnimeThemes;

// anime-themes spec: the weekly catalogue refresh (AnimeThemesCatalogueSyncService)
// over EF InMemory and a fake client. Time passing is simulated by rewinding the
// stored stamps, as the rest of this suite does, and the two-second gap between
// pages is recorded rather than sat through.
public class AnimeThemesCatalogueSyncServiceTests
{
    private static DbContextOptions<AnimeTrackerDbContext> CreateOptions(SaveRecorder? recorder = null)
    {
        var builder = new DbContextOptionsBuilder<AnimeTrackerDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString());
        if (recorder is not null)
            builder.AddInterceptors(recorder);
        return builder.Options;
    }

    // One opening and one ending per anime, every id derived from the anime's own,
    // so a test can change one by naming it.
    private static AnimeThemesAnime Show(int id, string songTitle = "Song") =>
        Parsed.Anime(id, [id + 1000],
            Parsed.Theme(id * 10, "OP", "OP1", 1, Parsed.Song(id * 100, songTitle, Parsed.Credit("Artist"))),
            Parsed.Theme(id * 10 + 1, "ED", "ED1", 1, Parsed.Song(id * 100 + 1, songTitle, Parsed.Credit("Artist"))));

    private static List<AnimeThemesPage> Paged(IEnumerable<AnimeThemesAnime> anime, int pageSize)
    {
        var chunks = anime.Chunk(pageSize).ToList();
        return chunks.Select((chunk, i) => new AnimeThemesPage(i < chunks.Count - 1, chunk)).ToList();
    }

    private static FakeClient Serving(IReadOnlyList<AnimeThemesPage> pages, List<string>? timeline = null) =>
        new(timeline ?? []) { Serve = (page, _) => pages[page - 1] };

    private sealed record Result(AnimeThemesCatalogueSyncOutcome Outcome, List<TimeSpan> Delays);

    private static async Task<Result> SyncAsync(
        DbContextOptions<AnimeTrackerDbContext> options,
        IAnimeThemesClient client,
        List<string>? timeline = null,
        CancellationToken ct = default,
        ILogger<AnimeThemesCatalogueSyncService>? logger = null)
    {
        // A new context per call, as each hourly tick gets a scope of its own.
        using var db = new AnimeTrackerDbContext(options);
        var sync = new TestSync(db, client, timeline ?? [], logger ?? NullLogger<AnimeThemesCatalogueSyncService>.Instance);
        var outcome = await sync.SyncIfDueAsync(ct);
        return new Result(outcome, sync.Delays);
    }

    private static Task<Result> SyncAsync(DbContextOptions<AnimeTrackerDbContext> options, IReadOnlyList<AnimeThemesPage> pages) =>
        SyncAsync(options, Serving(pages));

    private static async Task<AnimeThemesSyncState> ReadStateAsync(DbContextOptions<AnimeTrackerDbContext> options)
    {
        using var db = new AnimeTrackerDbContext(options);
        return await db.AnimeThemesSyncStates.AsNoTracking().SingleAsync();
    }

    // "The last successful refresh, and the last attempt, were both this long ago."
    private static Task AgeAsync(DbContextOptions<AnimeTrackerDbContext> options, TimeSpan age) =>
        SetStampsAsync(options, lastSyncedAgo: age, lastAttemptAgo: age);

    private static async Task SetStampsAsync(
        DbContextOptions<AnimeTrackerDbContext> options, TimeSpan? lastSyncedAgo, TimeSpan? lastAttemptAgo)
    {
        using var db = new AnimeTrackerDbContext(options);
        var state = await db.AnimeThemesSyncStates.SingleAsync();
        state.LastSyncedAt = lastSyncedAgo is { } synced ? DateTimeOffset.UtcNow - synced : null;
        state.LastAttemptAt = lastAttemptAgo is { } attempt ? DateTimeOffset.UtcNow - attempt : null;
        await db.SaveChangesAsync();
    }

    private static async Task<string> DescribeAsync(DbContextOptions<AnimeTrackerDbContext> options)
    {
        using var db = new AnimeTrackerDbContext(options);
        var rows = await db.AnimeThemes.AsNoTracking().ToListAsync();
        return string.Join("|", rows.OrderBy(r => r.AnimeId).ThenBy(r => r.ThemeId).Select(r =>
            $"{r.AnimeId}/{r.ThemeId}:{r.Kind}:{r.Sequence}:{r.Label}:{r.SongId}:{r.SongTitle}:{string.Join(",", r.Artists)}:{r.Episodes}:{r.Nsfw}:{r.Spoiler}:{r.Clean}:{r.VideoUrl}:{r.AudioUrl}"));
    }

    private static async Task<int> CountRowsAsync(DbContextOptions<AnimeTrackerDbContext> options)
    {
        using var db = new AnimeTrackerDbContext(options);
        return await db.AnimeThemes.CountAsync();
    }

    // A first refresh of this many anime, then the clocks wound back so the next is due.
    private static async Task SeedAsync(DbContextOptions<AnimeTrackerDbContext> options, int animeCount)
    {
        var first = await SyncAsync(options, Paged(Enumerable.Range(1, animeCount).Select(id => Show(id)), 100));
        Assert.Equal(AnimeThemesCatalogueSyncOutcome.Synced, first.Outcome);
        await AgeAsync(options, TimeSpan.FromDays(8));
    }

    private static void AssertJustNow(DateTimeOffset? stamp) =>
        Assert.InRange(stamp!.Value, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddSeconds(1));

    // --- Cadence ---

    [Fact]
    public async Task TheFirstRefreshStoresTheRowsAndStampsTheState()
    {
        var options = CreateOptions();
        var client = Serving(Paged([Show(1), Show(2), Show(3)], 100));

        var result = await SyncAsync(options, client);

        Assert.Equal(AnimeThemesCatalogueSyncOutcome.Synced, result.Outcome);
        Assert.Equal(6, await CountRowsAsync(options));
        var state = await ReadStateAsync(options);
        AssertJustNow(state.LastSyncedAt);
        Assert.Equal(state.LastSyncedAt, state.LastAttemptAt);
        Assert.Equal([1], client.Requested);
    }

    [Fact]
    public async Task ThreeDaysLaterThereIsNoRequest()
    {
        var options = CreateOptions();
        await SyncAsync(options, Paged([Show(1)], 100));
        await AgeAsync(options, TimeSpan.FromDays(3));
        var client = Serving(Paged([Show(1)], 100));

        var result = await SyncAsync(options, client);

        Assert.Equal(AnimeThemesCatalogueSyncOutcome.NotDue, result.Outcome);
        Assert.Empty(client.Requested);
    }

    [Fact]
    public async Task ARestartTheDayAfterDoesNotRepeatIt()
    {
        var options = CreateOptions();
        await SyncAsync(options, Paged([Show(1)], 100));
        await AgeAsync(options, TimeSpan.FromDays(1));
        var client = Serving(Paged([Show(1)], 100));

        // A new context and a new service, as after a restart: only the stored stamps carry over.
        var result = await SyncAsync(options, client);

        Assert.Equal(AnimeThemesCatalogueSyncOutcome.NotDue, result.Outcome);
        Assert.Empty(client.Requested);
    }

    [Fact]
    public async Task EightDaysLaterItRefreshes()
    {
        var options = CreateOptions();
        await SyncAsync(options, Paged([Show(1)], 100));
        await AgeAsync(options, TimeSpan.FromDays(8));
        var client = Serving(Paged([Show(1), Show(2)], 100));

        var result = await SyncAsync(options, client);

        Assert.Equal(AnimeThemesCatalogueSyncOutcome.Synced, result.Outcome);
        Assert.Equal([1], client.Requested);
        Assert.Equal(4, await CountRowsAsync(options));
        AssertJustNow((await ReadStateAsync(options)).LastSyncedAt);
    }

    // --- Failure and backoff ---

    [Fact]
    public async Task AFailedFirstRefreshRecordsTheAttemptAndStoresNothing()
    {
        var options = CreateOptions();
        var client = new FakeClient([]) { Serve = (_, _) => throw new AnimeThemesException("down") };

        var result = await SyncAsync(options, client);

        Assert.Equal(AnimeThemesCatalogueSyncOutcome.Failed, result.Outcome);
        Assert.Equal(0, await CountRowsAsync(options));
        var state = await ReadStateAsync(options);
        Assert.Null(state.LastSyncedAt);
        AssertJustNow(state.LastAttemptAt);
    }

    [Fact]
    public async Task AFailureTwoHoursAgoBacksOff_AndSixHoursLaterItTriesOnce()
    {
        var options = CreateOptions();
        await SyncAsync(options, new FakeClient([]) { Serve = (_, _) => throw new AnimeThemesException("down") });

        await SetStampsAsync(options, lastSyncedAgo: null, lastAttemptAgo: TimeSpan.FromHours(2));
        var backedOff = Serving(Paged([Show(1)], 100));
        var early = await SyncAsync(options, backedOff);

        Assert.Equal(AnimeThemesCatalogueSyncOutcome.NotDue, early.Outcome);
        Assert.Empty(backedOff.Requested);

        await SetStampsAsync(options, lastSyncedAgo: null, lastAttemptAgo: TimeSpan.FromHours(6) + TimeSpan.FromMinutes(1));
        var retry = Serving(Paged([Show(1)], 100));
        var late = await SyncAsync(options, retry);

        Assert.Equal(AnimeThemesCatalogueSyncOutcome.Synced, late.Outcome);
        Assert.Equal([1], retry.Requested);
    }

    [Fact]
    public async Task AFailureAfterAWeekOldSuccessBacksOffToo()
    {
        var options = CreateOptions();
        await SeedAsync(options, 4);
        await SetStampsAsync(options, lastSyncedAgo: TimeSpan.FromDays(8), lastAttemptAgo: TimeSpan.FromHours(2));
        var client = Serving(Paged([Show(1)], 100));

        var result = await SyncAsync(options, client);

        Assert.Equal(AnimeThemesCatalogueSyncOutcome.NotDue, result.Outcome);
        Assert.Empty(client.Requested);
    }

    [Fact]
    public async Task AFailureOnPage30LeavesTheStoredRowsUntouchedAndRecordsTheAttempt()
    {
        var options = CreateOptions();
        var pages = Paged(Enumerable.Range(1, 102).Select(id => Show(id)), 2);
        Assert.Equal(51, pages.Count);
        Assert.Equal(AnimeThemesCatalogueSyncOutcome.Synced, (await SyncAsync(options, pages)).Outcome);
        await AgeAsync(options, TimeSpan.FromDays(8));
        var before = await DescribeAsync(options);
        var beforeState = await ReadStateAsync(options);

        // The read that follows would have changed every row, if it had finished.
        var changed = Paged(Enumerable.Range(1, 102).Select(id => Show(id, "Another song")), 2);
        var client = new FakeClient([])
        {
            Serve = (page, _) => page == 30 ? throw new AnimeThemesException("page 30 failed") : changed[page - 1],
        };
        var result = await SyncAsync(options, client);

        Assert.Equal(AnimeThemesCatalogueSyncOutcome.Failed, result.Outcome);
        Assert.Equal(Enumerable.Range(1, 30), client.Requested);
        Assert.Equal(before, await DescribeAsync(options));
        var state = await ReadStateAsync(options);
        Assert.Equal(beforeState.LastSyncedAt, state.LastSyncedAt);
        AssertJustNow(state.LastAttemptAt);
    }

    [Fact]
    public async Task ABodyWithErrorsIsAFailureThatKeepsTheStoredCatalogue()
    {
        var options = CreateOptions();
        await SeedAsync(options, 4);
        var before = await DescribeAsync(options);

        // The real client, over an answer that reports an error about a field it asked for.
        var handler = new FakeAnimeThemesHandler();
        handler.Enqueue(() => FakeAnimeThemesHandler.Json("""{"errors":[{"message":"Cannot query field \"nc\" on type \"Video\"."}],"data":null}"""));
        var result = await SyncAsync(options, new InstantClient(handler));

        Assert.Equal(AnimeThemesCatalogueSyncOutcome.Failed, result.Outcome);
        Assert.Equal(before, await DescribeAsync(options));
        AssertJustNow((await ReadStateAsync(options)).LastAttemptAt);
    }

    [Fact]
    public async Task ARateLimitTheClientGivesUpOnIsAFailureThatKeepsTheStoredCatalogue()
    {
        var options = CreateOptions();
        await SeedAsync(options, 4);
        var before = await DescribeAsync(options);

        var handler = new FakeAnimeThemesHandler();
        handler.Enqueue(() => FakeAnimeThemesHandler.RateLimited(TimeSpan.FromMinutes(10)));
        var result = await SyncAsync(options, new InstantClient(handler));

        Assert.Equal(AnimeThemesCatalogueSyncOutcome.Failed, result.Outcome);
        Assert.Equal(before, await DescribeAsync(options));
    }

    [Fact]
    public async Task ATimeoutIsAFailureNotACancellation()
    {
        var options = CreateOptions();
        var client = new FakeClient([])
        {
            // What HttpClient.Timeout throws while the caller's own token is still live.
            Serve = (_, _) => throw new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout.", new TimeoutException()),
        };

        var result = await SyncAsync(options, client);

        Assert.Equal(AnimeThemesCatalogueSyncOutcome.Failed, result.Outcome);
        AssertJustNow((await ReadStateAsync(options)).LastAttemptAt);
    }

    [Fact]
    public async Task ATransportFailureIsAFailure()
    {
        var options = CreateOptions();
        var client = new FakeClient([]) { Serve = (_, _) => throw new HttpRequestException("connection refused") };

        var result = await SyncAsync(options, client);

        Assert.Equal(AnimeThemesCatalogueSyncOutcome.Failed, result.Outcome);
    }

    [Fact]
    public async Task AFailedSaveKeepsTheStoredCatalogueAndRecordsTheAttempt()
    {
        var recorder = new SaveRecorder();
        var options = CreateOptions(recorder);
        await SeedAsync(options, 4);
        var before = await DescribeAsync(options);
        recorder.FailNextThemeSave = true;

        var result = await SyncAsync(options, Serving(Paged([Show(1, "Changed"), Show(2), Show(3), Show(4)], 100)));

        Assert.Equal(AnimeThemesCatalogueSyncOutcome.Failed, result.Outcome);
        Assert.Equal(before, await DescribeAsync(options));
        AssertJustNow((await ReadStateAsync(options)).LastAttemptAt);
    }

    // --- The sanity guard ---

    [Fact]
    public async Task UnderHalfTheStoredAnimeIsAbandoned()
    {
        var options = CreateOptions();
        await SeedAsync(options, 20);
        var before = await DescribeAsync(options);
        var beforeState = await ReadStateAsync(options);

        var result = await SyncAsync(options, Paged(Enumerable.Range(1, 9).Select(id => Show(id)), 100));

        Assert.Equal(AnimeThemesCatalogueSyncOutcome.Failed, result.Outcome);
        Assert.Equal(before, await DescribeAsync(options));
        var state = await ReadStateAsync(options);
        Assert.Equal(beforeState.LastSyncedAt, state.LastSyncedAt);
        AssertJustNow(state.LastAttemptAt);
    }

    [Fact]
    public async Task ExactlyHalfTheStoredAnimeIsTaken()
    {
        var options = CreateOptions();
        await SeedAsync(options, 20);

        var result = await SyncAsync(options, Paged(Enumerable.Range(1, 10).Select(id => Show(id)), 100));

        Assert.Equal(AnimeThemesCatalogueSyncOutcome.Synced, result.Outcome);
        Assert.Equal(20, await CountRowsAsync(options));
    }

    [Fact]
    public async Task TheFirstRefreshIsNotHeldBackByTheGuard()
    {
        var options = CreateOptions();

        var result = await SyncAsync(options, Paged([Show(1)], 100));

        Assert.Equal(AnimeThemesCatalogueSyncOutcome.Synced, result.Outcome);
        Assert.Equal(2, await CountRowsAsync(options));
    }

    [Fact]
    public async Task AnEmptyReadIsNeverTaken()
    {
        var options = CreateOptions();

        var first = await SyncAsync(options, new List<AnimeThemesPage> { new(false, []) });

        Assert.Equal(AnimeThemesCatalogueSyncOutcome.Failed, first.Outcome);
        var state = await ReadStateAsync(options);
        Assert.Null(state.LastSyncedAt);
        AssertJustNow(state.LastAttemptAt);
    }

    // --- The diff ---

    [Fact]
    public async Task ARowAddedChangedAndRemovedIsWrittenAsThat()
    {
        var recorder = new SaveRecorder();
        var options = CreateOptions(recorder);
        await SeedAsync(options, 3);
        recorder.Reset();

        // Anime 1 unchanged, anime 2 with a new song title, anime 3 gone, anime 4 new.
        var result = await SyncAsync(options, Serving(Paged([Show(1), Show(2, "Changed"), Show(4)], 100)));

        Assert.Equal(AnimeThemesCatalogueSyncOutcome.Synced, result.Outcome);
        Assert.Equal(2, recorder.Saved.Count(s => s is ("AnimeTheme", EntityState.Added)));
        Assert.Equal(2, recorder.Saved.Count(s => s is ("AnimeTheme", EntityState.Modified)));
        Assert.Equal(2, recorder.Saved.Count(s => s is ("AnimeTheme", EntityState.Deleted)));

        using var db = new AnimeTrackerDbContext(options);
        var rows = await db.AnimeThemes.AsNoTracking().ToListAsync();
        // Rows are keyed on the MAL id, which Show makes the anime's id plus 1000.
        Assert.Equal([1001, 1002, 1004], rows.Select(r => r.AnimeId).Distinct().Order());
        Assert.All(rows.Where(r => r.AnimeId == 1002), r => Assert.Equal("Changed", r.SongTitle));
        Assert.All(rows.Where(r => r.AnimeId == 1001), r => Assert.Equal("Song", r.SongTitle));
    }

    [Fact]
    public async Task AChangedLinkOrFlagOrArtistListIsWritten()
    {
        var options = CreateOptions();
        await SyncAsync(options, Paged([Parsed.Anime(1, [100], Parsed.Theme(10, song: Parsed.Song(1, "S", Parsed.Credit("A"))))], 100));
        await AgeAsync(options, TimeSpan.FromDays(8));

        var changed = Parsed.Theme(10, song: Parsed.Song(1, "S", Parsed.Credit("A"), Parsed.Credit("B")), versions:
            [Parsed.Version(1, [Parsed.Video("Other", clean: true)], episodes: "1-3", nsfw: true)]);
        await SyncAsync(options, Paged([Parsed.Anime(1, [100], changed)], 100));

        using var db = new AnimeTrackerDbContext(options);
        var row = await db.AnimeThemes.AsNoTracking().SingleAsync();
        Assert.Equal(["A", "B"], row.Artists);
        Assert.Equal("1-3", row.Episodes);
        Assert.True(row.Nsfw);
        Assert.True(row.Clean);
        Assert.Equal("https://v.animethemes.moe/Other.webm", row.VideoUrl);
        Assert.Equal("https://a.animethemes.moe/Other.ogg", row.AudioUrl);
    }

    [Fact]
    public async Task AnUnchangedCatalogueWritesNoRowButTheStamp()
    {
        var recorder = new SaveRecorder();
        var options = CreateOptions(recorder);
        await SeedAsync(options, 5);
        var before = await DescribeAsync(options);
        recorder.Reset();

        var result = await SyncAsync(options, Serving(Paged(Enumerable.Range(1, 5).Select(id => Show(id)), 100)));

        Assert.Equal(AnimeThemesCatalogueSyncOutcome.Synced, result.Outcome);
        Assert.Equal([("AnimeThemesSyncState", EntityState.Modified)], recorder.Saved);
        Assert.Equal(before, await DescribeAsync(options));
        AssertJustNow((await ReadStateAsync(options)).LastSyncedAt);
    }

    [Fact]
    public async Task TheRefreshIsOneSaveThatCarriesTheStamp()
    {
        var recorder = new SaveRecorder();
        var options = CreateOptions(recorder);
        await SeedAsync(options, 3);
        recorder.Reset();

        await SyncAsync(options, Serving(Paged([Show(1), Show(2, "Changed"), Show(4)], 100)));

        Assert.Equal(1, recorder.SaveCount);
        Assert.Contains(("AnimeThemesSyncState", EntityState.Modified), recorder.Saved);
    }

    [Fact]
    public async Task RowsAreHeldForAnimeTheAppHasNotStored()
    {
        // No AnimeMetadata row exists in this database, and the rows are held anyway.
        var options = CreateOptions();

        await SyncAsync(options, Paged([Show(1)], 100));

        using var db = new AnimeTrackerDbContext(options);
        Assert.Empty(db.AnimeMetadata);
        Assert.Equal(2, await db.AnimeThemes.CountAsync());
    }

    // --- Pace ---

    [Fact]
    public async Task RequestsNeverOverlapAndAtLeastTwoSecondsSeparateThem()
    {
        var options = CreateOptions();
        var timeline = new List<string>();
        var client = Serving(Paged(Enumerable.Range(1, 102).Select(id => Show(id)), 2), timeline);

        var result = await SyncAsync(options, client, timeline);

        Assert.Equal(AnimeThemesCatalogueSyncOutcome.Synced, result.Outcome);
        Assert.Equal(51, client.Requested.Count);
        Assert.Equal(1, client.MaxConcurrent);

        // One wait between each pair of requests, none before the first or after the last.
        Assert.Equal(50, result.Delays.Count);
        Assert.All(result.Delays, wait => Assert.True(wait >= TimeSpan.FromSeconds(2)));
        Assert.Equal(["start 1", "end 1", "delay", "start 2", "end 2", "delay", "start 3"], timeline.Take(7));
        Assert.Equal(["end 50", "delay", "start 51", "end 51"], timeline.TakeLast(4));
    }

    [Fact]
    public void TheGapKeepsItUnderThirtyRequestsAMinute()
    {
        // Two seconds between the end of one request and the start of the next:
        // thirty a minute even if every request were instant.
        Assert.True(AnimeThemesCatalogueSyncService.PageGap >= TimeSpan.FromSeconds(2));
        Assert.True(TimeSpan.FromMinutes(1) / AnimeThemesCatalogueSyncService.PageGap <= 30);
    }

    [Fact]
    public async Task APageCountThatNeverEndsStopsAtTwoHundred()
    {
        var options = CreateOptions();
        var logger = new CapturingLogger<AnimeThemesCatalogueSyncService>();
        var client = new FakeClient([]) { Serve = (_, _) => new AnimeThemesPage(true, [Show(1), Show(2)]) };

        var result = await SyncAsync(options, client, logger: logger);

        Assert.Equal(AnimeThemesCatalogueSyncService.MaxPages, 200);
        Assert.Equal(Enumerable.Range(1, 200), client.Requested);
        Assert.Equal(199, result.Delays.Count);
        // What was read is used: the same two anime on every page are two anime.
        Assert.Equal(AnimeThemesCatalogueSyncOutcome.Synced, result.Outcome);
        Assert.Equal(4, await CountRowsAsync(options));
        Assert.Contains(logger.Lines, line => line.Contains("200", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ThePagesAreReadInOrderUntilOneSaysItIsTheLast()
    {
        var options = CreateOptions();
        var client = Serving(Paged(Enumerable.Range(1, 7).Select(id => Show(id)), 3));

        await SyncAsync(options, client);

        Assert.Equal([1, 2, 3], client.Requested);
        Assert.Equal(14, await CountRowsAsync(options));
    }

    [Fact]
    public async Task AnimeOnTwoPagesAreMergedIntoOneCatalogue()
    {
        var options = CreateOptions();

        await SyncAsync(options, Paged([Show(1), Show(2), Show(3), Show(4)], 2));

        Assert.Equal(8, await CountRowsAsync(options));
    }

    [Fact]
    public async Task NothingIsStoredUntilTheWholeCatalogueHasBeenRead()
    {
        var options = CreateOptions();
        var rowsWhenAskedForPage2 = -1;
        var client = new FakeClient([])
        {
            Serve = (page, _) =>
            {
                if (page == 2)
                {
                    using var db = new AnimeTrackerDbContext(options);
                    rowsWhenAskedForPage2 = db.AnimeThemes.Count();
                    return new AnimeThemesPage(false, [Show(2)]);
                }

                return new AnimeThemesPage(true, [Show(1)]);
            },
        };

        await SyncAsync(options, client);

        Assert.Equal(0, rowsWhenAskedForPage2);
        Assert.Equal(4, await CountRowsAsync(options));
    }

    // --- Cancellation ---

    [Fact]
    public async Task CancellationPassesThroughAndIsNotARecordedFailure()
    {
        var options = CreateOptions();
        using var cts = new CancellationTokenSource();
        var client = new FakeClient([])
        {
            Serve = (page, ct) =>
            {
                if (page == 2)
                {
                    cts.Cancel();
                    ct.ThrowIfCancellationRequested();
                }

                return new AnimeThemesPage(true, [Show(page)]);
            },
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => SyncAsync(options, client, ct: cts.Token));

        Assert.Equal(0, await CountRowsAsync(options));
        Assert.Null((await ReadStateAsync(options)).LastAttemptAt);
    }

    // --- Fakes ---

    // Records every wait instead of sitting through it, and where it fell among the requests.
    private sealed class TestSync(
        AnimeTrackerDbContext db, IAnimeThemesClient client, List<string> timeline, ILogger<AnimeThemesCatalogueSyncService> logger)
        : AnimeThemesCatalogueSyncService(db, client, logger)
    {
        public List<TimeSpan> Delays { get; } = [];

        protected override Task DelayAsync(TimeSpan wait, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Delays.Add(wait);
            timeline.Add("delay");
            return Task.CompletedTask;
        }
    }

    // Answers each page from a function, yielding in the middle of a request so a
    // second one running beside it would be seen.
    private sealed class FakeClient(List<string> timeline) : IAnimeThemesClient
    {
        private int _active;

        public Func<int, CancellationToken, AnimeThemesPage> Serve { get; set; } =
            (_, _) => new AnimeThemesPage(false, []);

        public List<int> Requested { get; } = [];
        public int MaxConcurrent { get; private set; }

        public async Task<AnimeThemesPage> GetPageAsync(int page, CancellationToken ct = default)
        {
            timeline.Add($"start {page}");
            var active = Interlocked.Increment(ref _active);
            MaxConcurrent = Math.Max(MaxConcurrent, active);
            try
            {
                Requested.Add(page);
                await Task.Yield();
                ct.ThrowIfCancellationRequested();
                return Serve(page, ct);
            }
            finally
            {
                Interlocked.Decrement(ref _active);
                timeline.Add($"end {page}");
            }
        }
    }

    // Notes which entities each save carries, and can fail the one that writes a theme,
    // the way a database error mid-write would. An interceptor rather than a derived
    // context: a derived context type has a model of its own, which an in-memory store
    // created by the plain one can't be shared with.
    private sealed class SaveRecorder : SaveChangesInterceptor
    {
        public List<(string Type, EntityState State)> Saved { get; } = [];
        public int SaveCount { get; private set; }
        public bool FailNextThemeSave { get; set; }

        public void Reset()
        {
            Saved.Clear();
            SaveCount = 0;
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var changes = eventData.Context!.ChangeTracker.Entries()
                .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                .Select(e => (e.Entity.GetType().Name, e.State))
                .ToList();

            if (FailNextThemeSave && changes.Any(c => c.Item1 == nameof(AnimeTheme)))
            {
                FailNextThemeSave = false;
                throw new DbUpdateException("simulated write failure");
            }

            if (changes.Count > 0)
                SaveCount++;
            Saved.AddRange(changes);
            return ValueTask.FromResult(result);
        }
    }
}
