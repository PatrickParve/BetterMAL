using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Metadata;
using AnimeTracker.Api.Services.Relations;
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
        new(db, metadataRefresh, new AnimeUpdateRecorder(db, new AnimeUpdateRelevance(db, new RelationResolver(db))), NullLogger<AnnouncementResolutionService>.Instance);

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
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Status = WatchStatus.Watching });
        // The relevance gate AnimeUpdateRecorder now applies needs an actual
        // relation edge to the owner, not just the RelationDiscovery audit
        // row below (scope-updates-to-my-list tasks 6.1).
        db.AnimeRelatedAnime.Add(new AnimeRelatedAnime { AnimeId = 2, RelatedAnimeId = 1, RelationType = "sequel" });
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
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 10, Status = WatchStatus.Watching });
        db.AnimeRelatedAnime.Add(new AnimeRelatedAnime { AnimeId = 5, RelatedAnimeId = 10, RelationType = "sequel" });
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
        // earlier resolution pass. LastSyncedAt set: design.md D5's
        // hadFullDetail gate reads this, not merely whether a row exists, to
        // decide the anime needs no resolving fetch.
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 6, Title = "Already Announced", AiringStatus = "not_yet_aired", LastSyncedAt = DateTimeOffset.UtcNow.AddDays(-30) });
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

    // design.md D5 (scope-updates-to-my-list tasks 6.6): hadFullDetail is
    // captured before any resolving fetch, so an anime already fully fetched
    // is recognised as not-announceable without spending a MAL call at all —
    // distinct from AnAlreadyAnnouncedAnimeIsNotAnnouncedTwice above, which
    // covers the same anime naming an update it already carries.
    [Fact]
    public async Task ADiscoveryNamingAnAlreadyFullyFetchedAnimeSpendsNoMalCall()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Owner" });
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 7, Title = "Already known", LastSyncedAt = DateTimeOffset.UtcNow.AddDays(-30) });
        db.RelationDiscoveries.Add(Discovery(1, 7, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();

        var metadataRefresh = new FakeMetadataRefreshService(db, new()); // empty: any call would throw
        var service = CreateService(db, metadataRefresh);

        var malCalls = await service.ResolveAsync(10);

        Assert.Equal(0, malCalls);
        Assert.Empty(await db.AnimeUpdates.ToListAsync());
        var discovery = await db.RelationDiscoveries.AsNoTracking().SingleAsync();
        Assert.NotNull(discovery.ProcessedAt);
    }

    // design.md D5: hasEntry ignores status — a dropped entry still proves
    // the anime isn't a new show, even though it was never fully fetched.
    [Fact]
    public async Task ADiscoveryNamingAnAnimeWithAListEntryOfAnyStatusRecordsNothing()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Owner" });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 8, Status = WatchStatus.Dropped });
        db.RelationDiscoveries.Add(Discovery(1, 8, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();

        var metadataRefresh = new FakeMetadataRefreshService(db, new()); // empty: any call would throw
        var service = CreateService(db, metadataRefresh);

        var malCalls = await service.ResolveAsync(10);

        Assert.Equal(0, malCalls);
        Assert.Empty(await db.AnimeUpdates.ToListAsync());
        var discovery = await db.RelationDiscoveries.AsNoTracking().SingleAsync();
        Assert.NotNull(discovery.ProcessedAt);
    }

    // design.md D5 step 4: the fetch runs whenever the row is absent OR
    // never fully fetched — closing the latent hole where a lean row (a
    // season-listing write, say) carried no airing status for the gate to
    // read and was silently never announced.
    [Fact]
    public async Task ADiscoveryNamingALeanRowAnimeIsFetchedForItsAiringStatus()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Owner" });
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 9, Title = "Lean Sequel", LastSyncedAt = default });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Status = WatchStatus.Watching });
        // The relevance gate AnimeUpdateRecorder now applies needs an actual
        // relation edge to the owner, not just the RelationDiscovery audit
        // row below (scope-updates-to-my-list tasks 6.1).
        db.AnimeRelatedAnime.Add(new AnimeRelatedAnime { AnimeId = 9, RelatedAnimeId = 1, RelationType = "sequel" });
        db.RelationDiscoveries.Add(Discovery(1, 9, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();

        var metadataRefresh = new FakeMetadataRefreshService(db, new()
        {
            [9] = new AnimeMetadata { Id = 9, Title = "Lean Sequel", AiringStatus = "not_yet_aired", LastSyncedAt = DateTimeOffset.UtcNow },
        });
        var service = CreateService(db, metadataRefresh);

        var malCalls = await service.ResolveAsync(10);

        Assert.Equal(1, malCalls);
        var update = Assert.Single(await db.AnimeUpdates.AsNoTracking().ToListAsync());
        Assert.Equal(9, update.AnimeId);
    }

    private sealed class FakeMetadataRefreshService(AnimeTrackerDbContext db, Dictionary<int, AnimeMetadata> resolvedAnime) : IMetadataRefreshService
    {
        public Task<int> RefreshStaleBatchAsync(int batchSize, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public async Task RefreshOneAsync(int animeId, CancellationToken ct = default)
        {
            if (!resolvedAnime.TryGetValue(animeId, out var resolved))
                throw new AnimeMetadataNotFoundException(animeId);

            // Upsert: a discovery can name an anime that already has a lean
            // cached row (a season-listing write, say), in which case this
            // fetch is a resolving update to that row rather than a fresh
            // insert.
            var existing = await db.AnimeMetadata.FirstOrDefaultAsync(a => a.Id == animeId, ct);
            if (existing is null)
                db.AnimeMetadata.Add(resolved);
            else
                db.Entry(existing).CurrentValues.SetValues(resolved);

            await db.SaveChangesAsync(ct);
        }
    }
}
