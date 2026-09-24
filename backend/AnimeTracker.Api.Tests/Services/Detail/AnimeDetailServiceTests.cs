using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Detail;
using AnimeTracker.Api.Services.Entries;
using AnimeTracker.Api.Services.Infrastructure;
using AnimeTracker.Api.Services.Metadata;
using AnimeTracker.Api.Services.Relations;
using AnimeTracker.Api.Services.IdMapping;
using AnimeTracker.Api.Services.Tmdb;
using AnimeTracker.Api.Tests.Services.IdMapping;
using AnimeTracker.Api.Tests.Services.Tmdb;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using static AnimeTracker.Api.Tests.Services.Tmdb.TmdbTestData;

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

    // The TMDB service defaults to one with no key, which makes no request and
    // reports nothing due — so the tests below that are not about TMDB never
    // see it. The ones that are pass their own, over the same db.
    private static AnimeDetailService CreateService(
        AnimeTrackerDbContext db, FakeMetadataRefreshService refreshService, ITmdbArtworkService? tmdb = null,
        ICustomIdMappings? custom = null) =>
        new(
            new AnimeMetadataRepository(db),
            refreshService,
            new FakeEpisodeScheduleService(),
            new FakeAiringWatchStatusService(),
            new RelationResolver(db),
            db,
            new RefreshGate(),
            tmdb ?? ArtworkService(db, new FakeTmdbClient(), apiKey: "", custom: custom),
            TestIdMappings.Resolver(db, custom),
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

    // --- 4.4: PicturesFetchPending ---

    [Fact]
    public async Task GetDetailAsync_MyListAnimeWithNoPictureSetAndFreshDetail_FlagsPicturesFetchPending()
    {
        using var db = CreateDb();
        var anime = Anime(1, DateTimeOffset.UtcNow); // fresh enough to skip the live fetch
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1 });
        await db.SaveChangesAsync();
        var refresh = new FakeMetadataRefreshService(db);

        var detail = await CreateService(db, refresh).GetDetailAsync(1);

        Assert.True(detail.PicturesFetchPending);
        Assert.Empty(refresh.Calls);
    }

    [Fact]
    public async Task GetDetailAsync_LiveFetchSuppliesPictures_ClearsPicturesFetchPending()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(Anime(1, default)); // never fetched -> triggers the live fetch
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1 });
        await db.SaveChangesAsync();
        var refresh = new FakeMetadataRefreshService(db, setsPicturesSyncedAt: true);

        var detail = await CreateService(db, refresh).GetDetailAsync(1);

        Assert.False(detail.PicturesFetchPending);
    }

    [Fact]
    public async Task GetDetailAsync_AnimeWithNoEntry_NeverFlagsPicturesFetchPending()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(Anime(1, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
        var refresh = new FakeMetadataRefreshService(db);

        var detail = await CreateService(db, refresh).GetDetailAsync(1);

        Assert.False(detail.PicturesFetchPending);
    }

    [Fact]
    public async Task GetDetailAsync_DtoCarriesStoredPictureSetAndMalMainPicture()
    {
        using var db = CreateDb();
        var anime = Anime(1, DateTimeOffset.UtcNow);
        anime.MalPictureUrl = "https://mal/main.jpg";
        anime.PictureUrls = ["https://mal/p1.jpg", "https://mal/p2.jpg"];
        db.AnimeMetadata.Add(anime);
        await db.SaveChangesAsync();
        var refresh = new FakeMetadataRefreshService(db);

        var detail = await CreateService(db, refresh).GetDetailAsync(1);

        Assert.Equal("https://mal/main.jpg", detail.MalPictureUrl);
        Assert.Equal(["https://mal/p1.jpg", "https://mal/p2.jpg"], detail.PictureUrls);
    }

    [Fact]
    public async Task GetDetailAsync_DtoCarriesNullSelectedPictureUrlForAnAnimeWithNoChoice()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(Anime(1, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
        var refresh = new FakeMetadataRefreshService(db);

        var detail = await CreateService(db, refresh).GetDetailAsync(1);

        Assert.Null(detail.SelectedPictureUrl);
    }

    [Fact]
    public async Task GetDetailAsync_DtoCarriesTheChosenPictureUrlForAnAnimeWithAChoice()
    {
        using var db = CreateDb();
        var anime = Anime(1, DateTimeOffset.UtcNow);
        anime.SelectedPictureUrl = "https://mal/chosen.jpg";
        db.AnimeMetadata.Add(anime);
        await db.SaveChangesAsync();
        var refresh = new FakeMetadataRefreshService(db);

        var detail = await CreateService(db, refresh).GetDetailAsync(1);

        Assert.Equal("https://mal/chosen.jpg", detail.SelectedPictureUrl);
    }

    // --- 7.4: ImdbIds, Tmdb, TmdbFetchPending (anime-detail "The detail response
    // carries the anime's TMDB pictures and asks for a TMDB fetch when one is
    // due") ---

    // Anime 1: mapped to TV 1429 season 3, with a fresh detail so a read never
    // live-fetches it from MAL.
    private static void AddMappedAnime(AnimeTrackerDbContext db, bool inMyList = true, string[]? imdbIds = null) =>
        AddAnime(db, 1, inMyList, tvId: 1429, season: 3, imdbIds: imdbIds).LastSyncedAt = DateTimeOffset.UtcNow;

    [Fact]
    public async Task GetDetailAsync_MyListAnimeWithADueSeasonSet_FlagsTmdbFetchPendingAndNeverCallsTmdb()
    {
        using var db = CreateDb();
        AddMappedAnime(db);
        db.TmdbTvImageSets.Add(TvSet(1429, Fresh, Poster("/tv.jpg", "ja"))); // season 3 was never fetched
        await db.SaveChangesAsync();
        var tmdbClient = new FakeTmdbClient();

        var detail = await CreateService(db, new FakeMetadataRefreshService(db), ArtworkService(db, tmdbClient)).GetDetailAsync(1);

        Assert.True(detail.TmdbFetchPending);
        Assert.Empty(tmdbClient.Calls); // the read serves the cache; the follow-up request does the fetching
        var tmdb = Assert.IsType<AnimeTmdbPicturesDto>(detail.Tmdb);
        Assert.True(tmdb.HasMapping);
        Assert.Equal(TmdbScope.Series, Assert.Single(tmdb.Scopes).Scope);
    }

    [Fact]
    public async Task GetDetailAsync_MyListAnimeWithFreshSets_CarriesTheirImagesAndNoFlag()
    {
        using var db = CreateDb();
        AddMappedAnime(db);
        db.TmdbTvImageSets.Add(TvSet(1429, Fresh, Poster("/tv.jpg", "ja")));
        db.TmdbSeasonImageSets.Add(SeasonSet(1429, 3, Fresh, Poster("/s3.jpg", "en")));
        await db.SaveChangesAsync();
        var tmdbClient = new FakeTmdbClient();

        var detail = await CreateService(db, new FakeMetadataRefreshService(db), ArtworkService(db, tmdbClient)).GetDetailAsync(1);

        Assert.False(detail.TmdbFetchPending);
        var tmdb = Assert.IsType<AnimeTmdbPicturesDto>(detail.Tmdb);
        Assert.Equal([TmdbScope.Series, TmdbScope.Season], tmdb.Scopes.Select(scope => scope.Scope));
        var season = tmdb.Scopes[1];
        Assert.Equal(3, season.SeasonNumber);
        var english = Assert.Single(season.Languages);
        Assert.Equal("en", english.Language);
        Assert.Equal($"{ImageBase}/s3.jpg", Assert.Single(english.Pictures).Url);
        Assert.Empty(tmdbClient.Calls);
    }

    [Fact]
    public async Task GetDetailAsync_AnimeNotInMyList_CarriesImdbIdsButNoTmdbAndNoFlag()
    {
        using var db = CreateDb();
        AddMappedAnime(db, inMyList: false, imdbIds: ["tt2560140", "tt9999999"]);
        db.TmdbTvImageSets.Add(TvSet(1429, Fresh, Poster("/tv.jpg", "ja"))); // cached, and season 3 still to fetch — neither is offered
        await db.SaveChangesAsync();

        var detail = await CreateService(db, new FakeMetadataRefreshService(db), ArtworkService(db, new FakeTmdbClient())).GetDetailAsync(1);

        Assert.Equal(["tt2560140", "tt9999999"], detail.ImdbIds); // in the mapping's order, list or no list
        Assert.Null(detail.Tmdb);
        Assert.False(detail.TmdbFetchPending);
    }

    [Fact]
    public async Task GetDetailAsync_WithNoKey_HasNoFlagButStillOffersImagesCachedEarlier()
    {
        using var db = CreateDb();
        AddMappedAnime(db);
        db.TmdbTvImageSets.Add(TvSet(1429, Fresh, Poster("/tv.jpg", "ja"))); // season 3 never fetched: due, were there a key
        await db.SaveChangesAsync();
        var tmdbClient = new FakeTmdbClient();

        var detail = await CreateService(db, new FakeMetadataRefreshService(db), ArtworkService(db, tmdbClient, apiKey: "")).GetDetailAsync(1);

        Assert.False(detail.TmdbFetchPending);
        var tmdb = Assert.IsType<AnimeTmdbPicturesDto>(detail.Tmdb);
        Assert.Equal(TmdbScope.Series, Assert.Single(tmdb.Scopes).Scope);
        Assert.Empty(tmdbClient.Calls);
    }

    [Fact]
    public async Task GetDetailAsync_AnimeWithNoMapping_HasNoImdbIdsAndNoTmdbMatch()
    {
        using var db = CreateDb();
        AddAnime(db, 1, withMapping: false).LastSyncedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        var detail = await CreateService(db, new FakeMetadataRefreshService(db), ArtworkService(db, new FakeTmdbClient())).GetDetailAsync(1);

        Assert.Empty(detail.ImdbIds); // an empty list, not null
        var tmdb = Assert.IsType<AnimeTmdbPicturesDto>(detail.Tmdb); // in my list, so asked: no match is the answer
        Assert.False(tmdb.HasMapping);
        Assert.Empty(tmdb.Scopes);
        Assert.False(detail.TmdbFetchPending);
    }

    // --- The custom id mapping (design.md D19): the detail read sees the merged view ---

    [Fact]
    public async Task GetDetailAsync_AnimeTheSourceDoesNotMap_TakesItsImdbIdsAndTmdbMatchFromTheCustomMapping()
    {
        using var db = CreateDb();
        AddAnime(db, 1, withMapping: false).LastSyncedAt = DateTimeOffset.UtcNow;
        db.TmdbTvImageSets.Add(TvSet(280564, Fresh, Poster("/tv.jpg", "ja")));
        db.TmdbSeasonImageSets.Add(SeasonSet(280564, 1, Fresh, Poster("/season.jpg", "en")));
        await db.SaveChangesAsync();
        var custom = new FakeCustomIdMappings(FakeCustomIdMappings.Fill(1, tv: 280564, season: 1, imdb: ["tt38754770"]));

        var detail = await CreateService(db, new FakeMetadataRefreshService(db), ArtworkService(db, new FakeTmdbClient(), custom: custom), custom)
            .GetDetailAsync(1);

        Assert.Equal(["tt38754770"], detail.ImdbIds);
        var tmdb = Assert.IsType<AnimeTmdbPicturesDto>(detail.Tmdb);
        Assert.True(tmdb.HasMapping); // not "TMDB has no match"
        Assert.Equal([TmdbScope.Series, TmdbScope.Season], tmdb.Scopes.Select(scope => scope.Scope));
        Assert.False(detail.TmdbFetchPending); // both sets are cached and fresh
    }

    [Fact]
    public async Task GetDetailAsync_AnimeNotInMyList_StillCarriesTheCustomImdbIds()
    {
        // The IMDb link needs neither my list nor a key, and neither does a custom entry for it.
        using var db = CreateDb();
        AddAnime(db, 1, inMyList: false, withMapping: false).LastSyncedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        var custom = new FakeCustomIdMappings(FakeCustomIdMappings.Fill(1, imdb: ["tt38754770"]));

        var detail = await CreateService(db, new FakeMetadataRefreshService(db), custom: custom).GetDetailAsync(1);

        Assert.Equal(["tt38754770"], detail.ImdbIds);
        Assert.Null(detail.Tmdb);
    }

    [Fact]
    public async Task GetDetailAsync_ADefaultCustomEntryYieldsToTheSourcesImdbIds()
    {
        using var db = CreateDb();
        AddMappedAnime(db, imdbIds: ["tt2560140"]);
        await db.SaveChangesAsync();
        var custom = new FakeCustomIdMappings(FakeCustomIdMappings.Fill(1, imdb: ["tt0000099"]));

        var detail = await CreateService(db, new FakeMetadataRefreshService(db), custom: custom).GetDetailAsync(1);

        Assert.Equal(["tt2560140"], detail.ImdbIds); // the source has caught up: its ids apply
    }

    private sealed class FakeMetadataRefreshService(AnimeTrackerDbContext db, bool throwOnRefresh = false, bool setsPicturesSyncedAt = false) : IMetadataRefreshService
    {
        public List<int> Calls { get; } = [];

        public Task RefreshStaleBatchAsync(int batchSize, int? skipAnimeId, MalCallTally tally, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public async Task RefreshOneAsync(int animeId, CancellationToken ct = default)
        {
            Calls.Add(animeId);
            if (throwOnRefresh)
                throw new InvalidOperationException("Simulated MAL failure.");

            var anime = await db.AnimeMetadata.FirstAsync(a => a.Id == animeId, ct);
            anime.LastSyncedAt = DateTimeOffset.UtcNow;
            if (setsPicturesSyncedAt)
                anime.PicturesSyncedAt = DateTimeOffset.UtcNow;
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

    private sealed class FakeAiringWatchStatusService : IAiringWatchStatusService
    {
        public Task SettleAsync(
            IReadOnlyCollection<UserAnimeEntry> entries, IReadOnlyDictionary<int, int> airedSoFarByAnimeId, CancellationToken ct = default) =>
            Task.CompletedTask;
    }
}
