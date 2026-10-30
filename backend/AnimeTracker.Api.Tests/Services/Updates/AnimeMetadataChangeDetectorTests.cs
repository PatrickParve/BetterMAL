using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Mal.Dto;
using AnimeTracker.Api.Services.Relations;
using AnimeTracker.Api.Services.Series;
using AnimeTracker.Api.Services.Updates;
using Microsoft.EntityFrameworkCore;
using AnimeTracker.Api.Tests.Services.ScoreHistory;

namespace AnimeTracker.Api.Tests.Services.Updates;

// IAnimeMetadataChangeDetector.RecordAsync's discovery gates (design.md D4;
// scope-updates-to-my-list tasks 6.4): a RelationDiscovery is written only
// for a non-Dropped list entry of the user's own that had already been fully
// fetched before this write — never on a first full-detail fetch (the Yani
// Neko case), and never for an anime the user does not track at all.
public class AnimeMetadataChangeDetectorTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static AnimeMetadataChangeDetector CreateDetector(AnimeTrackerDbContext db, ISeriesBuildTrigger trigger) =>
        new(db, new AnimeUpdateRecorder(db, new AnimeUpdateRelevance(db, new RelationResolver(db))), trigger, ScoreRecorderTestKit.Create(db));

    [Fact]
    public async Task AFirstFullDetailFetchWritesZeroDiscoveriesButStillEnqueuesTheBuild()
    {
        using var db = CreateDb();
        // Anime 1 is the user's own entry, so the discovery gate's list-entry
        // half would pass on its own — isolating that HadFullDetail alone is
        // what silences this, distinct from the not-my-entry case below.
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1", LastSyncedAt = default };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Status = WatchStatus.Watching });
        await db.SaveChangesAsync();

        var trigger = new SeriesBuildTrigger();
        var detector = CreateDetector(db, trigger);
        var before = detector.Snapshot(anime); // HadFullDetail false; Relations captured as an empty set

        anime.RelatedAnime.Add(new AnimeRelatedAnime { AnimeId = 1, RelatedAnimeId = 2, RelationType = "sequel", Title = "Anime 2" });
        anime.LastSyncedAt = DateTimeOffset.UtcNow; // the fetch itself stamps this, as ApplyTo would

        await detector.RecordAsync(anime, before, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();

        Assert.Empty(await db.RelationDiscoveries.ToListAsync());

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        Assert.Equal(1, await trigger.WaitAsync(cts.Token));
    }

    [Fact]
    public async Task ARefreshOfMyOwnFullyFetchedEntryWritesTheDiscovery()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1", LastSyncedAt = DateTimeOffset.UtcNow.AddDays(-60) };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Status = WatchStatus.Watching });
        await db.SaveChangesAsync();

        var detector = CreateDetector(db, new SeriesBuildTrigger());
        var before = detector.Snapshot(anime); // HadFullDetail true; Relations captured as an empty set

        anime.RelatedAnime.Add(new AnimeRelatedAnime { AnimeId = 1, RelatedAnimeId = 2, RelationType = "sequel", Title = "Anime 2" });
        anime.LastSyncedAt = DateTimeOffset.UtcNow;

        await detector.RecordAsync(anime, before, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();

        var discovery = Assert.Single(await db.RelationDiscoveries.AsNoTracking().ToListAsync());
        Assert.Equal(1, discovery.AnimeId);
        Assert.Equal(2, discovery.RelatedAnimeId);
    }

    [Fact]
    public async Task ARefreshOfAnAnimeThatIsNotMyEntryWritesNoDiscovery()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1", LastSyncedAt = DateTimeOffset.UtcNow.AddDays(-60) };
        db.AnimeMetadata.Add(anime); // no UserAnimeEntries at all
        await db.SaveChangesAsync();

        var detector = CreateDetector(db, new SeriesBuildTrigger());
        var before = detector.Snapshot(anime);

        anime.RelatedAnime.Add(new AnimeRelatedAnime { AnimeId = 1, RelatedAnimeId = 2, RelationType = "sequel", Title = "Anime 2" });
        anime.LastSyncedAt = DateTimeOffset.UtcNow;

        await detector.RecordAsync(anime, before, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();

        Assert.Empty(await db.RelationDiscoveries.ToListAsync());
    }

    [Fact]
    public async Task ARefreshOfMyDroppedEntryWritesNoDiscovery()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1", LastSyncedAt = DateTimeOffset.UtcNow.AddDays(-60) };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Status = WatchStatus.Dropped });
        await db.SaveChangesAsync();

        var detector = CreateDetector(db, new SeriesBuildTrigger());
        var before = detector.Snapshot(anime);

        anime.RelatedAnime.Add(new AnimeRelatedAnime { AnimeId = 1, RelatedAnimeId = 2, RelationType = "sequel", Title = "Anime 2" });
        anime.LastSyncedAt = DateTimeOffset.UtcNow;

        await detector.RecordAsync(anime, before, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();

        Assert.Empty(await db.RelationDiscoveries.ToListAsync());
    }

    // fix-announcements-lost-to-series-build D1, D3: a discovery records
    // whether its far end had ever been fully fetched when the edge appeared,
    // read from committed data. "No row at all" and "a lean row" are both
    // never-fully-fetched; only a row with LastSyncedAt set is not.
    private static async Task<RelationDiscovery> RecordANewSequelEdgeAsync(AnimeMetadata? farEnd)
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1", LastSyncedAt = DateTimeOffset.UtcNow.AddDays(-60) };
        db.AnimeMetadata.Add(anime);
        if (farEnd is not null)
            db.AnimeMetadata.Add(farEnd);
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Status = WatchStatus.Watching });
        await db.SaveChangesAsync();

        var detector = CreateDetector(db, new SeriesBuildTrigger());
        var before = detector.Snapshot(anime);

        anime.RelatedAnime.Add(new AnimeRelatedAnime { AnimeId = 1, RelatedAnimeId = 2, RelationType = "sequel", Title = "Anime 2" });
        anime.LastSyncedAt = DateTimeOffset.UtcNow;

        await detector.RecordAsync(anime, before, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();

        return Assert.Single(await db.RelationDiscoveries.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task ANewEdgeToAnAnimeWithNoCachedRowRecordsItAsNeverFullyFetched()
    {
        var discovery = await RecordANewSequelEdgeAsync(farEnd: null);

        Assert.False(discovery.RelatedAnimeHadFullDetail);
    }

    [Fact]
    public async Task ANewEdgeToAnAnimeWithALeanRowRecordsItAsNeverFullyFetched()
    {
        var discovery = await RecordANewSequelEdgeAsync(
            new AnimeMetadata { Id = 2, Title = "Anime 2", LastSyncedAt = default });

        Assert.False(discovery.RelatedAnimeHadFullDetail);
    }

    [Fact]
    public async Task ANewEdgeToAnAnimeWithAFullDetailRowRecordsItAsAlreadyFullyFetched()
    {
        var discovery = await RecordANewSequelEdgeAsync(
            new AnimeMetadata { Id = 2, Title = "Anime 2", LastSyncedAt = DateTimeOffset.UtcNow.AddDays(-30) });

        Assert.True(discovery.RelatedAnimeHadFullDetail);
    }

    // record-both-ends-of-a-schedule-move design.md D2: both ends of a
    // schedule move are in hand at the moment RecordFieldUpdates compares
    // before against anime, so it passes the just-written value as the
    // moved-to end — no new read, no new query.
    [Fact]
    public async Task APremiereMovePassesTheJustWrittenDateAsTheMovedToEnd()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata
        {
            Id = 1,
            Title = "Anime 1",
            LastSyncedAt = DateTimeOffset.UtcNow.AddDays(-60),
            AiringStatus = "not_yet_aired",
            AiredFrom = new DateOnly(2026, 10, 8),
        };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Status = WatchStatus.Watching });
        await db.SaveChangesAsync();

        var detector = CreateDetector(db, new SeriesBuildTrigger());
        var before = detector.Snapshot(anime);

        anime.AiredFrom = new DateOnly(2026, 10, 1);

        await detector.RecordAsync(anime, before, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();

        var update = Assert.Single(await db.AnimeUpdates.AsNoTracking().ToListAsync());
        Assert.Equal(new DateOnly(2026, 10, 8), update.PreviousStartDate);
        Assert.Equal(new DateOnly(2026, 10, 1), update.NewStartDate);
    }

    [Fact]
    public async Task ASlotMovePassesTheJustWrittenSlotAsTheMovedToEnd()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata
        {
            Id = 1,
            Title = "Anime 1",
            LastSyncedAt = DateTimeOffset.UtcNow.AddDays(-60),
            AiringStatus = "currently_airing",
            BroadcastDayOfWeek = "mondays",
            BroadcastTime = new TimeOnly(12, 0),
        };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Status = WatchStatus.Watching });
        await db.SaveChangesAsync();

        var detector = CreateDetector(db, new SeriesBuildTrigger());
        var before = detector.Snapshot(anime);

        anime.BroadcastDayOfWeek = "tuesdays";
        anime.BroadcastTime = new TimeOnly(13, 30);

        await detector.RecordAsync(anime, before, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();

        var update = Assert.Single(await db.AnimeUpdates.AsNoTracking().ToListAsync());
        Assert.Equal("mondays", update.PreviousBroadcastDayOfWeek);
        Assert.Equal(new TimeOnly(12, 0), update.PreviousBroadcastTime);
        Assert.Equal("tuesdays", update.NewBroadcastDayOfWeek);
        Assert.Equal(new TimeOnly(13, 30), update.NewBroadcastTime);
    }

    // add-score-history-graph design D4: the detector also feeds the score
    // recorder, outside the HadFullDetail gate that silences everything above.

    [Fact]
    public async Task AFullDetailWriteRecordsTheScoreItWrote()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata
        {
            Id = 1, Title = "Anime 1", LastSyncedAt = DateTimeOffset.UtcNow.AddDays(-60),
            AiringStatus = "currently_airing", MalScore = 8.10,
        };
        db.AnimeMetadata.Add(anime);
        await db.SaveChangesAsync();

        var detector = CreateDetector(db, new SeriesBuildTrigger());
        var before = detector.Snapshot(anime);
        var now = DateTimeOffset.UtcNow;
        new MalAnimeNode { Id = 1, Title = "Anime 1", Status = "currently_airing", Mean = 8.25 }.ApplyTo(anime, now);

        await detector.RecordAsync(anime, before, now);
        await db.SaveChangesAsync();

        var record = Assert.Single(await db.AnimeScoreRecords.AsNoTracking().ToListAsync());
        Assert.Equal((1, 8.25, now), (record.AnimeId, record.Score, record.DetectedAt));
    }

    [Fact]
    public async Task AListingWriteRecordsTheScoreForAnAnimeWhoseStatusIsAlreadyKnown()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata
        {
            Id = 1, Title = "Anime 1", LastSyncedAt = DateTimeOffset.UtcNow.AddDays(-60),
            AiringStatus = "currently_airing", MalScore = 8.10,
        };
        db.AnimeMetadata.Add(anime);
        await db.SaveChangesAsync();

        var detector = CreateDetector(db, new SeriesBuildTrigger());
        var before = detector.SnapshotListing(anime);
        var now = DateTimeOffset.UtcNow;
        new MalAnimeNode { Id = 1, Title = "Anime 1", Mean = 8.18 }.ApplyLeanTo(anime, now); // a listing never carries a status

        await detector.RecordAsync(anime, before, now);
        await db.SaveChangesAsync();

        Assert.Equal(8.18, Assert.Single(await db.AnimeScoreRecords.AsNoTracking().ToListAsync()).Score);
    }

    [Fact]
    public async Task AListingWriteForAnAnimeNeverFullyFetchedRecordsNothing()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1", LastSyncedAt = default, MalScore = 7.5 }; // listing-only: no status
        db.AnimeMetadata.Add(anime);
        await db.SaveChangesAsync();

        var detector = CreateDetector(db, new SeriesBuildTrigger());
        var before = detector.SnapshotListing(anime);
        var now = DateTimeOffset.UtcNow;
        new MalAnimeNode { Id = 1, Title = "Anime 1", Mean = 7.6 }.ApplyLeanTo(anime, now);

        await detector.RecordAsync(anime, before, now);
        await db.SaveChangesAsync();

        Assert.Empty(await db.AnimeScoreRecords.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task AFirstFullFetchOfAListingOnlyRowStartsItsHistory()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1", LastSyncedAt = default, MalScore = 7.5 };
        db.AnimeMetadata.Add(anime);
        await db.SaveChangesAsync();

        var detector = CreateDetector(db, new SeriesBuildTrigger());
        var before = detector.Snapshot(anime);
        Assert.False(before.HadFullDetail); // the gate that silences the updates feed for this write
        var now = DateTimeOffset.UtcNow;
        new MalAnimeNode { Id = 1, Title = "Anime 1", Status = "finished_airing", Mean = 7.6 }.ApplyTo(anime, now);

        await detector.RecordAsync(anime, before, now);
        await db.SaveChangesAsync();

        Assert.Empty(await db.AnimeUpdates.AsNoTracking().ToListAsync());
        Assert.Equal(7.6, Assert.Single(await db.AnimeScoreRecords.AsNoTracking().ToListAsync()).Score);
    }
}
