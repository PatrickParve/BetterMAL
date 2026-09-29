using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Artwork;
using AnimeTracker.Api.Services.Infrastructure;
using AnimeTracker.Api.Services.Metadata;
using AnimeTracker.Api.Services.Profile;
using AnimeTracker.Api.Services.Search;
using AnimeTracker.Api.Services.Series;
using AnimeTracker.Api.Services.IdMapping;
using AnimeTracker.Api.Services.Tmdb;
using AnimeTracker.Api.Services.Transfer;
using AnimeTracker.Api.Tests.Services.Search;
using AnimeTracker.Api.Tests.Services.IdMapping;
using AnimeTracker.Api.Tests.Services.Tmdb;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using static AnimeTracker.Api.Tests.Services.Tmdb.FakeTmdbClient;
using static AnimeTracker.Api.Tests.Services.Tmdb.TmdbTestData;

namespace AnimeTracker.Api.Tests.Services.Transfer;

// TransferImportRunner (device-transfer design.md D4-D13, tasks.md 8.1-8.6):
// prepare (fetch unseen anime, resolve series, pre-check pictures) then apply
// (one transaction, the merge rules). Runs on the in-memory provider with
// TransactionIgnoredWarning suppressed, with fakes for the metadata, series
// and picture-refresh services — same pattern as ExportServiceTests.
public class TransferImportRunnerTests
{
    private static AnimeTrackerDbContext CreateDb(string dbName) =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    // Apply opens its own DI scope against the same in-memory database
    // (design.md D11), so its DbContext, IArtworkSelectionService and
    // ITopAnimeSelectionRepository are all resolved fresh rather than reused
    // from prepare's.
    private static IServiceScopeFactory CreateScopeFactory(string dbName)
    {
        var services = new ServiceCollection();
        services.AddDbContext<AnimeTrackerDbContext>(o => o
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        services.AddScoped<IArtworkSelectionService, ArtworkSelectionService>();
        services.AddScoped<ITopAnimeSelectionRepository, TopAnimeSelectionRepository>();
        services.AddSingleton<IAnimeSearchIndex>(new FakeAnimeSearchIndex());
        // What ArtworkSelectionService needs to check a TMDB choice: the real
        // TMDB artwork service over the same database, with a fake client.
        services.AddSingleton<ITmdbClient>(new FakeTmdbClient());
        services.AddSingleton<RefreshGate>();
        services.AddSingleton(Options.Create(new TmdbOptions()));
        services.AddSingleton<ICustomIdMappings>(new FakeCustomIdMappings());
        services.AddScoped<IAnimeIdMappingResolver, AnimeIdMappingResolver>();
        services.AddScoped<ITmdbArtworkService, TmdbArtworkService>();
        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    // One TMDB service serves both the runner's own picture checks and its
    // refreshes, as the scope's single instance does in production. Left
    // unset it has no key, so it never fetches, whatever the file asks for.
    private static TransferImportRunner CreateRunner(
        AnimeTrackerDbContext db, string dbName,
        FakeMetadataRefreshService metadata, FakeSeriesService series, FakePictureRefreshService pictures,
        ITmdbArtworkService? tmdb = null)
    {
        tmdb ??= new TmdbArtworkService(db, new FakeTmdbClient(), new RefreshGate(), Options.Create(new TmdbOptions()), TestIdMappings.Resolver(db));
        return new(db, CreateScopeFactory(dbName), metadata, series,
            new ArtworkSelectionService(db, new FakeAnimeSearchIndex(), tmdb),
            pictures, tmdb, new TopAnimeSelectionRepository(db), new RefreshGate());
    }

    private static TransferImportProgressTracker NewProgress() => new();

    private static TransferFile File(
        TransferRanking? ranking = null,
        List<TransferAnimePicture>? animePictures = null,
        List<TransferSeries>? series = null,
        List<TransferActivity>? activity = null) =>
        new(
            FormatVersion: 1,
            Device: new TransferDevice(Guid.NewGuid(), "Other Device"),
            ExportedAt: DateTimeOffset.UtcNow,
            Ranking: ranking ?? new TransferRanking([], null),
            AnimePictures: animePictures ?? [],
            Series: series ?? [],
            Activity: activity ?? []);

    // --- Fakes (tasks.md 8's own instruction: fakes for metadata, series, picture-refresh) ---

    private sealed class FakeMetadataRefreshService(AnimeTrackerDbContext db) : IMetadataRefreshService
    {
        public HashSet<int> NotFoundIds { get; } = [];
        public HashSet<int> FailingIds { get; } = [];
        public List<int> Calls { get; } = [];

        public Task RefreshStaleBatchAsync(int batchSize, int? skipAnimeId, MalCallTally tally, CancellationToken ct = default) => throw new NotImplementedException();

        public async Task RefreshOneAsync(int animeId, CancellationToken ct = default)
        {
            Calls.Add(animeId);
            if (NotFoundIds.Contains(animeId)) throw new AnimeMetadataNotFoundException(animeId);
            if (FailingIds.Contains(animeId)) throw new HttpRequestException("MyAnimeList unavailable");

            if (!await db.AnimeMetadata.AnyAsync(a => a.Id == animeId, ct))
            {
                db.AnimeMetadata.Add(new AnimeMetadata { Id = animeId, Title = $"Anime {animeId}" });
                await db.SaveChangesAsync(ct);
            }
        }
    }

    private sealed class FakeSeriesService(AnimeTrackerDbContext db) : ISeriesService
    {
        public Task<SetupBuildOutcome> BuildForSetupAsync(int animeId, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public HashSet<int> Unbuildable { get; } = []; // GetSeriesAsync throws SeriesNotFoundException
        public HashSet<int> BuildFails { get; } = []; // GetSeriesAsync throws a different exception
        public Dictionary<int, Action> BuildActions { get; } = []; // what a successful build stores
        public List<int> BuildCalls { get; } = [];

        public Task<int?> FindSeriesIdAsync(int animeId, CancellationToken ct = default) =>
            db.SeriesMembers.AsNoTracking()
                .Where(m => m.AnimeId == animeId && m.IsPrimary)
                .Select(m => (int?)m.SeriesId)
                .FirstOrDefaultAsync(ct);

        public async Task<SeriesDto> GetSeriesAsync(int animeId, CancellationToken ct = default)
        {
            BuildCalls.Add(animeId);
            if (Unbuildable.Contains(animeId)) throw new SeriesNotFoundException(animeId);
            if (BuildFails.Contains(animeId)) throw new InvalidOperationException("The series could not be built.");
            if (BuildActions.TryGetValue(animeId, out var build))
            {
                build();
                await db.SaveChangesAsync(ct);
            }
            return null!; // never read by the runner
        }

        public Task<SeriesDto> RebuildSeriesAsync(int animeId, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class FakePictureRefreshService(AnimeTrackerDbContext db) : IPictureRefreshService
    {
        public List<(int AnimeId, bool EvenIfFetched)> Calls { get; } = [];

        // Simulates MyAnimeList having (or not having) added the picture by
        // the time of the refresh: the option set a refresh installs for a
        // given anime.
        public Dictionary<int, List<string>> PicturesToInstallOnRefresh { get; } = [];

        public async Task<bool> RefreshOneAsync(int animeId, bool evenIfFetched = false, CancellationToken ct = default)
        {
            Calls.Add((animeId, evenIfFetched));
            var anime = await db.AnimeMetadata.FirstOrDefaultAsync(a => a.Id == animeId, ct);
            if (anime is null) return false;

            if (PicturesToInstallOnRefresh.TryGetValue(animeId, out var urls))
                anime.PictureUrls = urls;
            anime.PicturesSyncedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return true;
        }

        public Task<int> RefreshSeriesMainLineAsync(int seriesId, int budget, CancellationToken ct = default) => throw new NotImplementedException();
    }

    // --- 8.1: Log ---

    [Fact]
    public async Task NewRecordsAreStoredAndPresentOnesSkipped()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDb(dbName);
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Anime 1" });
        var existing = new ActivityLog { EventId = Guid.NewGuid(), AnimeId = 1, Timestamp = DateTimeOffset.UtcNow.AddDays(-5), ChangeType = ActivityChangeType.Added };
        db.ActivityLogs.Add(existing);
        await db.SaveChangesAsync();
        var newId = Guid.NewGuid();
        var file = File(activity:
        [
            new TransferActivity(existing.EventId, existing.Timestamp, 1, "Added", null, null),
            new TransferActivity(newId, DateTimeOffset.UtcNow, 1, "EpisodeIncremented", null, 3),
        ]);

        await CreateRunner(db, dbName, new(db), new(db), new(db)).RunAsync(file, NewProgress(), CancellationToken.None);

        var logs = await db.ActivityLogs.AsNoTracking().ToListAsync();
        Assert.Equal(2, logs.Count);
        Assert.Contains(logs, l => l.EventId == newId && l.ChangeType == ActivityChangeType.EpisodeIncremented && l.PreviousEpisodesWatched == 3);
    }

    [Fact]
    public async Task ADuplicateWithinTheFileIsSkipped()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDb(dbName);
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Anime 1" });
        await db.SaveChangesAsync();
        var id = Guid.NewGuid();
        var file = File(activity:
        [
            new TransferActivity(id, DateTimeOffset.UtcNow, 1, "Added", null, null),
            new TransferActivity(id, DateTimeOffset.UtcNow.AddMinutes(1), 1, "EpisodeIncremented", null, 1),
        ]);

        await CreateRunner(db, dbName, new(db), new(db), new(db)).RunAsync(file, NewProgress(), CancellationToken.None);

        var log = Assert.Single(await db.ActivityLogs.AsNoTracking().ToListAsync());
        Assert.Equal(ActivityChangeType.Added, log.ChangeType); // first occurrence only
    }

    [Fact]
    public async Task NothingIsReTimed()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDb(dbName);
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Anime 1" });
        var storedTime = DateTimeOffset.UtcNow.AddDays(-3);
        var existing = new ActivityLog { EventId = Guid.NewGuid(), AnimeId = 1, Timestamp = storedTime, ChangeType = ActivityChangeType.Added };
        db.ActivityLogs.Add(existing);
        await db.SaveChangesAsync();
        var file = File(activity: [new TransferActivity(existing.EventId, DateTimeOffset.UtcNow, 1, "Added", null, null)]);

        await CreateRunner(db, dbName, new(db), new(db), new(db)).RunAsync(file, NewProgress(), CancellationToken.None);

        var log = Assert.Single(await db.ActivityLogs.AsNoTracking().ToListAsync());
        Assert.Equal(storedTime, log.Timestamp);
    }

    [Fact]
    public async Task ACompletionThenAScoreComposeIntoOneRow()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDb(dbName);
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Anime 1" });
        await db.SaveChangesAsync();
        var t = DateTimeOffset.UtcNow;
        var file = File(activity:
        [
            new TransferActivity(Guid.NewGuid(), t, 1, "Completed", null, null),
            new TransferActivity(Guid.NewGuid(), t, 1, "ScoreChanged", "Score 9", null),
        ]);

        await CreateRunner(db, dbName, new(db), new(db), new(db)).RunAsync(file, NewProgress(), CancellationToken.None);

        var logs = await db.ActivityLogs.AsNoTracking().OrderBy(l => l.Id).ToListAsync();
        Assert.Equal(2, logs.Count);
        var mostRecentFirst = logs.OrderByDescending(l => l.Timestamp).ThenByDescending(l => l.Id).ToList();
        var merges = ActivityFeedComposer.FindCompletionScoreMerges(mostRecentFirst);
        var completion = logs.Single(l => l.ChangeType == ActivityChangeType.Completed);
        var merge = Assert.Single(merges);
        Assert.Equal(completion.Id, merge.Key);
        Assert.Equal(9, merge.Value.Score);
    }

    [Fact]
    public async Task AnUnknownChangeTypeIsReportedAndTheRestIsStored()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDb(dbName);
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Anime 1" });
        await db.SaveChangesAsync();
        var t = DateTimeOffset.UtcNow;
        var file = File(activity:
        [
            new TransferActivity(Guid.NewGuid(), t, 1, "SomeFutureChangeType", null, null),
            new TransferActivity(Guid.NewGuid(), t, 1, "Added", null, null),
        ]);

        var report = await CreateRunner(db, dbName, new(db), new(db), new(db)).RunAsync(file, NewProgress(), CancellationToken.None);

        Assert.Single(await db.ActivityLogs.AsNoTracking().ToListAsync());
        var failure = Assert.Single(report.Failures);
        Assert.Equal(TransferImportFailureSubject.Anime, failure.Subject);
        Assert.Equal(1, failure.Id);
        Assert.Contains("edit history", failure.What);
        Assert.Contains("1 record", failure.What);
        Assert.Equal(TransferImportFailureKind.EditHistory, failure.Kind);
    }

    [Fact]
    public async Task ARemovalRecordLeavesTheListEntryInPlace()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDb(dbName);
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Anime 1" });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1 });
        await db.SaveChangesAsync();
        var file = File(activity: [new TransferActivity(Guid.NewGuid(), DateTimeOffset.UtcNow, 1, "Removed", null, null)]);

        await CreateRunner(db, dbName, new(db), new(db), new(db)).RunAsync(file, NewProgress(), CancellationToken.None);

        Assert.NotNull(await db.UserAnimeEntries.AsNoTracking().FirstOrDefaultAsync(e => e.AnimeId == 1));
        Assert.Single(await db.ActivityLogs.AsNoTracking().ToListAsync());
    }

    // --- 8.2: Choices ---

    [Fact]
    public async Task ANewerChoiceWinsCarryingTheFilesTime()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDb(dbName);
        var oldTime = DateTimeOffset.UtcNow.AddDays(-2);
        db.AnimeMetadata.Add(new AnimeMetadata
        {
            Id = 1, Title = "T", MalPictureUrl = "https://mal/main.jpg",
            PictureUrl = "https://mal/old.jpg", SelectedPictureUrl = "https://mal/old.jpg", SelectedPictureModifiedAt = oldTime,
            PictureUrls = ["https://mal/old.jpg", "https://mal/new.jpg"],
        });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1 });
        await db.SaveChangesAsync();
        var newTime = oldTime.AddDays(1);
        var file = File(animePictures: [new TransferAnimePicture(1, "https://mal/new.jpg", newTime)]);

        await CreateRunner(db, dbName, new(db), new(db), new(db)).RunAsync(file, NewProgress(), CancellationToken.None);

        var anime = await db.AnimeMetadata.AsNoTracking().SingleAsync(a => a.Id == 1);
        Assert.Equal("https://mal/new.jpg", anime.SelectedPictureUrl);
        Assert.Equal(newTime, anime.SelectedPictureModifiedAt);
    }

    [Fact]
    public async Task AnOlderChoiceLoses()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDb(dbName);
        var storedTime = DateTimeOffset.UtcNow;
        db.AnimeMetadata.Add(new AnimeMetadata
        {
            Id = 1, Title = "T", MalPictureUrl = "https://mal/main.jpg",
            SelectedPictureUrl = "https://mal/current.jpg", SelectedPictureModifiedAt = storedTime,
            PictureUrls = ["https://mal/current.jpg", "https://mal/older.jpg"],
        });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1 });
        await db.SaveChangesAsync();
        var file = File(animePictures: [new TransferAnimePicture(1, "https://mal/older.jpg", storedTime.AddDays(-1))]);

        await CreateRunner(db, dbName, new(db), new(db), new(db)).RunAsync(file, NewProgress(), CancellationToken.None);

        var anime = await db.AnimeMetadata.AsNoTracking().SingleAsync(a => a.Id == 1);
        Assert.Equal("https://mal/current.jpg", anime.SelectedPictureUrl);
        Assert.Equal(storedTime, anime.SelectedPictureModifiedAt);
    }

    [Fact]
    public async Task AnEqualTimeChangesNothing()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDb(dbName);
        var storedTime = DateTimeOffset.UtcNow;
        db.AnimeMetadata.Add(new AnimeMetadata
        {
            Id = 1, Title = "T", MalPictureUrl = "https://mal/main.jpg",
            SelectedPictureUrl = "https://mal/current.jpg", SelectedPictureModifiedAt = storedTime,
            PictureUrls = ["https://mal/current.jpg", "https://mal/other.jpg"],
        });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1 });
        await db.SaveChangesAsync();
        var file = File(animePictures: [new TransferAnimePicture(1, "https://mal/other.jpg", storedTime)]);

        await CreateRunner(db, dbName, new(db), new(db), new(db)).RunAsync(file, NewProgress(), CancellationToken.None);

        var anime = await db.AnimeMetadata.AsNoTracking().SingleAsync(a => a.Id == 1);
        Assert.Equal("https://mal/current.jpg", anime.SelectedPictureUrl);
    }

    [Fact]
    public async Task ANewerClearWins()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDb(dbName);
        var oldTime = DateTimeOffset.UtcNow.AddDays(-2);
        db.AnimeMetadata.Add(new AnimeMetadata
        {
            Id = 1, Title = "T", MalPictureUrl = "https://mal/main.jpg",
            PictureUrl = "https://mal/chosen.jpg", SelectedPictureUrl = "https://mal/chosen.jpg", SelectedPictureModifiedAt = oldTime,
        });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1 });
        await db.SaveChangesAsync();
        var newTime = oldTime.AddDays(1);
        var file = File(animePictures: [new TransferAnimePicture(1, null, newTime)]);

        await CreateRunner(db, dbName, new(db), new(db), new(db)).RunAsync(file, NewProgress(), CancellationToken.None);

        var anime = await db.AnimeMetadata.AsNoTracking().SingleAsync(a => a.Id == 1);
        Assert.Null(anime.SelectedPictureUrl);
        Assert.Equal(newTime, anime.SelectedPictureModifiedAt);
        Assert.Equal("https://mal/main.jpg", anime.PictureUrl);
    }

    [Fact]
    public async Task WithNoTimeStoredTheFilesChoiceIsStored()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDb(dbName);
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "T", MalPictureUrl = "https://mal/main.jpg", PictureUrls = ["https://mal/main.jpg", "https://mal/alt.jpg"] });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1 });
        await db.SaveChangesAsync();
        var fileTime = DateTimeOffset.UtcNow.AddDays(-10);
        var file = File(animePictures: [new TransferAnimePicture(1, "https://mal/alt.jpg", fileTime)]);

        await CreateRunner(db, dbName, new(db), new(db), new(db)).RunAsync(file, NewProgress(), CancellationToken.None);

        var anime = await db.AnimeMetadata.AsNoTracking().SingleAsync(a => a.Id == 1);
        Assert.Equal("https://mal/alt.jpg", anime.SelectedPictureUrl);
        Assert.Equal(fileTime, anime.SelectedPictureModifiedAt);
    }

    [Fact]
    public async Task ASeriesTitleAndPictureAreDecidedIndependently()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDb(dbName);
        var localTitleTime = DateTimeOffset.UtcNow;
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Local Title", MalPictureUrl = "https://mal/main.jpg" });
        db.Series.Add(new AnimeTracker.Api.Models.Series
        {
            Id = 1, BuiltAt = DateTimeOffset.UtcNow, SelectedTitle = "Local Title", SelectedTitleModifiedAt = localTitleTime,
        });
        db.SeriesMembers.Add(new SeriesMember { SeriesId = 1, AnimeId = 1, IsMainLine = true, IsPrimary = true, Order = 0 });
        await db.SaveChangesAsync();
        var file = File(series:
        [
            new TransferSeries(1,
                Title: new TransferSeriesChoice("File Title", localTitleTime.AddDays(-1)), // older: loses
                Picture: new TransferSeriesChoice("https://mal/main.jpg", localTitleTime.AddDays(1))), // newer, no stored time: wins
        ]);

        await CreateRunner(db, dbName, new(db), new(db), new(db)).RunAsync(file, NewProgress(), CancellationToken.None);

        var series = await db.Series.AsNoTracking().SingleAsync(s => s.Id == 1);
        Assert.Equal("Local Title", series.SelectedTitle);
        Assert.Equal("https://mal/main.jpg", series.SelectedPictureUrl);
    }

    [Fact]
    public async Task AnAbsentBlockIsLeftAlone()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDb(dbName);
        var titleTime = DateTimeOffset.UtcNow;
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Local Title" });
        db.Series.Add(new AnimeTracker.Api.Models.Series { Id = 1, BuiltAt = DateTimeOffset.UtcNow, SelectedTitle = "Local Title", SelectedTitleModifiedAt = titleTime });
        db.SeriesMembers.Add(new SeriesMember { SeriesId = 1, AnimeId = 1, IsMainLine = true, IsPrimary = true, Order = 0 });
        await db.SaveChangesAsync();
        var file = File(series: [new TransferSeries(1, Picture: new TransferSeriesChoice(null, DateTimeOffset.UtcNow))]); // no Title block

        await CreateRunner(db, dbName, new(db), new(db), new(db)).RunAsync(file, NewProgress(), CancellationToken.None);

        var series = await db.Series.AsNoTracking().SingleAsync(s => s.Id == 1);
        Assert.Equal("Local Title", series.SelectedTitle);
        Assert.Equal(titleTime, series.SelectedTitleModifiedAt);
    }

    [Fact]
    public async Task AMovedRootResolvesThroughMembership()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDb(dbName);
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 90, Title = "Root Anime" });
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 100, Title = "New Title" });
        db.Series.Add(new AnimeTracker.Api.Models.Series { Id = 90, BuiltAt = DateTimeOffset.UtcNow });
        db.SeriesMembers.Add(new SeriesMember { SeriesId = 90, AnimeId = 90, IsMainLine = true, IsPrimary = true, Order = 0 });
        // Anime 100 was the old root; it moved, but still primarily belongs to series 90.
        db.SeriesMembers.Add(new SeriesMember { SeriesId = 90, AnimeId = 100, IsMainLine = true, IsPrimary = true, Order = 1 });
        await db.SaveChangesAsync();
        var file = File(series: [new TransferSeries(100, Title: new TransferSeriesChoice("New Title", DateTimeOffset.UtcNow))]);

        await CreateRunner(db, dbName, new(db), new(db), new(db)).RunAsync(file, NewProgress(), CancellationToken.None);

        var series = await db.Series.AsNoTracking().SingleAsync(s => s.Id == 90);
        Assert.Equal("New Title", series.SelectedTitle);
    }

    [Fact]
    public async Task TwoFileEntriesOnOneSeriesTheNewestWins()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDb(dbName);
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Title A" });
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 2, Title = "Title B" });
        db.Series.Add(new AnimeTracker.Api.Models.Series { Id = 1, BuiltAt = DateTimeOffset.UtcNow });
        db.SeriesMembers.Add(new SeriesMember { SeriesId = 1, AnimeId = 1, IsMainLine = true, IsPrimary = true, Order = 0 });
        db.SeriesMembers.Add(new SeriesMember { SeriesId = 1, AnimeId = 2, IsMainLine = true, IsPrimary = false, Order = 1 });
        await db.SaveChangesAsync();
        var t1 = DateTimeOffset.UtcNow;
        var t2 = t1.AddDays(1);
        var file = File(series:
        [
            new TransferSeries(1, Title: new TransferSeriesChoice("Title A", t1)),
            new TransferSeries(1, Title: new TransferSeriesChoice("Title B", t2)),
        ]);

        await CreateRunner(db, dbName, new(db), new(db), new(db)).RunAsync(file, NewProgress(), CancellationToken.None);

        var series = await db.Series.AsNoTracking().SingleAsync(s => s.Id == 1);
        Assert.Equal("Title B", series.SelectedTitle);
        Assert.Equal(t2, series.SelectedTitleModifiedAt);
    }

    // --- 8.3: Checks and the retry ---

    [Fact]
    public async Task ANeverFetchedSetIsAcceptedAfterItsRefresh()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDb(dbName);
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "T", MalPictureUrl = "https://mal/main.jpg", PictureUrl = "https://mal/main.jpg" });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1 });
        await db.SaveChangesAsync();
        var pictures = new FakePictureRefreshService(db);
        pictures.PicturesToInstallOnRefresh[1] = ["https://mal/new.jpg"];
        var file = File(animePictures: [new TransferAnimePicture(1, "https://mal/new.jpg", DateTimeOffset.UtcNow)]);

        var report = await CreateRunner(db, dbName, new(db), new(db), pictures).RunAsync(file, NewProgress(), CancellationToken.None);

        Assert.Empty(report.Failures);
        Assert.Contains((1, true), pictures.Calls);
        var anime = await db.AnimeMetadata.AsNoTracking().SingleAsync(a => a.Id == 1);
        Assert.Equal("https://mal/new.jpg", anime.SelectedPictureUrl);
    }

    [Fact]
    public async Task ASetFetchedBeforeIsFetchedAgain()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDb(dbName);
        db.AnimeMetadata.Add(new AnimeMetadata
        {
            Id = 1, Title = "T", MalPictureUrl = "https://mal/main.jpg", PictureUrl = "https://mal/main.jpg",
            PicturesSyncedAt = DateTimeOffset.UtcNow.AddDays(-30), PictureUrls = ["https://mal/old.jpg"],
        });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1 });
        await db.SaveChangesAsync();
        var pictures = new FakePictureRefreshService(db);
        pictures.PicturesToInstallOnRefresh[1] = ["https://mal/old.jpg", "https://mal/new.jpg"];
        var file = File(animePictures: [new TransferAnimePicture(1, "https://mal/new.jpg", DateTimeOffset.UtcNow)]);

        var report = await CreateRunner(db, dbName, new(db), new(db), pictures).RunAsync(file, NewProgress(), CancellationToken.None);

        Assert.Empty(report.Failures);
        Assert.Contains((1, true), pictures.Calls); // evenIfFetched: true, despite PicturesSyncedAt already set
    }

    [Fact]
    public async Task AChoiceStillRefusedIsReported()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDb(dbName);
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "T", MalPictureUrl = "https://mal/main.jpg", PictureUrl = "https://mal/main.jpg" });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1 });
        await db.SaveChangesAsync();
        var file = File(animePictures: [new TransferAnimePicture(1, "https://mal/never-exists.jpg", DateTimeOffset.UtcNow)]);

        var report = await CreateRunner(db, dbName, new(db), new(db), new(db)).RunAsync(file, NewProgress(), CancellationToken.None);

        var failure = Assert.Single(report.Failures);
        Assert.Equal("chosen picture", failure.What);
        Assert.Equal(TransferImportFailureKind.ChosenPicture, failure.Kind);
        var anime = await db.AnimeMetadata.AsNoTracking().SingleAsync(a => a.Id == 1);
        Assert.Null(anime.SelectedPictureUrl);
    }

    [Fact]
    public async Task AnAnimeNotInMyListIsReported()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDb(dbName);
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "T", MalPictureUrl = "https://mal/main.jpg", PictureUrls = ["https://mal/alt.jpg"] });
        await db.SaveChangesAsync(); // no UserAnimeEntry
        var file = File(animePictures: [new TransferAnimePicture(1, "https://mal/alt.jpg", DateTimeOffset.UtcNow)]);

        var report = await CreateRunner(db, dbName, new(db), new(db), new(db)).RunAsync(file, NewProgress(), CancellationToken.None);

        var failure = Assert.Single(report.Failures);
        Assert.Equal("chosen picture", failure.What);
        Assert.Equal(TransferImportFailureKind.ChosenPicture, failure.Kind);
        Assert.Contains("not in my list", failure.Reason);
    }

    [Fact]
    public async Task ATitleThatIsNotATrimIsReported()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDb(dbName);
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Series Title" });
        db.Series.Add(new AnimeTracker.Api.Models.Series { Id = 1, BuiltAt = DateTimeOffset.UtcNow });
        db.SeriesMembers.Add(new SeriesMember { SeriesId = 1, AnimeId = 1, IsMainLine = true, IsPrimary = true, Order = 0 });
        await db.SaveChangesAsync();
        var file = File(series: [new TransferSeries(1, Title: new TransferSeriesChoice("Invented Title", DateTimeOffset.UtcNow))]);

        var report = await CreateRunner(db, dbName, new(db), new(db), new(db)).RunAsync(file, NewProgress(), CancellationToken.None);

        var failure = Assert.Single(report.Failures);
        Assert.Equal(TransferImportFailureSubject.Series, failure.Subject);
        Assert.Equal("series title", failure.What);
        Assert.Equal(TransferImportFailureKind.SeriesTitle, failure.Kind);
        var series = await db.Series.AsNoTracking().SingleAsync(s => s.Id == 1);
        Assert.Null(series.SelectedTitle);
    }

    [Fact]
    public async Task AClearPassesWithNoCheck()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDb(dbName);
        var oldTime = DateTimeOffset.UtcNow.AddDays(-1);
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "T", SelectedPictureUrl = "https://mal/chosen.jpg", SelectedPictureModifiedAt = oldTime, MalPictureUrl = "https://mal/main.jpg" });
        await db.SaveChangesAsync(); // no UserAnimeEntry: not in my list
        var newTime = oldTime.AddDays(1);
        var file = File(animePictures: [new TransferAnimePicture(1, null, newTime)]);

        var report = await CreateRunner(db, dbName, new(db), new(db), new(db)).RunAsync(file, NewProgress(), CancellationToken.None);

        Assert.Empty(report.Failures);
        var anime = await db.AnimeMetadata.AsNoTracking().SingleAsync(a => a.Id == 1);
        Assert.Null(anime.SelectedPictureUrl);
        Assert.Equal(newTime, anime.SelectedPictureModifiedAt);
    }

    // --- design.md D14: a refused TMDB choice is refreshed from TMDB, not MyAnimeList ---

    [Fact]
    public async Task ATmdbChoiceIsAcceptedOnceItsSetsAreFetchedAndNoMalRequestIsMade()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDb(dbName);
        AddAnime(db, 1, tvId: 1429, season: 3);
        await db.SaveChangesAsync();
        var client = new FakeTmdbClient().Serve(TmdbSetKey.Season(1429, 3), [Image("/s3.jpg", "ja")]);
        var pictures = new FakePictureRefreshService(db);
        var chosen = TmdbImageUrl.Original("/s3.jpg");
        var file = File(animePictures: [new TransferAnimePicture(1, chosen, DateTimeOffset.UtcNow)]);
        var progress = NewProgress();

        var report = await CreateRunner(db, dbName, new(db), new(db), pictures, ArtworkService(db, client))
            .RunAsync(file, progress, CancellationToken.None);

        Assert.Empty(report.Failures);
        var anime = await db.AnimeMetadata.AsNoTracking().SingleAsync(a => a.Id == 1);
        Assert.Equal(chosen, anime.SelectedPictureUrl);
        Assert.Equal([TmdbSetKey.Tv(1429), TmdbSetKey.Season(1429, 3)], client.Calls);
        Assert.Empty(pictures.Calls); // a TMDB choice is never a reason to ask MyAnimeList
        Assert.Equal((1, 1), (progress.Snapshot.Done, progress.Snapshot.Total)); // the one refresh, counted and finished
    }

    [Fact]
    public async Task ATmdbSetFetchedYesterdayIsFetchedAgainBecauseTheRefreshIsForced()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDb(dbName);
        AddAnime(db, 1, tvId: 1429, season: 3);
        db.TmdbTvImageSets.Add(TvSet(1429, Fresh, Poster("/tv.jpg", "ja")));
        db.TmdbSeasonImageSets.Add(SeasonSet(1429, 3, Fresh, Poster("/s3-old.jpg", "ja")));
        await db.SaveChangesAsync();
        // TMDB has added a poster since this device last fetched the season.
        var client = new FakeTmdbClient().Serve(TmdbSetKey.Season(1429, 3), [Image("/s3-old.jpg", "ja"), Image("/s3-new.jpg", "ja")]);
        var chosen = TmdbImageUrl.Original("/s3-new.jpg");
        var file = File(animePictures: [new TransferAnimePicture(1, chosen, DateTimeOffset.UtcNow)]);

        var report = await CreateRunner(db, dbName, new(db), new(db), new(db), ArtworkService(db, client))
            .RunAsync(file, NewProgress(), CancellationToken.None);

        Assert.Empty(report.Failures);
        Assert.Contains(TmdbSetKey.Season(1429, 3), client.Calls); // not due for 29 more days, fetched all the same
        var anime = await db.AnimeMetadata.AsNoTracking().SingleAsync(a => a.Id == 1);
        Assert.Equal(chosen, anime.SelectedPictureUrl);
    }

    [Fact]
    public async Task ATmdbChoiceWithNoKeyIsReportedAndNotStored()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDb(dbName);
        AddAnime(db, 1, tvId: 1429, season: 3);
        await db.SaveChangesAsync();
        var client = new FakeTmdbClient().Serve(TmdbSetKey.Season(1429, 3), [Image("/s3.jpg", "ja")]); // would answer, if asked
        var pictures = new FakePictureRefreshService(db);
        var file = File(animePictures: [new TransferAnimePicture(1, TmdbImageUrl.Original("/s3.jpg"), DateTimeOffset.UtcNow)]);

        var report = await CreateRunner(db, dbName, new(db), new(db), pictures, ArtworkService(db, client, apiKey: ""))
            .RunAsync(file, NewProgress(), CancellationToken.None);

        var failure = Assert.Single(report.Failures);
        Assert.Equal("chosen picture", failure.What);
        Assert.Equal(TransferImportFailureKind.ChosenPicture, failure.Kind);
        var anime = await db.AnimeMetadata.AsNoTracking().SingleAsync(a => a.Id == 1);
        Assert.Null(anime.SelectedPictureUrl);
        Assert.Empty(client.Calls);
        Assert.Empty(pictures.Calls);
    }

    [Fact]
    public async Task AMalChoiceIsStillRefreshedFromMalAndTmdbIsNeverAsked()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDb(dbName);
        AddAnime(db, 1, tvId: 1429, season: 3).MalPictureUrl = "https://mal/main.jpg";
        await db.SaveChangesAsync();
        var client = new FakeTmdbClient(); // a key is configured, so TMDB would be asked if the route were wrong
        var pictures = new FakePictureRefreshService(db);
        pictures.PicturesToInstallOnRefresh[1] = ["https://mal/new.jpg"];
        var file = File(animePictures: [new TransferAnimePicture(1, "https://mal/new.jpg", DateTimeOffset.UtcNow)]);

        var report = await CreateRunner(db, dbName, new(db), new(db), pictures, ArtworkService(db, client))
            .RunAsync(file, NewProgress(), CancellationToken.None);

        Assert.Empty(report.Failures);
        Assert.Contains((1, true), pictures.Calls);
        Assert.Empty(client.Calls);
        var anime = await db.AnimeMetadata.AsNoTracking().SingleAsync(a => a.Id == 1);
        Assert.Equal("https://mal/new.jpg", anime.SelectedPictureUrl);
    }

    [Fact]
    public async Task ATmdbSeriesChoiceIsAcceptedOnceTheWholeFranchiseIsFetched()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDb(dbName);
        AddAnime(db, 1, tvId: 1429, season: 1);
        AddAnime(db, 2, tvId: 1429, season: 2);
        AddAnime(db, 3, movieIds: [635302]); // a film among the extras
        AddSeries(db, 1, [1, 2], (3, null, 0));
        await db.SaveChangesAsync();
        var client = new FakeTmdbClient().Serve(TmdbSetKey.Movie(635302), [Image("/film.jpg", "ja")]);
        var pictures = new FakePictureRefreshService(db);
        var chosen = TmdbImageUrl.Original("/film.jpg");
        var file = File(series: [new TransferSeries(1, Picture: new TransferSeriesChoice(chosen, DateTimeOffset.UtcNow))]);
        var progress = NewProgress();

        var report = await CreateRunner(db, dbName, new(db), new(db), pictures, ArtworkService(db, client))
            .RunAsync(file, progress, CancellationToken.None);

        Assert.Empty(report.Failures);
        var series = await db.Series.AsNoTracking().SingleAsync(s => s.Id == 1);
        Assert.Equal(chosen, series.SelectedPictureUrl);
        Assert.Equal(
            [TmdbSetKey.Tv(1429), TmdbSetKey.Season(1429, 1), TmdbSetKey.Season(1429, 2), TmdbSetKey.Movie(635302)],
            client.Calls);
        Assert.Empty(pictures.Calls); // no main-line member is asked for its MyAnimeList set
        Assert.Equal((2, 2), (progress.Snapshot.Done, progress.Snapshot.Total)); // resolving the series, then the one refresh
    }

    [Fact]
    public async Task AnImportFetchesEverySetOfTheFranchiseNotJustTheSeriesPagesBudget()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDb(dbName);
        // 25 main-line members with a film of their own each: 25 sets, five more
        // than a series page fetches in one visit.
        var members = Enumerable.Range(1, 25).ToArray();
        foreach (var id in members)
            AddAnime(db, id, movieIds: [1000 + id]);
        AddSeries(db, 1, members);
        await db.SaveChangesAsync();
        var client = new FakeTmdbClient().Serve(TmdbSetKey.Movie(1025), [Image("/last.jpg", "ja")]); // the 25th set
        var chosen = TmdbImageUrl.Original("/last.jpg");
        var file = File(series: [new TransferSeries(1, Picture: new TransferSeriesChoice(chosen, DateTimeOffset.UtcNow))]);

        var report = await CreateRunner(db, dbName, new(db), new(db), new(db), ArtworkService(db, client))
            .RunAsync(file, NewProgress(), CancellationToken.None);

        Assert.Empty(report.Failures);
        Assert.Equal(25, client.Calls.Count);
        var series = await db.Series.AsNoTracking().SingleAsync(s => s.Id == 1);
        Assert.Equal(chosen, series.SelectedPictureUrl);
    }

    [Fact]
    public async Task ATmdbSeriesChoiceWithNoKeyIsReportedAndNotStored()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDb(dbName);
        AddAnime(db, 1, movieIds: [635302]);
        AddSeries(db, 1, [1]);
        await db.SaveChangesAsync();
        var client = new FakeTmdbClient().Serve(TmdbSetKey.Movie(635302), [Image("/film.jpg", "ja")]);
        var pictures = new FakePictureRefreshService(db);
        var file = File(series: [new TransferSeries(1, Picture: new TransferSeriesChoice(TmdbImageUrl.Original("/film.jpg"), DateTimeOffset.UtcNow))]);

        var report = await CreateRunner(db, dbName, new(db), new(db), pictures, ArtworkService(db, client, apiKey: ""))
            .RunAsync(file, NewProgress(), CancellationToken.None);

        var failure = Assert.Single(report.Failures);
        Assert.Equal(TransferImportFailureSubject.Series, failure.Subject);
        Assert.Equal("series picture", failure.What);
        Assert.Equal(TransferImportFailureKind.SeriesPicture, failure.Kind);
        var series = await db.Series.AsNoTracking().SingleAsync(s => s.Id == 1);
        Assert.Null(series.SelectedPictureUrl);
        Assert.Empty(client.Calls);
        Assert.Empty(pictures.Calls);
    }

    // --- 8.4: Ranking ---

    [Fact]
    public async Task ANewerRankingReplacesThisOneAndDropsTheMissingAnime()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDb(dbName);
        db.AnimeMetadata.AddRange(new AnimeMetadata { Id = 1, Title = "A" }, new AnimeMetadata { Id = 2, Title = "B" }, new AnimeMetadata { Id = 3, Title = "C" });
        await db.SaveChangesAsync();
        var t1 = DateTimeOffset.UtcNow.AddDays(-1);
        await new TopAnimeSelectionRepository(db).ReplaceAllAsync([1, 2, 3], t1);
        var t2 = t1.AddDays(1);
        var file = File(ranking: new TransferRanking([3, 4, 1], t2)); // anime 4 is unseen and fetchable

        var report = await CreateRunner(db, dbName, new(db), new(db), new(db)).RunAsync(file, NewProgress(), CancellationToken.None);

        var order = await new TopAnimeSelectionRepository(db).GetOrderedAnimeIdsAsync();
        Assert.Equal([3, 4, 1], order);
        Assert.Equal(t2, await new TopAnimeSelectionRepository(db).GetModifiedAtAsync());
        Assert.Equal([4], report.RankingAdded.Select(a => a.AnimeId));
        Assert.Equal([2], report.RankingRemoved.Select(a => a.AnimeId));
    }

    [Fact]
    public async Task AnOlderRankingIsLeftAlone()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDb(dbName);
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "A" });
        await db.SaveChangesAsync();
        var t1 = DateTimeOffset.UtcNow;
        await new TopAnimeSelectionRepository(db).ReplaceAllAsync([1], t1);
        var file = File(ranking: new TransferRanking([1], t1.AddDays(-1)));

        await CreateRunner(db, dbName, new(db), new(db), new(db)).RunAsync(file, NewProgress(), CancellationToken.None);

        Assert.Equal(t1, await new TopAnimeSelectionRepository(db).GetModifiedAtAsync());
    }

    [Fact]
    public async Task ANullRankingTimeLeavesTheRankingUnchanged()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDb(dbName);
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "A" });
        await db.SaveChangesAsync();
        var t1 = DateTimeOffset.UtcNow;
        await new TopAnimeSelectionRepository(db).ReplaceAllAsync([1], t1);
        var file = File(ranking: new TransferRanking([2, 3], null));

        await CreateRunner(db, dbName, new(db), new(db), new(db)).RunAsync(file, NewProgress(), CancellationToken.None);

        Assert.Equal([1], await new TopAnimeSelectionRepository(db).GetOrderedAnimeIdsAsync());
    }

    [Fact]
    public async Task AnEmptiedNewerRankingEmptiesThisOneAndCarriesItsTime()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDb(dbName);
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "A" });
        await db.SaveChangesAsync();
        var t1 = DateTimeOffset.UtcNow;
        await new TopAnimeSelectionRepository(db).ReplaceAllAsync([1], t1);
        var t2 = t1.AddDays(1);
        var file = File(ranking: new TransferRanking([], t2));

        await CreateRunner(db, dbName, new(db), new(db), new(db)).RunAsync(file, NewProgress(), CancellationToken.None);

        Assert.Empty(await new TopAnimeSelectionRepository(db).GetOrderedAnimeIdsAsync());
        Assert.Equal(t2, await new TopAnimeSelectionRepository(db).GetModifiedAtAsync());
    }

    [Fact]
    public async Task AnUnfetchableAnimeLeavesTheRankingAloneAndIsReported()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDb(dbName);
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "A" });
        await db.SaveChangesAsync();
        var t1 = DateTimeOffset.UtcNow;
        await new TopAnimeSelectionRepository(db).ReplaceAllAsync([1], t1);
        var metadata = new FakeMetadataRefreshService(db);
        metadata.NotFoundIds.Add(999);
        var file = File(ranking: new TransferRanking([1, 999], t1.AddDays(1)));

        var report = await CreateRunner(db, dbName, metadata, new(db), new(db)).RunAsync(file, NewProgress(), CancellationToken.None);

        Assert.Equal([1], await new TopAnimeSelectionRepository(db).GetOrderedAnimeIdsAsync());
        Assert.Equal(t1, await new TopAnimeSelectionRepository(db).GetModifiedAtAsync());
        var failure = Assert.Single(report.Failures, f => f.What == "ranking");
        Assert.Equal(TransferImportFailureKind.Ranking, failure.Kind);
        Assert.Equal(999, failure.Id);
    }

    [Fact]
    public async Task APureReorderListsNothing()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDb(dbName);
        db.AnimeMetadata.AddRange(new AnimeMetadata { Id = 1, Title = "A" }, new AnimeMetadata { Id = 2, Title = "B" });
        await db.SaveChangesAsync();
        var t1 = DateTimeOffset.UtcNow;
        await new TopAnimeSelectionRepository(db).ReplaceAllAsync([1, 2], t1);
        var file = File(ranking: new TransferRanking([2, 1], t1.AddDays(1)));

        var report = await CreateRunner(db, dbName, new(db), new(db), new(db)).RunAsync(file, NewProgress(), CancellationToken.None);

        Assert.Empty(report.RankingAdded);
        Assert.Empty(report.RankingRemoved);
        Assert.Equal([2, 1], await new TopAnimeSelectionRepository(db).GetOrderedAnimeIdsAsync());
    }

    // --- 8.5: Fetching ---

    [Fact]
    public async Task AnUnseenAnimeIsFetchedAndListed()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDb(dbName);
        var file = File(activity: [new TransferActivity(Guid.NewGuid(), DateTimeOffset.UtcNow, 555, "Added", null, null)]);

        var report = await CreateRunner(db, dbName, new(db), new(db), new(db)).RunAsync(file, NewProgress(), CancellationToken.None);

        Assert.Contains(report.Fetched, a => a.AnimeId == 555);
        Assert.Single(await db.ActivityLogs.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task A404IsReportedAndThatAnimesRecordsAreSkipped()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDb(dbName);
        var metadata = new FakeMetadataRefreshService(db);
        metadata.NotFoundIds.Add(555);
        var file = File(activity: [new TransferActivity(Guid.NewGuid(), DateTimeOffset.UtcNow, 555, "Added", null, null)]);

        var report = await CreateRunner(db, dbName, metadata, new(db), new(db)).RunAsync(file, NewProgress(), CancellationToken.None);

        Assert.Empty(await db.ActivityLogs.AsNoTracking().ToListAsync());
        var failure = Assert.Single(report.Failures);
        Assert.Contains("edit history", failure.What);
        Assert.Equal(TransferImportFailureKind.EditHistory, failure.Kind);
        Assert.Contains("MyAnimeList has no anime", failure.Reason);
    }

    [Fact]
    public async Task AnUnbuiltSeriesIsBuilt()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDb(dbName);
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "T" });
        await db.SaveChangesAsync();
        var series = new FakeSeriesService(db);
        series.BuildActions[1] = () =>
        {
            db.Series.Add(new AnimeTracker.Api.Models.Series { Id = 1, BuiltAt = DateTimeOffset.UtcNow });
            db.SeriesMembers.Add(new SeriesMember { SeriesId = 1, AnimeId = 1, IsMainLine = true, IsPrimary = true, Order = 0 });
        };
        var file = File(series: [new TransferSeries(1, Title: new TransferSeriesChoice("T", DateTimeOffset.UtcNow))]);

        await CreateRunner(db, dbName, new(db), series, new(db)).RunAsync(file, NewProgress(), CancellationToken.None);

        Assert.Contains(1, series.BuildCalls);
        Assert.NotNull(await db.Series.AsNoTracking().FirstOrDefaultAsync(s => s.Id == 1));
    }

    [Fact]
    public async Task AnUnbuildableSeriesIsReported()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDb(dbName);
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "T" });
        await db.SaveChangesAsync();
        var series = new FakeSeriesService(db);
        series.Unbuildable.Add(1);
        var file = File(series: [new TransferSeries(1, Title: new TransferSeriesChoice("T", DateTimeOffset.UtcNow))]);

        var report = await CreateRunner(db, dbName, new(db), series, new(db)).RunAsync(file, NewProgress(), CancellationToken.None);

        var failure = Assert.Single(report.Failures);
        Assert.Equal(TransferImportFailureSubject.Series, failure.Subject);
        Assert.Equal(TransferImportFailureKind.SeriesTitle, failure.Kind);
        Assert.Contains("not part of a series", failure.Reason);
    }

    // --- 8.6: Re-importing and side effects ---

    [Fact]
    public async Task ReImportingTheSameFileChangesNothingAndReportsNothing()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDb(dbName);
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "T", MalPictureUrl = "https://mal/main.jpg", PictureUrls = ["https://mal/main.jpg", "https://mal/alt.jpg"] });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1 });
        await db.SaveChangesAsync();
        var file = File(animePictures: [new TransferAnimePicture(1, "https://mal/alt.jpg", DateTimeOffset.UtcNow.AddDays(-1))]);
        var runner = CreateRunner(db, dbName, new(db), new(db), new(db));
        await runner.RunAsync(file, NewProgress(), CancellationToken.None);
        var stampAfterFirstRun = (await db.AnimeMetadata.AsNoTracking().SingleAsync(a => a.Id == 1)).SelectedPictureModifiedAt;

        var report = await runner.RunAsync(file, NewProgress(), CancellationToken.None);

        Assert.Empty(report.Failures);
        Assert.Empty(report.Fetched);
        Assert.Empty(report.RankingAdded);
        Assert.Empty(report.RankingRemoved);
        var anime = await db.AnimeMetadata.AsNoTracking().SingleAsync(a => a.Id == 1);
        Assert.Equal(stampAfterFirstRun, anime.SelectedPictureModifiedAt);
    }

    [Fact]
    public async Task NoListEntryIsWrittenAndNoPendingSyncIsSet()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDb(dbName);
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "T" });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Status = WatchStatus.Watching, EpisodesWatched = 0, PendingSync = false });
        await db.SaveChangesAsync();
        var file = File(activity: [new TransferActivity(Guid.NewGuid(), DateTimeOffset.UtcNow, 1, "EpisodeIncremented", null, 0)]);

        await CreateRunner(db, dbName, new(db), new(db), new(db)).RunAsync(file, NewProgress(), CancellationToken.None);

        var entry = await db.UserAnimeEntries.AsNoTracking().SingleAsync(e => e.AnimeId == 1);
        Assert.Equal(0, entry.EpisodesWatched);
        Assert.False(entry.PendingSync);
    }

    [Fact]
    public async Task TheImportRecordsNoActivityOfItsOwn()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDb(dbName);
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "T", MalPictureUrl = "https://mal/main.jpg", PictureUrls = ["https://mal/main.jpg", "https://mal/alt.jpg"] });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1 });
        await db.SaveChangesAsync();
        var file = File(animePictures: [new TransferAnimePicture(1, "https://mal/alt.jpg", DateTimeOffset.UtcNow)]);

        await CreateRunner(db, dbName, new(db), new(db), new(db)).RunAsync(file, NewProgress(), CancellationToken.None);

        Assert.Empty(await db.ActivityLogs.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task TheDeviceIdIsUnchanged()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDb(dbName);
        var deviceId = Guid.NewGuid();
        db.DeviceIdentities.Add(new DeviceIdentity { DeviceId = deviceId });
        await db.SaveChangesAsync();
        var file = File(activity: [new TransferActivity(Guid.NewGuid(), DateTimeOffset.UtcNow, 1, "Added", null, null)]);

        await CreateRunner(db, dbName, new(db), new(db), new(db)).RunAsync(file, NewProgress(), CancellationToken.None);

        Assert.Equal(deviceId, (await db.DeviceIdentities.AsNoTracking().SingleAsync()).DeviceId);
    }
}
