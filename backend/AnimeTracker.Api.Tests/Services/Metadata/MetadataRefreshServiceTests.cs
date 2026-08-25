using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Mal.Dto;
using AnimeTracker.Api.Services.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimeTracker.Api.Tests.Services.Metadata;

// MetadataRefreshService (metadata-refresh spec, tasks 2.1-2.7/3.1-3.4/8.5/8.12):
// the tiered nightly job is now a full-detail refresh keyed on LastSyncedAt,
// and both it and the on-demand path diff relations against their
// pre-refresh snapshot to write RelationDiscovery events.
public class MetadataRefreshServiceTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

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
        var service = new MetadataRefreshService(db, malClient, NullLogger<MetadataRefreshService>.Instance);

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
        var service = new MetadataRefreshService(db, malClient, NullLogger<MetadataRefreshService>.Instance);

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
        var service = new MetadataRefreshService(db, malClient, NullLogger<MetadataRefreshService>.Instance);

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
        var service = new MetadataRefreshService(db, malClient, NullLogger<MetadataRefreshService>.Instance);

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
        var service = new MetadataRefreshService(db, malClient, NullLogger<MetadataRefreshService>.Instance);

        await service.RefreshStaleBatchAsync(10);

        Assert.Empty(await db.RelationDiscoveries.ToListAsync());
    }

    [Fact]
    public async Task RefreshOneAsync_FirstTimeCacheWritesNoDiscoveries()
    {
        using var db = CreateDb();
        var malClient = new FakeMalClient(new Dictionary<int, MalAnimeNode> { [1] = DetailNode(1, (2, "sequel"), (3, "side_story")) });
        var service = new MetadataRefreshService(db, malClient, NullLogger<MetadataRefreshService>.Instance);

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
        var service = new MetadataRefreshService(db, malClient, NullLogger<MetadataRefreshService>.Instance);

        await service.RefreshStaleBatchAsync(10);

        Assert.Empty(await db.RelationDiscoveries.ToListAsync());
        var stored = await db.AnimeMetadata.Include(a => a.RelatedAnime).AsNoTracking().SingleAsync(a => a.Id == 1);
        Assert.Empty(stored.RelatedAnime);
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
