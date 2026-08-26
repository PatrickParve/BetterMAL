using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Metadata;
using AnimeTracker.Api.Services.Updates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimeTracker.Api.Tests.Services.Updates;

// IAnnouncementResolutionService (anime-updates spec, tasks 3.1-3.5): resolves
// newly-discovered relation edges into cached anime and, where warranted, an
// Announced update, marking every considered discovery as processed.
public class AnnouncementResolutionServiceTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static AnnouncementResolutionService CreateService(AnimeTrackerDbContext db, IMetadataRefreshService metadataRefresh) =>
        new(db, metadataRefresh, new AnimeUpdateRecorder(db), NullLogger<AnnouncementResolutionService>.Instance);

    private static RelationDiscovery Discovery(int ownerAnimeId, int relatedAnimeId, DateTimeOffset discoveredAt) => new()
    {
        AnimeId = ownerAnimeId,
        RelatedAnimeId = relatedAnimeId,
        RelationType = "sequel",
        DiscoveredAt = discoveredAt,
    };

    [Fact]
    public async Task AnUnairedRelatedAnimeAnnounces()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Owner" });
        db.RelationDiscoveries.Add(Discovery(1, 2, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();

        var metadataRefresh = new FakeMetadataRefreshService(db, new()
        {
            [2] = new AnimeMetadata { Id = 2, Title = "Sequel", AiringStatus = "not_yet_aired" },
        });
        var service = CreateService(db, metadataRefresh);

        var malCalls = await service.ResolveAsync(10);

        Assert.Equal(1, malCalls);
        var update = Assert.Single(await db.AnimeUpdates.AsNoTracking().ToListAsync());
        Assert.Equal(2, update.AnimeId);
        Assert.Equal(AnimeUpdateKinds.Announced, update.Kinds);
        var discovery = await db.RelationDiscoveries.AsNoTracking().SingleAsync();
        Assert.NotNull(discovery.ProcessedAt);
    }

    [Fact]
    public async Task AFinishedRelatedAnimeDoesNotAnnounceButIsMarkedProcessed()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Owner" });
        db.RelationDiscoveries.Add(Discovery(1, 3, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();

        var metadataRefresh = new FakeMetadataRefreshService(db, new()
        {
            [3] = new AnimeMetadata { Id = 3, Title = "Old OVA", AiringStatus = "finished_airing" },
        });
        var service = CreateService(db, metadataRefresh);

        await service.ResolveAsync(10);

        Assert.Empty(await db.AnimeUpdates.ToListAsync());
        var discovery = await db.RelationDiscoveries.AsNoTracking().SingleAsync();
        Assert.NotNull(discovery.ProcessedAt);
    }

    [Fact]
    public async Task AFailedFetchLeavesTheDiscoveryUnprocessed()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Owner" });
        db.RelationDiscoveries.Add(Discovery(1, 4, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();

        // 4 is absent from the resolved-anime map, so the fake fetch throws.
        var metadataRefresh = new FakeMetadataRefreshService(db, new());
        var service = CreateService(db, metadataRefresh);

        var malCalls = await service.ResolveAsync(10);

        Assert.Equal(0, malCalls);
        Assert.Empty(await db.AnimeUpdates.ToListAsync());
        var discovery = await db.RelationDiscoveries.AsNoTracking().SingleAsync();
        Assert.Null(discovery.ProcessedAt);
    }

    [Fact]
    public async Task TwoDiscoveriesOfOneAnimeProduceASingleAnnouncement()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 10, Title = "Owner A" });
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 11, Title = "Owner B" });
        var now = DateTimeOffset.UtcNow;
        db.RelationDiscoveries.Add(Discovery(10, 5, now));
        db.RelationDiscoveries.Add(Discovery(11, 5, now.AddMinutes(1)));
        await db.SaveChangesAsync();

        var metadataRefresh = new FakeMetadataRefreshService(db, new()
        {
            [5] = new AnimeMetadata { Id = 5, Title = "Announced Sequel", AiringStatus = "not_yet_aired" },
        });
        var service = CreateService(db, metadataRefresh);

        var malCalls = await service.ResolveAsync(10);

        Assert.Equal(1, malCalls);
        Assert.Single(await db.AnimeUpdates.ToListAsync());
        var discoveries = await db.RelationDiscoveries.AsNoTracking().ToListAsync();
        Assert.Equal(2, discoveries.Count);
        Assert.All(discoveries, d => Assert.NotNull(d.ProcessedAt));
    }

    [Fact]
    public async Task AnAlreadyAnnouncedAnimeIsNotAnnouncedTwice()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Owner" });
        // Already cached and already carries an Announced update from an
        // earlier resolution pass.
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 6, Title = "Already Announced", AiringStatus = "not_yet_aired" });
        db.AnimeUpdates.Add(new AnimeUpdate { AnimeId = 6, DetectedAt = DateTimeOffset.UtcNow.AddDays(-1), Kinds = AnimeUpdateKinds.Announced });
        // A later, lagging discovery naming the same anime.
        db.RelationDiscoveries.Add(Discovery(1, 6, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();

        var metadataRefresh = new FakeMetadataRefreshService(db, new());
        var service = CreateService(db, metadataRefresh);

        var malCalls = await service.ResolveAsync(10);

        Assert.Equal(0, malCalls); // anime 6 was already cached — no fetch needed
        Assert.Single(await db.AnimeUpdates.ToListAsync());
        var discovery = await db.RelationDiscoveries.AsNoTracking().SingleAsync();
        Assert.NotNull(discovery.ProcessedAt);
    }

    private sealed class FakeMetadataRefreshService(AnimeTrackerDbContext db, Dictionary<int, AnimeMetadata> resolvedAnime) : IMetadataRefreshService
    {
        public Task<int> RefreshStaleBatchAsync(int batchSize, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public async Task RefreshOneAsync(int animeId, CancellationToken ct = default)
        {
            if (!resolvedAnime.TryGetValue(animeId, out var anime))
                throw new AnimeMetadataNotFoundException(animeId);

            db.AnimeMetadata.Add(anime);
            await db.SaveChangesAsync(ct);
        }
    }
}
