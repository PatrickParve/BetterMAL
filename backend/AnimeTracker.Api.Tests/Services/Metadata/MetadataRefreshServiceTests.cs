using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Mal.Dto;
using AnimeTracker.Api.Services.Metadata;
using AnimeTracker.Api.Services.Relations;
using AnimeTracker.Api.Services.Series;
using AnimeTracker.Api.Services.Updates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimeTracker.Api.Tests.Services.Metadata;

// MetadataRefreshService (metadata-refresh spec, tasks 2.1-2.7/3.1-3.4/8.5/8.12;
// anime-updates spec, tasks 2.1-2.5): the tiered nightly job is now a
// full-detail refresh keyed on LastSyncedAt, and both it and the on-demand
// path diff relations against their pre-refresh snapshot to write
// RelationDiscovery events, and diff episode count/premiere date/broadcast
// slot against their own pre-refresh snapshot to write AnimeUpdate events.
public class MetadataRefreshServiceTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static MetadataRefreshService CreateService(
        AnimeTrackerDbContext db, IMalClient malClient, ISeriesBuildTrigger? seriesBuildTrigger = null) =>
        new(db, malClient,
            new AnimeMetadataChangeDetector(db, new AnimeUpdateRecorder(db, new AnimeUpdateRelevance(db, new RelationResolver(db))), seriesBuildTrigger ?? new SeriesBuildTrigger()),
            NullLogger<MetadataRefreshService>.Instance);

    // A row that stands for an anime already fully fetched once, long enough
    // ago to still be due on every tier of RefreshTiers' ladder (the longest,
    // StaleTtl, is 28 days). Detection reads LastSyncedAt to decide whether
    // there was ever a prior observation to diff against (anime-updates: "the
    // first time SHALL mean the anime's first full-detail fetch"), so a
    // fixture left at default is a never-fetched row and records nothing —
    // which is a different test from the one each of these is making.
    private static readonly DateTimeOffset FullyFetchedLongAgo = DateTimeOffset.UtcNow - TimeSpan.FromDays(60);

    private static MalAnimeNode DetailNode(int id, params (int RelatedId, string RelationType)[] relations) => new()
    {
        Id = id,
        Title = $"Anime {id}",
        MediaType = "tv",
        Status = "finished_airing",
        RelatedAnime = relations
            .Select(r => new MalRelatedAnimeEdge
            {
                RelationType = r.RelationType,
                Node = new MalAnimeNode { Id = r.RelatedId, Title = $"Anime {r.RelatedId}" },
            })
            .ToList(),
    };

    // --- 8.5: refresh ---

    [Fact]
    public async Task RefreshStaleBatchAsync_UpdatesRelationsAndAdvancesLastSyncedAt()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Anime 1", LastSyncedAt = default });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1 });
        await db.SaveChangesAsync();

        var malClient = new FakeMalClient(new Dictionary<int, MalAnimeNode> { [1] = DetailNode(1, (2, "sequel")) });
        var service = CreateService(db, malClient);

        var refreshedCount = await service.RefreshStaleBatchAsync(10);

        Assert.Equal(1, refreshedCount);
        var anime = await db.AnimeMetadata.Include(a => a.RelatedAnime).AsNoTracking().SingleAsync(a => a.Id == 1);
        Assert.NotEqual(default, anime.LastSyncedAt);
        var relation = Assert.Single(anime.RelatedAnime);
        Assert.Equal(2, relation.RelatedAnimeId);
    }

    [Fact]
    public async Task RefreshStaleBatchAsync_AnimeWithinItsTierIsSkipped()
    {
        using var db = CreateDb();
        var now = DateTimeOffset.UtcNow;
        db.AnimeMetadata.Add(new AnimeMetadata
        {
            Id = 1,
            Title = "Anime 1",
            AiringStatus = "currently_airing",
            LastSyncedAt = now - TimeSpan.FromHours(1), // well inside the 1-day airing tier
        });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1 });
        await db.SaveChangesAsync();

        var malClient = new FakeMalClient(new Dictionary<int, MalAnimeNode>());
        var service = CreateService(db, malClient);

        var refreshedCount = await service.RefreshStaleBatchAsync(10);

        Assert.Equal(0, refreshedCount);
        Assert.Empty(malClient.Calls);
    }

    [Fact]
    public async Task RefreshStaleBatchAsync_LeanListingRefreshDoesNotPostponeTieredRefresh()
    {
        using var db = CreateDb();
        var now = DateTimeOffset.UtcNow;
        // Never full-detail fetched, but a lean listing browse (season/top-anime)
        // just bumped LastScoreSyncedAt. The due query must key on
        // LastSyncedAt, not LastScoreSyncedAt, or this row would look falsely
        // fresh and never get a full-detail refresh.
        db.AnimeMetadata.Add(new AnimeMetadata
        {
            Id = 1,
            Title = "Anime 1",
            LastSyncedAt = default,
            LastScoreSyncedAt = now,
        });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1 });
        await db.SaveChangesAsync();

        var malClient = new FakeMalClient(new Dictionary<int, MalAnimeNode> { [1] = DetailNode(1) });
        var service = CreateService(db, malClient);

        var refreshedCount = await service.RefreshStaleBatchAsync(10);

        Assert.Equal(1, refreshedCount);
        Assert.Contains(1, malClient.Calls);
    }

    // --- 8.12: relation discovery events ---

    [Fact]
    public async Task RefreshStaleBatchAsync_NewlyAppearingEdgeWritesOneDiscoveryRow()
    {
        using var db = CreateDb();
        // Carries an edge, so it stands for an already-fully-fetched anime:
        // only ApplyTo writes RelatedAnime, and it stamps LastSyncedAt with it.
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1", LastSyncedAt = FullyFetchedLongAgo };
        anime.RelatedAnime.Add(new AnimeRelatedAnime { AnimeId = 1, RelatedAnimeId = 2, RelationType = "sequel", Title = "Anime 2" });
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1 });
        await db.SaveChangesAsync();

        // MAL now reports the existing edge to 2 plus a new one to 3.
        var malClient = new FakeMalClient(new Dictionary<int, MalAnimeNode> { [1] = DetailNode(1, (2, "sequel"), (3, "sequel")) });
        var service = CreateService(db, malClient);

        await service.RefreshStaleBatchAsync(10);

        var discovery = Assert.Single(await db.RelationDiscoveries.AsNoTracking().ToListAsync());
        Assert.Equal(1, discovery.AnimeId);
        Assert.Equal(3, discovery.RelatedAnimeId);
        Assert.Equal("sequel", discovery.RelationType);
    }

    [Fact]
    public async Task RefreshStaleBatchAsync_UnchangedRelationSetWritesNoDiscoveries()
    {
        using var db = CreateDb();
        // Carries an edge, so it stands for an already-fully-fetched anime:
        // only ApplyTo writes RelatedAnime, and it stamps LastSyncedAt with it.
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1", LastSyncedAt = FullyFetchedLongAgo };
        anime.RelatedAnime.Add(new AnimeRelatedAnime { AnimeId = 1, RelatedAnimeId = 2, RelationType = "sequel", Title = "Anime 2" });
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1 });
        await db.SaveChangesAsync();

        var malClient = new FakeMalClient(new Dictionary<int, MalAnimeNode> { [1] = DetailNode(1, (2, "sequel")) });
        var service = CreateService(db, malClient);

        await service.RefreshStaleBatchAsync(10);

        Assert.Empty(await db.RelationDiscoveries.ToListAsync());
    }

    [Fact]
    public async Task RefreshOneAsync_FirstTimeCacheWritesNoDiscoveries()
    {
        using var db = CreateDb();
        var malClient = new FakeMalClient(new Dictionary<int, MalAnimeNode> { [1] = DetailNode(1, (2, "sequel"), (3, "side_story")) });
        var service = CreateService(db, malClient);

        await service.RefreshOneAsync(1);

        Assert.Empty(await db.RelationDiscoveries.ToListAsync());
    }

    [Fact]
    public async Task RefreshStaleBatchAsync_RemovedEdgeWritesNoDiscoveryAndIsDropped()
    {
        using var db = CreateDb();
        // Carries an edge, so it stands for an already-fully-fetched anime:
        // only ApplyTo writes RelatedAnime, and it stamps LastSyncedAt with it.
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1", LastSyncedAt = FullyFetchedLongAgo };
        anime.RelatedAnime.Add(new AnimeRelatedAnime { AnimeId = 1, RelatedAnimeId = 2, RelationType = "sequel", Title = "Anime 2" });
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1 });
        await db.SaveChangesAsync();

        // MAL no longer reports the edge to 2 at all.
        var malClient = new FakeMalClient(new Dictionary<int, MalAnimeNode> { [1] = DetailNode(1) });
        var service = CreateService(db, malClient);

        await service.RefreshStaleBatchAsync(10);

        Assert.Empty(await db.RelationDiscoveries.ToListAsync());
        var stored = await db.AnimeMetadata.Include(a => a.RelatedAnime).AsNoTracking().SingleAsync(a => a.Id == 1);
        Assert.Empty(stored.RelatedAnime);
    }

    // --- anime-updates 2.5: field-update detection ---

    private static MalAnimeNode FieldNode(
        int id, string status, int? numEpisodes = null, string? startDate = null,
        string? broadcastDay = null, string? broadcastTime = null,
        (int RelatedId, string RelationType)[]? relations = null) => new()
    {
        Id = id,
        Title = $"Anime {id}",
        MediaType = "tv",
        Status = status,
        NumEpisodes = numEpisodes,
        StartDate = startDate,
        Broadcast = broadcastDay is null && broadcastTime is null
            ? null
            : new MalBroadcast { DayOfTheWeek = broadcastDay, StartTime = broadcastTime },
        RelatedAnime = relations?
            .Select(r => new MalRelatedAnimeEdge
            {
                RelationType = r.RelationType,
                Node = new MalAnimeNode { Id = r.RelatedId, Title = $"Anime {r.RelatedId}" },
            })
            .ToList(),
    };

    // A row in exactly the state the three lean paths leave it in
    // (ReconciliationService, TopAnimeService, SeasonBrowseService): title,
    // score and rank written, LastScoreSyncedAt stamped, and every
    // detail-only field — airing status, premiere date, broadcast slot — and
    // LastSyncedAt untouched. Built through the real mapper rather than by
    // hand, so it cannot drift from what ApplyLeanTo actually writes.
    private static AnimeMetadata LeanCachedAnime(int id, DateTimeOffset now) =>
        new MalAnimeNode { Id = id, Title = $"Anime {id}", MediaType = "tv" }.ToLeanAnimeMetadata(now);

    [Fact]
    public async Task RefreshOneAsync_UnknownToKnownEpisodeCountRecordsRelease()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Anime 1", AiringStatus = "currently_airing", LastSyncedAt = FullyFetchedLongAgo });
        // anime-updates "Nothing is recorded for an anime outside my list and
        // its direct relations" (scope-updates-to-my-list tasks 6.1): without
        // a non-Dropped entry, the relevance gate would silence this release.
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Status = WatchStatus.Watching });
        await db.SaveChangesAsync();

        var responses = new Dictionary<int, MalAnimeNode> { [1] = FieldNode(1, "currently_airing", numEpisodes: 12) };
        var service = CreateService(db, new FakeMalClient(responses));

        await service.RefreshOneAsync(1);

        var update = Assert.Single(await db.AnimeUpdates.AsNoTracking().ToListAsync());
        Assert.Equal(AnimeUpdateKinds.EpisodeCountReleased, update.Kinds);
    }

    [Fact]
    public async Task RefreshStaleBatchAsync_UnknownToKnownStartDateRecordsRelease()
    {
        using var db = CreateDb();
        // Unaired with no known premiere date is the 3-day tier, so a
        // 60-day-old stamp keeps this row selected by the batch's due query
        // while still standing for an anime that has been fully fetched
        // before — which is what makes the reveal below a reveal.
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Anime 1", AiringStatus = "not_yet_aired", LastSyncedAt = FullyFetchedLongAgo });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1 });
        await db.SaveChangesAsync();

        var responses = new Dictionary<int, MalAnimeNode> { [1] = FieldNode(1, "not_yet_aired", startDate: "2024-10-05") };
        var service = CreateService(db, new FakeMalClient(responses));

        await service.RefreshStaleBatchAsync(10);

        var update = Assert.Single(await db.AnimeUpdates.AsNoTracking().ToListAsync());
        Assert.Equal(AnimeUpdateKinds.StartDateReleased, update.Kinds);
    }

    [Fact]
    public async Task RefreshOneAsync_KnownToDifferentKnownRecordsDateAndSlotChangeButNotEpisodeCount()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata
        {
            Id = 1,
            Title = "Anime 1",
            AiringStatus = "not_yet_aired",
            TotalEpisodes = 12,
            AiredFrom = new DateOnly(2024, 10, 5),
            BroadcastDayOfWeek = "mondays",
            BroadcastTime = new TimeOnly(23, 30),
            LastSyncedAt = FullyFetchedLongAgo,
        });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Status = WatchStatus.Watching });
        await db.SaveChangesAsync();

        var responses = new Dictionary<int, MalAnimeNode>
        {
            [1] = FieldNode(1, "not_yet_aired", numEpisodes: 24, startDate: "2024-10-12", broadcastDay: "saturdays", broadcastTime: "01:00"),
        };
        var service = CreateService(db, new FakeMalClient(responses));

        await service.RefreshOneAsync(1);

        var update = Assert.Single(await db.AnimeUpdates.AsNoTracking().ToListAsync());
        Assert.Equal(AnimeUpdateKinds.StartDateChanged | AnimeUpdateKinds.BroadcastSlotChanged, update.Kinds);
        Assert.Equal(new DateOnly(2024, 10, 5), update.PreviousStartDate);
        Assert.Equal("mondays", update.PreviousBroadcastDayOfWeek);
        Assert.Equal(new TimeOnly(23, 30), update.PreviousBroadcastTime);
    }

    [Fact]
    public async Task RefreshOneAsync_KnownToNullRecordsNothing()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata
        {
            Id = 1,
            Title = "Anime 1",
            AiringStatus = "not_yet_aired",
            TotalEpisodes = 12,
            AiredFrom = new DateOnly(2024, 10, 5),
            LastSyncedAt = FullyFetchedLongAgo,
        });
        await db.SaveChangesAsync();

        var responses = new Dictionary<int, MalAnimeNode> { [1] = FieldNode(1, "not_yet_aired") };
        var service = CreateService(db, new FakeMalClient(responses));

        await service.RefreshOneAsync(1);

        Assert.Empty(await db.AnimeUpdates.ToListAsync());
    }

    [Fact]
    public async Task RefreshOneAsync_ReleasedThenUnknownThenKnownAgainRecordsOnlyOnce()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Anime 1", AiringStatus = "currently_airing", LastSyncedAt = FullyFetchedLongAgo });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Status = WatchStatus.Watching });
        await db.SaveChangesAsync();

        var responses = new Dictionary<int, MalAnimeNode> { [1] = FieldNode(1, "currently_airing", numEpisodes: 12) };
        var service = CreateService(db, new FakeMalClient(responses));
        await service.RefreshOneAsync(1); // null -> 12: records a release

        responses[1] = FieldNode(1, "currently_airing"); // 12 -> null: flapping, records nothing
        await service.RefreshOneAsync(1);

        responses[1] = FieldNode(1, "currently_airing", numEpisodes: 13); // null -> 13: already recorded once, forever
        await service.RefreshOneAsync(1);

        var updates = await db.AnimeUpdates.AsNoTracking().ToListAsync();
        var update = Assert.Single(updates);
        Assert.Equal(AnimeUpdateKinds.EpisodeCountReleased, update.Kinds);
    }

    [Fact]
    public async Task RefreshOneAsync_TwoSuccessiveDateMovesRecordTwoChanges()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata
        {
            Id = 1,
            Title = "Anime 1",
            AiringStatus = "not_yet_aired",
            AiredFrom = new DateOnly(2024, 10, 5),
            LastSyncedAt = FullyFetchedLongAgo,
        });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Status = WatchStatus.Watching });
        await db.SaveChangesAsync();

        var responses = new Dictionary<int, MalAnimeNode> { [1] = FieldNode(1, "not_yet_aired", startDate: "2024-10-12") };
        var service = CreateService(db, new FakeMalClient(responses));
        await service.RefreshOneAsync(1); // 5 Oct -> 12 Oct

        responses[1] = FieldNode(1, "not_yet_aired", startDate: "2024-10-19");
        await service.RefreshOneAsync(1); // 12 Oct -> 19 Oct

        var updates = await db.AnimeUpdates.AsNoTracking().ToListAsync();
        Assert.Equal(2, updates.Count);
        Assert.All(updates, u => Assert.Equal(AnimeUpdateKinds.StartDateChanged, u.Kinds));
    }

    [Fact]
    public async Task RefreshOneAsync_CountAndDateReleasedTogetherProduceOneRowWithBothKinds()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Anime 1", AiringStatus = "not_yet_aired", LastSyncedAt = FullyFetchedLongAgo });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Status = WatchStatus.Watching });
        await db.SaveChangesAsync();

        var responses = new Dictionary<int, MalAnimeNode>
        {
            [1] = FieldNode(1, "not_yet_aired", numEpisodes: 12, startDate: "2024-10-05"),
        };
        var service = CreateService(db, new FakeMalClient(responses));

        await service.RefreshOneAsync(1);

        var update = Assert.Single(await db.AnimeUpdates.AsNoTracking().ToListAsync());
        Assert.Equal(AnimeUpdateKinds.EpisodeCountReleased | AnimeUpdateKinds.StartDateReleased, update.Kinds);
    }

    [Fact]
    public async Task RefreshOneAsync_FinishedAiringAnimeRecordsNoScheduleChange()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata
        {
            Id = 1,
            Title = "Anime 1",
            AiringStatus = "finished_airing",
            AiredFrom = new DateOnly(2011, 4, 1),
            LastSyncedAt = FullyFetchedLongAgo,
        });
        await db.SaveChangesAsync();

        var responses = new Dictionary<int, MalAnimeNode> { [1] = FieldNode(1, "finished_airing", startDate: "2011-04-08") };
        var service = CreateService(db, new FakeMalClient(responses));

        await service.RefreshOneAsync(1);

        Assert.Empty(await db.AnimeUpdates.ToListAsync());
    }

    [Fact]
    public async Task RefreshOneAsync_FirstEverFetchRecordsNothing()
    {
        using var db = CreateDb();
        var responses = new Dictionary<int, MalAnimeNode>
        {
            [1] = FieldNode(1, "not_yet_aired", numEpisodes: 12, startDate: "2024-10-05"),
        };
        var service = CreateService(db, new FakeMalClient(responses));

        await service.RefreshOneAsync(1);

        Assert.Empty(await db.AnimeUpdates.ToListAsync());
    }

    // --- fix-false-updates-on-lean-rows: a lean row is not a prior observation ---

    // The reported bug (Clannad: After Story - Another World, Kyou Chapter).
    // Reconciliation, Top-Anime and season browsing all cache an anime
    // leanly, so a row exists carrying no premiere date and no episode count
    // — but that null was never an observation of anything, and diffing
    // against it reports the cache catching up as though MAL had just
    // revealed the date. anime-updates: "the first time SHALL mean the
    // anime's first full-detail fetch, not the first time a row existed".
    //
    // Deliberately currently_airing rather than the reported anime's
    // finished_airing, so nothing but the never-fully-fetched rule can
    // explain the silence — the finished-airing gate is not doing the work.
    [Fact]
    public async Task RefreshOneAsync_FirstFullFetchOfALeanlyCachedAnimeRecordsNothing()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(LeanCachedAnime(1, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();

        var responses = new Dictionary<int, MalAnimeNode>
        {
            [1] = FieldNode(1, "currently_airing", numEpisodes: 24, startDate: "2024-10-05"),
        };
        var service = CreateService(db, new FakeMalClient(responses));

        await service.RefreshOneAsync(1);

        Assert.Empty(await db.AnimeUpdates.ToListAsync());

        // The fetch itself still lands — the row now holds the very values it
        // recorded nothing about, which is what stops a later diff finding them.
        var stored = await db.AnimeMetadata.AsNoTracking().SingleAsync(a => a.Id == 1);
        Assert.Equal(24, stored.TotalEpisodes);
        Assert.Equal(new DateOnly(2024, 10, 5), stored.AiredFrom);
        Assert.NotEqual(default, stored.LastSyncedAt);
    }

    // scope-updates-to-my-list design.md D4, reversing archived decision D3:
    // a first full-detail fetch's whole relation set arriving at once is the
    // system finally looking, not links newly appearing, so it now discovers
    // nothing. The series-build enqueue is unaffected — narrowing what gets
    // *recorded* must not narrow what gets *compared* (spec: "The comparison
    // that finds newly-appeared edges SHALL still run for every anime").
    [Fact]
    public async Task RefreshOneAsync_FirstFullFetchOfALeanlyCachedAnimeDiscoversNothingButStillEnqueuesTheBuild()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(LeanCachedAnime(1, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();

        var trigger = new SeriesBuildTrigger();
        var responses = new Dictionary<int, MalAnimeNode>
        {
            [1] = FieldNode(1, "currently_airing", numEpisodes: 24, startDate: "2024-10-05",
                relations: [(2, "sequel"), (3, "side_story")]),
        };
        var service = CreateService(db, new FakeMalClient(responses), trigger);

        await service.RefreshOneAsync(1);

        Assert.Empty(await db.RelationDiscoveries.ToListAsync());

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        Assert.Equal(1, await trigger.WaitAsync(cts.Token));

        Assert.Empty(await db.AnimeUpdates.ToListAsync());
    }

    // Design D2 / anime-updates "A premiere date released is news only while
    // an anime has not finished airing": a premiere date arriving for a show
    // that finished years ago is MAL's records reaching us, not a date anyone
    // is waiting for. The gate reads the status this same fetch just wrote.
    [Theory]
    [InlineData("finished_airing", false)]
    [InlineData("not_yet_aired", true)]
    [InlineData("currently_airing", true)]
    public async Task RefreshOneAsync_UnknownToKnownStartDateIsGatedOnAiringStatus(string status, bool expectRecorded)
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Anime 1", AiringStatus = status, LastSyncedAt = FullyFetchedLongAgo });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Status = WatchStatus.Watching });
        await db.SaveChangesAsync();

        var responses = new Dictionary<int, MalAnimeNode> { [1] = FieldNode(1, status, startDate: "2008-06-15") };
        var service = CreateService(db, new FakeMalClient(responses));

        await service.RefreshOneAsync(1);

        if (!expectRecorded)
        {
            Assert.Empty(await db.AnimeUpdates.ToListAsync());
            return;
        }

        var update = Assert.Single(await db.AnimeUpdates.AsNoTracking().ToListAsync());
        Assert.Equal(AnimeUpdateKinds.StartDateReleased, update.Kinds);
    }

    // The deliberate asymmetry in the gate above (design D2), and the test
    // that should stop a future reader "tidying" EpisodeCountReleased into
    // the mask alongside it: a premiere date for a finished show reports an
    // event already in the past that the anime's own record displays, but an
    // episode count is a fact about what there is to watch — and
    // anime-updates specifies the AniList-supplied total precisely for the
    // finished anime MyAnimeList publishes no count for.
    [Fact]
    public async Task RefreshOneAsync_FinishedAiringAnimeStillRecordsAnEpisodeCountRelease()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata
        {
            Id = 1,
            Title = "Anime 1",
            AiringStatus = "finished_airing",
            AiredFrom = new DateOnly(2008, 6, 15),
            LastSyncedAt = FullyFetchedLongAgo,
        });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Status = WatchStatus.Watching });
        await db.SaveChangesAsync();

        var responses = new Dictionary<int, MalAnimeNode>
        {
            [1] = FieldNode(1, "finished_airing", numEpisodes: 1, startDate: "2008-06-15"),
        };
        var service = CreateService(db, new FakeMalClient(responses));

        await service.RefreshOneAsync(1);

        var update = Assert.Single(await db.AnimeUpdates.AsNoTracking().ToListAsync());
        Assert.Equal(AnimeUpdateKinds.EpisodeCountReleased, update.Kinds);
    }

    // --- 5.4/5.5: the adjacent set widens RefreshStaleBatchAsync's candidates ---

    [Fact]
    public async Task RefreshStaleBatchAsync_UnairedAdjacentAnimeIsSelected()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Announced sequel", AiringStatus = "not_yet_aired" });
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 2, Title = "My show", AiringStatus = "currently_airing", LastSyncedAt = DateTimeOffset.UtcNow });
        db.AnimeRelatedAnime.Add(new AnimeRelatedAnime { AnimeId = 1, RelatedAnimeId = 2, RelationType = "sequel" });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 2, Status = WatchStatus.Watching });
        await db.SaveChangesAsync();

        var malClient = new FakeMalClient(new Dictionary<int, MalAnimeNode> { [1] = FieldNode(1, "not_yet_aired") });
        var service = CreateService(db, malClient);

        var refreshedCount = await service.RefreshStaleBatchAsync(10);

        Assert.Equal(1, refreshedCount);
        Assert.Contains(1, malClient.Calls);
    }

    [Fact]
    public async Task RefreshStaleBatchAsync_AdjacentAnimeNotSelectedOnceAiring()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Now airing", AiringStatus = "currently_airing" });
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 2, Title = "My show", AiringStatus = "currently_airing", LastSyncedAt = DateTimeOffset.UtcNow });
        db.AnimeRelatedAnime.Add(new AnimeRelatedAnime { AnimeId = 1, RelatedAnimeId = 2, RelationType = "sequel" });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 2, Status = WatchStatus.Watching });
        await db.SaveChangesAsync();

        var malClient = new FakeMalClient(new Dictionary<int, MalAnimeNode>());
        var service = CreateService(db, malClient);

        var refreshedCount = await service.RefreshStaleBatchAsync(10);

        Assert.Equal(0, refreshedCount);
        Assert.Empty(malClient.Calls);
    }

    [Fact]
    public async Task RefreshStaleBatchAsync_AdjacentAnimeNotSelectedWhenOnlyAffiliateIsDropped()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Announced sequel", AiringStatus = "not_yet_aired" });
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 2, Title = "My dropped show", AiringStatus = "currently_airing", LastSyncedAt = DateTimeOffset.UtcNow });
        db.AnimeRelatedAnime.Add(new AnimeRelatedAnime { AnimeId = 1, RelatedAnimeId = 2, RelationType = "sequel" });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 2, Status = WatchStatus.Dropped });
        await db.SaveChangesAsync();

        var malClient = new FakeMalClient(new Dictionary<int, MalAnimeNode>());
        var service = CreateService(db, malClient);

        var refreshedCount = await service.RefreshStaleBatchAsync(10);

        Assert.Equal(0, refreshedCount);
        Assert.Empty(malClient.Calls);
    }

    [Fact]
    public async Task RefreshStaleBatchAsync_AdjacentAnimeNotSelectedOverCharacterEdge()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Shares a cast", AiringStatus = "not_yet_aired" });
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 2, Title = "My show", AiringStatus = "currently_airing", LastSyncedAt = DateTimeOffset.UtcNow });
        db.AnimeRelatedAnime.Add(new AnimeRelatedAnime { AnimeId = 1, RelatedAnimeId = 2, RelationType = "character" });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 2, Status = WatchStatus.Watching });
        await db.SaveChangesAsync();

        var malClient = new FakeMalClient(new Dictionary<int, MalAnimeNode>());
        var service = CreateService(db, malClient);

        var refreshedCount = await service.RefreshStaleBatchAsync(10);

        Assert.Equal(0, refreshedCount);
        Assert.Empty(malClient.Calls);
    }

    [Fact]
    public async Task RefreshStaleBatchAsync_FinishedNonListAnimeNeverSelected()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Old finished show", AiringStatus = "finished_airing" });
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 2, Title = "My show", AiringStatus = "currently_airing", LastSyncedAt = DateTimeOffset.UtcNow });
        db.AnimeRelatedAnime.Add(new AnimeRelatedAnime { AnimeId = 1, RelatedAnimeId = 2, RelationType = "sequel" });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 2, Status = WatchStatus.Watching });
        await db.SaveChangesAsync();

        var malClient = new FakeMalClient(new Dictionary<int, MalAnimeNode>());
        var service = CreateService(db, malClient);

        var refreshedCount = await service.RefreshStaleBatchAsync(10);

        Assert.Equal(0, refreshedCount);
        Assert.Empty(malClient.Calls);
    }

    private sealed class FakeMalClient(Dictionary<int, MalAnimeNode> responses) : IMalClient
    {
        public List<int> Calls { get; } = [];

        public Task<MalAnimeNode> GetAnimeDetailsAsync(int animeId, IReadOnlyCollection<string>? fields = null, CancellationToken ct = default)
        {
            Calls.Add(animeId);
            return Task.FromResult(responses[animeId]);
        }

        public Task<MalPagedResponse<MalAnimeListEdge>> SearchAnimeAsync(string query, int limit = 5, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<MalPagedResponse<MalAnimeListEdge>> GetSeasonAsync(int year, string season, int limit = 100, int offset = 0, string? sort = null, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<List<MalAnimeListEdge>?> GetFullSeasonAsync(int year, string season, string? sort = null, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<MalPagedResponse<MalAnimeListEdge>> GetRankingAsync(string rankingType = "all", int limit = 100, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<MalPagedResponse<MalUserAnimeListEdge>> GetUserAnimeListAsync(string? status = null, int limit = 100, int offset = 0, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<List<MalUserAnimeListEdge>> GetFullUserAnimeListAsync(CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<MalListStatus> UpdateMyListStatusAsync(int animeId, MalListStatusUpdate update, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task DeleteMyListStatusAsync(int animeId, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<MalListStatus?> GetMyListStatusAsync(int animeId, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }
}
