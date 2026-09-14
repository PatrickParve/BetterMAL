using System.Net;
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
        var tally = new MalCallTally();

        await service.ResolveAsync(10, skipAnimeId: null, tally);

        Assert.Equal(1, tally.Attempts);
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

        await service.ResolveAsync(10, skipAnimeId: null, new MalCallTally());

        Assert.Empty(await db.AnimeUpdates.ToListAsync());
        var discovery = await db.RelationDiscoveries.AsNoTracking().SingleAsync();
        Assert.NotNull(discovery.ProcessedAt);
    }

    // Reworked for outage/not-found accounting (metadata-refresh-call-accounting
    // tasks 4.3-4.4): a plain 400 is an "Other" failure, so it still leaves the
    // discovery unprocessed for a later pass, but it now counts as an attempt.
    [Fact]
    public async Task AFailedFetchLeavesTheDiscoveryUnprocessed()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Owner" });
        db.RelationDiscoveries.Add(Discovery(1, 4, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();

        var metadataRefresh = new FakeMetadataRefreshService(db, new(),
            new() { [4] = new HttpRequestException("bad request", null, HttpStatusCode.BadRequest) });
        var service = CreateService(db, metadataRefresh);
        var tally = new MalCallTally();

        await service.ResolveAsync(10, skipAnimeId: null, tally);

        Assert.Equal(1, tally.Attempts);
        Assert.False(tally.MalUnavailable);
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
        var tally = new MalCallTally();

        await service.ResolveAsync(10, skipAnimeId: null, tally);

        Assert.Equal(1, tally.Attempts);
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
        var tally = new MalCallTally();

        await service.ResolveAsync(10, skipAnimeId: null, tally);

        Assert.Equal(0, tally.Attempts); // anime 6 was already cached — no fetch needed
        Assert.Empty(metadataRefresh.Calls);
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
        var tally = new MalCallTally();

        await service.ResolveAsync(10, skipAnimeId: null, tally);

        Assert.Equal(0, tally.Attempts);
        Assert.Empty(metadataRefresh.Calls);
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
        var tally = new MalCallTally();

        await service.ResolveAsync(10, skipAnimeId: null, tally);

        Assert.Equal(0, tally.Attempts);
        Assert.Empty(metadataRefresh.Calls);
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
        var tally = new MalCallTally();

        await service.ResolveAsync(10, skipAnimeId: null, tally);

        Assert.Equal(1, tally.Attempts);
        var update = Assert.Single(await db.AnimeUpdates.AsNoTracking().ToListAsync());
        Assert.Equal(9, update.AnimeId);
    }

    // --- 4.5: outage/not-found accounting for resolution (design D3, D6) ---

    [Fact]
    public async Task ADiscoveryNamingANotFoundAnimeIsProcessedAndNotRetried()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Owner" });
        db.RelationDiscoveries.Add(Discovery(1, 20, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();

        // 20 is absent from the resolved-anime map, so the fake throws
        // AnimeMetadataNotFoundException, same as a real 404.
        var metadataRefresh = new FakeMetadataRefreshService(db, new());
        var service = CreateService(db, metadataRefresh);
        var tally = new MalCallTally();

        await service.ResolveAsync(10, skipAnimeId: null, tally);

        Assert.Equal(1, tally.Attempts);
        Assert.Empty(await db.AnimeUpdates.ToListAsync());
        var discovery = await db.RelationDiscoveries.AsNoTracking().SingleAsync();
        Assert.NotNull(discovery.ProcessedAt);

        await service.ResolveAsync(10, skipAnimeId: null, new MalCallTally());
        Assert.Equal([20], metadataRefresh.Calls); // already processed: no second call
    }

    [Fact]
    public async Task OutageOnTheSecondOfTwoGroupsEndsThePassLeavingItUnprocessed()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Owner" });
        var now = DateTimeOffset.UtcNow;
        db.RelationDiscoveries.Add(Discovery(1, 21, now));
        db.RelationDiscoveries.Add(Discovery(1, 22, now.AddMinutes(1)));
        await db.SaveChangesAsync();

        var metadataRefresh = new FakeMetadataRefreshService(
            db,
            new() { [21] = new AnimeMetadata { Id = 21, Title = "Resolved", AiringStatus = "finished_airing" } },
            new() { [22] = new HttpRequestException("failed", null, HttpStatusCode.ServiceUnavailable) });
        var service = CreateService(db, metadataRefresh);
        var tally = new MalCallTally();

        await service.ResolveAsync(10, skipAnimeId: null, tally);

        Assert.Equal(2, tally.Attempts);
        Assert.Equal(22, tally.UnavailableAnimeId);
        var discoveries = await db.RelationDiscoveries.AsNoTracking().ToDictionaryAsync(d => d.RelatedAnimeId);
        Assert.NotNull(discoveries[21].ProcessedAt);
        Assert.Null(discoveries[22].ProcessedAt);
    }

    [Fact]
    public async Task OutageOnTheFirstOfThreeGroupsLeavesTheOtherTwoUntouched()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Owner" });
        var now = DateTimeOffset.UtcNow;
        db.RelationDiscoveries.Add(Discovery(1, 31, now));
        db.RelationDiscoveries.Add(Discovery(1, 32, now.AddMinutes(1)));
        db.RelationDiscoveries.Add(Discovery(1, 33, now.AddMinutes(2)));
        await db.SaveChangesAsync();

        var metadataRefresh = new FakeMetadataRefreshService(
            db, new(),
            new() { [31] = new HttpRequestException("failed", null, HttpStatusCode.ServiceUnavailable) });
        var service = CreateService(db, metadataRefresh);
        var tally = new MalCallTally();

        await service.ResolveAsync(10, skipAnimeId: null, tally);

        Assert.Equal(1, tally.Attempts);
        Assert.Equal([31], metadataRefresh.Calls);
        Assert.All(await db.RelationDiscoveries.AsNoTracking().ToListAsync(), d => Assert.Null(d.ProcessedAt));
    }

    [Fact]
    public async Task SkipAnimeIdExcludesThatDiscoveryAndTheNextOneStillResolves()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Owner" });
        var now = DateTimeOffset.UtcNow;
        db.RelationDiscoveries.Add(Discovery(1, 41, now));
        db.RelationDiscoveries.Add(Discovery(1, 42, now.AddMinutes(1)));
        await db.SaveChangesAsync();

        var metadataRefresh = new FakeMetadataRefreshService(db, new()
        {
            [42] = new AnimeMetadata { Id = 42, Title = "Resolved", AiringStatus = "finished_airing" },
        });
        var service = CreateService(db, metadataRefresh);
        var tally = new MalCallTally();

        await service.ResolveAsync(10, skipAnimeId: 41, tally);

        Assert.DoesNotContain(41, metadataRefresh.Calls);
        Assert.Contains(42, metadataRefresh.Calls);
        var discoveries = await db.RelationDiscoveries.AsNoTracking().ToDictionaryAsync(d => d.RelatedAnimeId);
        Assert.Null(discoveries[41].ProcessedAt);
        Assert.NotNull(discoveries[42].ProcessedAt);
    }

    private sealed class FakeMetadataRefreshService(
        AnimeTrackerDbContext db, Dictionary<int, AnimeMetadata> resolvedAnime, Dictionary<int, Exception>? failures = null) : IMetadataRefreshService
    {
        public List<int> Calls { get; } = [];

        public Task RefreshStaleBatchAsync(int batchSize, int? skipAnimeId, MalCallTally tally, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public async Task RefreshOneAsync(int animeId, CancellationToken ct = default)
        {
            Calls.Add(animeId);
            if (failures is not null && failures.TryGetValue(animeId, out var failure))
                throw failure;
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
