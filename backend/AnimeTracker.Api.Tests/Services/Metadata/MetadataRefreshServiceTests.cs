using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Mal.Dto;
using AnimeTracker.Api.Services.Metadata;
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

    private static MetadataRefreshService CreateService(AnimeTrackerDbContext db, IMalClient malClient) =>
        new(db, malClient, new AnimeMetadataChangeDetector(db, new AnimeUpdateRecorder(db)), NullLogger<MetadataRefreshService>.Instance);

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
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1", LastSyncedAt = default };
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
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1", LastSyncedAt = default };
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
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1", LastSyncedAt = default };
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
        string? broadcastDay = null, string? broadcastTime = null) => new()
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
    };

    [Fact]
    public async Task RefreshOneAsync_UnknownToKnownEpisodeCountRecordsRelease()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Anime 1", AiringStatus = "currently_airing" });
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
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Anime 1", AiringStatus = "not_yet_aired", LastSyncedAt = default });
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
        });
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
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Anime 1", AiringStatus = "currently_airing" });
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
        });
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
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Anime 1", AiringStatus = "not_yet_aired" });
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
    }
}
