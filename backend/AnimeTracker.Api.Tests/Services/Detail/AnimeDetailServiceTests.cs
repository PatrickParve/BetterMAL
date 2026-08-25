using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Detail;
using AnimeTracker.Api.Services.Entries;
using AnimeTracker.Api.Services.Infrastructure;
using AnimeTracker.Api.Services.Metadata;
using AnimeTracker.Api.Services.Relations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimeTracker.Api.Tests.Services.Detail;

// AnimeDetailService.NeedsFullDetailFetch (metadata-refresh spec, tasks
// 2.8-2.10/8.4/8.13): a plain TTL comparison against RefreshTiers, replacing
// the old Genres/RelatedAnime-count completeness markers and dated migration
// cutoffs; and the RefreshFailed flag for a live fetch that throws.
public class AnimeDetailServiceTests
{
    // A fixed instant standing for "already full-detail fetched at this
    // point" — every boundary below is expressed relative to UtcNow at test
    // run time, not to this constant.
    private static readonly TimeSpan Buffer = TimeSpan.FromHours(2);

    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static AnimeDetailService CreateService(AnimeTrackerDbContext db, FakeMetadataRefreshService refreshService) =>
        new(
            new AnimeMetadataRepository(db),
            refreshService,
            new FakeEpisodeScheduleService(),
            new FakeCompletedEntryReopenService(),
            new RelationResolver(db),
            db,
            new RefreshGate(),
            NullLogger<AnimeDetailService>.Instance);

    private static AnimeMetadata Anime(
        int id, DateTimeOffset lastSyncedAt, string? airingStatus = "finished_airing", DateOnly? airedTo = null) => new()
    {
        Id = id,
        Title = $"Anime {id}",
        AiringStatus = airingStatus,
        AiredTo = airedTo,
        LastSyncedAt = lastSyncedAt,
    };

    // --- 8.4: tier TTL matrix ---

    [Fact]
    public async Task GetDetailAsync_NeverFetched_AlwaysRefetches()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(Anime(1, default));
        await db.SaveChangesAsync();
        var refresh = new FakeMetadataRefreshService(db);

        await CreateService(db, refresh).GetDetailAsync(1);

        Assert.Contains(1, refresh.Calls);
    }

    [Fact]
    public async Task GetDetailAsync_AiringTier_JustInsideTtlServesCache()
    {
        using var db = CreateDb();
        var now = DateTimeOffset.UtcNow;
        db.AnimeMetadata.Add(Anime(1, now - TimeSpan.FromDays(1) + Buffer, airingStatus: "currently_airing"));
        await db.SaveChangesAsync();
        var refresh = new FakeMetadataRefreshService(db);

        await CreateService(db, refresh).GetDetailAsync(1);

        Assert.Empty(refresh.Calls);
    }

    [Fact]
    public async Task GetDetailAsync_AiringTier_JustOutsideTtlRefetches()
    {
        using var db = CreateDb();
        var now = DateTimeOffset.UtcNow;
        db.AnimeMetadata.Add(Anime(1, now - TimeSpan.FromDays(1) - Buffer, airingStatus: "currently_airing"));
        await db.SaveChangesAsync();
        var refresh = new FakeMetadataRefreshService(db);

        await CreateService(db, refresh).GetDetailAsync(1);

        Assert.Contains(1, refresh.Calls);
    }

    [Fact]
    public async Task GetDetailAsync_RecentlyFinishedTier_JustInsideTtlServesCache()
    {
        using var db = CreateDb();
        var now = DateTimeOffset.UtcNow;
        var airedTo = DateOnly.FromDateTime(now.UtcDateTime).AddMonths(-6); // within 1 year -> 3-day tier
        db.AnimeMetadata.Add(Anime(1, now - TimeSpan.FromDays(3) + Buffer, airedTo: airedTo));
        await db.SaveChangesAsync();
        var refresh = new FakeMetadataRefreshService(db);

        await CreateService(db, refresh).GetDetailAsync(1);

        Assert.Empty(refresh.Calls);
    }

    [Fact]
    public async Task GetDetailAsync_RecentlyFinishedTier_JustOutsideTtlRefetches()
    {
        using var db = CreateDb();
        var now = DateTimeOffset.UtcNow;
        var airedTo = DateOnly.FromDateTime(now.UtcDateTime).AddMonths(-6);
        db.AnimeMetadata.Add(Anime(1, now - TimeSpan.FromDays(3) - Buffer, airedTo: airedTo));
        await db.SaveChangesAsync();
        var refresh = new FakeMetadataRefreshService(db);

        await CreateService(db, refresh).GetDetailAsync(1);

        Assert.Contains(1, refresh.Calls);
    }

    [Fact]
    public async Task GetDetailAsync_AgingTier_JustInsideTtlServesCache()
    {
        using var db = CreateDb();
        var now = DateTimeOffset.UtcNow;
        var airedTo = DateOnly.FromDateTime(now.UtcDateTime).AddYears(-1).AddMonths(-6); // 1-2 years -> 14-day tier
        db.AnimeMetadata.Add(Anime(1, now - TimeSpan.FromDays(14) + Buffer, airedTo: airedTo));
        await db.SaveChangesAsync();
        var refresh = new FakeMetadataRefreshService(db);

        await CreateService(db, refresh).GetDetailAsync(1);

        Assert.Empty(refresh.Calls);
    }

    [Fact]
    public async Task GetDetailAsync_AgingTier_JustOutsideTtlRefetches()
    {
        using var db = CreateDb();
        var now = DateTimeOffset.UtcNow;
        var airedTo = DateOnly.FromDateTime(now.UtcDateTime).AddYears(-1).AddMonths(-6);
        db.AnimeMetadata.Add(Anime(1, now - TimeSpan.FromDays(14) - Buffer, airedTo: airedTo));
        await db.SaveChangesAsync();
        var refresh = new FakeMetadataRefreshService(db);

        await CreateService(db, refresh).GetDetailAsync(1);

        Assert.Contains(1, refresh.Calls);
    }

    [Fact]
    public async Task GetDetailAsync_StaleTier_JustInsideTtlServesCache()
    {
        using var db = CreateDb();
        var now = DateTimeOffset.UtcNow;
        var airedTo = DateOnly.FromDateTime(now.UtcDateTime).AddYears(-3); // older than 2 years -> 28-day tier
        db.AnimeMetadata.Add(Anime(1, now - TimeSpan.FromDays(28) + Buffer, airedTo: airedTo));
        await db.SaveChangesAsync();
        var refresh = new FakeMetadataRefreshService(db);

        await CreateService(db, refresh).GetDetailAsync(1);

        Assert.Empty(refresh.Calls);
    }

    [Fact]
    public async Task GetDetailAsync_StaleTier_JustOutsideTtlRefetches()
    {
        using var db = CreateDb();
        var now = DateTimeOffset.UtcNow;
        var airedTo = DateOnly.FromDateTime(now.UtcDateTime).AddYears(-3);
        db.AnimeMetadata.Add(Anime(1, now - TimeSpan.FromDays(28) - Buffer, airedTo: airedTo));
        await db.SaveChangesAsync();
        var refresh = new FakeMetadataRefreshService(db);

        await CreateService(db, refresh).GetDetailAsync(1);

        Assert.Contains(1, refresh.Calls);
    }

    // Regression guard for the 130 live rows (e.g. 27775, 48556) stuck behind
    // the old Genres/RelatedAnime-count completeness proxy: a row that
    // genuinely has genres and zero relations must not be treated as
    // incomplete and refetched on every visit.
    [Fact]
    public async Task GetDetailAsync_GenresPresentButGenuinelyZeroRelations_DoesNotRefetchWithinTier()
    {
        using var db = CreateDb();
        var now = DateTimeOffset.UtcNow;
        var airedTo = DateOnly.FromDateTime(now.UtcDateTime).AddMonths(-6);
        var anime = Anime(1, now - TimeSpan.FromDays(1), airedTo: airedTo);
        anime.Genres = ["Drama"];
        db.AnimeMetadata.Add(anime);
        await db.SaveChangesAsync();
        var refresh = new FakeMetadataRefreshService(db);

        await CreateService(db, refresh).GetDetailAsync(1);

        Assert.Empty(refresh.Calls);
    }

    // --- 8.13: RefreshFailed ---

    [Fact]
    public async Task GetDetailAsync_LiveFetchThrows_FlagsRefreshFailedButStillServesCachedData()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Cached Title", LastSyncedAt = default });
        await db.SaveChangesAsync();
        var refresh = new FakeMetadataRefreshService(db, throwOnRefresh: true);

        var detail = await CreateService(db, refresh).GetDetailAsync(1);

        Assert.True(detail.RefreshFailed);
        Assert.Equal("Cached Title", detail.Title);
    }

    [Fact]
    public async Task GetDetailAsync_SuccessfulFetch_DoesNotFlagRefreshFailed()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(Anime(1, default));
        await db.SaveChangesAsync();
        var refresh = new FakeMetadataRefreshService(db);

        var detail = await CreateService(db, refresh).GetDetailAsync(1);

        Assert.False(detail.RefreshFailed);
    }

    [Fact]
    public async Task GetDetailAsync_FailedFetchLeavesLastSyncedAtUntouchedSoTheNextReadRetries()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(Anime(1, default));
        await db.SaveChangesAsync();
        var refresh = new FakeMetadataRefreshService(db, throwOnRefresh: true);
        var service = CreateService(db, refresh);

        await service.GetDetailAsync(1);
        await service.GetDetailAsync(1);

        Assert.Equal(2, refresh.Calls.Count);
    }

    private sealed class FakeMetadataRefreshService(AnimeTrackerDbContext db, bool throwOnRefresh = false) : IMetadataRefreshService
    {
        public List<int> Calls { get; } = [];

        public Task<int> RefreshStaleBatchAsync(int batchSize, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public async Task RefreshOneAsync(int animeId, CancellationToken ct = default)
        {
            Calls.Add(animeId);
            if (throwOnRefresh)
                throw new InvalidOperationException("Simulated MAL failure.");

            var anime = await db.AnimeMetadata.FirstAsync(a => a.Id == animeId, ct);
            anime.LastSyncedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }

    private sealed class FakeEpisodeScheduleService : IEpisodeScheduleService
    {
        public Task<ResolvedEpisode?> ResolveOnLocalDateAsync(AnimeMetadata anime, DateOnly localDate, CancellationToken ct = default) =>
            Task.FromResult<ResolvedEpisode?>(null);
        public Task<DateTimeOffset?> NextAiringInstantAsync(AnimeMetadata anime, DateTimeOffset afterUtc, CancellationToken ct = default) =>
            Task.FromResult<DateTimeOffset?>(null);
        public Task<int?> EpisodesAiredAsOfAsync(AnimeMetadata anime, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            Task.FromResult<int?>(null);
        public Task<Dictionary<int, int>> EpisodesAiredAsOfAsync(IReadOnlyCollection<AnimeMetadata> anime, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            Task.FromResult(new Dictionary<int, int>());
        public Task<Dictionary<int, int>> EpisodesAiredAsOfAsync(IReadOnlyCollection<int> animeIds, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            Task.FromResult(new Dictionary<int, int>());
    }

    private sealed class FakeCompletedEntryReopenService : ICompletedEntryReopenService
    {
        public Task ReopenAsync(
            IReadOnlyCollection<UserAnimeEntry> entries, IReadOnlyDictionary<int, int> airedSoFarByAnimeId, CancellationToken ct = default) =>
            Task.CompletedTask;
    }
}
