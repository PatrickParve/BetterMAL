using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Mal.Dto;
using AnimeTracker.Api.Services.Metadata;
using AnimeTracker.Api.Services.Series;
using AnimeTracker.Api.Services.Updates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimeTracker.Api.Tests.Services.Updates;

// AnimeMetadataChangeDetector's relation-discovery-enqueues-a-build wiring
// (split-series-by-version design.md D8, metadata-refresh spec "A refresh
// that discovers a relation enqueues a series build", tasks 9.1-9.3): a
// relation edge newly discovered on any metadata write enqueues the owning
// anime on ISeriesBuildTrigger, non-blocking and deduplicated, so a newly
// announced entry — or any other newly discovered relation — reaches the
// series page without waiting for a visit.
public class AnimeMetadataChangeDetectorSeriesBuildTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static MetadataRefreshService CreateService(AnimeTrackerDbContext db, IMalClient malClient, ISeriesBuildTrigger trigger) =>
        new(db, malClient, new AnimeMetadataChangeDetector(db, new AnimeUpdateRecorder(db), trigger), NullLogger<MetadataRefreshService>.Instance);

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

    [Fact]
    public async Task ADiscoveredRelationEnqueuesTheOwningAnime()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1", LastSyncedAt = default };
        anime.RelatedAnime.Add(new AnimeRelatedAnime { AnimeId = 1, RelatedAnimeId = 2, RelationType = "sequel", Title = "Anime 2" });
        db.AnimeMetadata.Add(anime);
        await db.SaveChangesAsync();

        var trigger = new SeriesBuildTrigger();
        // MAL now additionally reports a new sequel edge to 3.
        var malClient = new FakeMalClient(new Dictionary<int, MalAnimeNode> { [1] = DetailNode(1, (2, "sequel"), (3, "sequel")) });
        await CreateService(db, malClient, trigger).RefreshOneAsync(1);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        Assert.Equal(1, await trigger.WaitAsync(cts.Token));
    }

    [Fact]
    public async Task ARewriteWithNoNewEdgeEnqueuesNothing()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1", LastSyncedAt = default };
        anime.RelatedAnime.Add(new AnimeRelatedAnime { AnimeId = 1, RelatedAnimeId = 2, RelationType = "sequel", Title = "Anime 2" });
        db.AnimeMetadata.Add(anime);
        await db.SaveChangesAsync();

        var trigger = new SeriesBuildTrigger();
        // MAL reports exactly the same edge — nothing new.
        var malClient = new FakeMalClient(new Dictionary<int, MalAnimeNode> { [1] = DetailNode(1, (2, "sequel")) });
        await CreateService(db, malClient, trigger).RefreshOneAsync(1);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => trigger.WaitAsync(cts.Token));
    }

    // fix-false-updates-on-lean-rows design D4: the lean listing paths
    // (season browsing, Top-Anime, reconciliation) snapshot through
    // SnapshotListing, which leaves Relations null — "this write did not
    // observe relations", as opposed to an empty set's "this anime has none".
    // Neither of them Includes RelatedAnime, so diffing it would read an
    // unloaded collection as empty and either manufacture removals or
    // re-discover every edge; either way it would enqueue a series build off
    // a write that never looked at a single relation.
    [Fact]
    public async Task AListingWriteEnqueuesNothingEvenOnARowThatCarriesEdges()
    {
        using var db = CreateDb();
        var now = DateTimeOffset.UtcNow;
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1", LastSyncedAt = now.AddDays(-60) };
        anime.RelatedAnime.Add(new AnimeRelatedAnime { AnimeId = 1, RelatedAnimeId = 2, RelationType = "sequel", Title = "Anime 2" });
        db.AnimeMetadata.Add(anime);
        await db.SaveChangesAsync();

        var trigger = new SeriesBuildTrigger();
        var detector = new AnimeMetadataChangeDetector(db, new AnimeUpdateRecorder(db), trigger);

        var before = detector.SnapshotListing(anime);
        new MalAnimeNode { Id = 1, Title = "Anime 1", MediaType = "tv", NumEpisodes = 12 }.ApplyLeanTo(anime, now);
        await detector.RecordAsync(anime, before, now);
        await db.SaveChangesAsync();

        Assert.Empty(await db.RelationDiscoveries.ToListAsync());

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => trigger.WaitAsync(cts.Token));
    }

    private sealed class FakeMalClient(Dictionary<int, MalAnimeNode> responses) : IMalClient
    {
        public Task<MalAnimeNode> GetAnimeDetailsAsync(int animeId, IReadOnlyCollection<string>? fields = null, CancellationToken ct = default) =>
            Task.FromResult(responses[animeId]);

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
