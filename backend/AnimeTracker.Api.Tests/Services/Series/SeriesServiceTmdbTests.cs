using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.IdMapping;
using AnimeTracker.Api.Services.Infrastructure;
using AnimeTracker.Api.Services.Ranking;
using AnimeTracker.Api.Services.Series;
using AnimeTracker.Api.Services.Tmdb;
using AnimeTracker.Api.Tests.Services.IdMapping;
using AnimeTracker.Api.Tests.Services.Tmdb;
using Microsoft.EntityFrameworkCore;
using static AnimeTracker.Api.Tests.Services.Tmdb.TmdbTestData;

namespace AnimeTracker.Api.Tests.Services.Series;

// SeriesService's TMDB projection (series-page "The series read carries the
// franchise's TMDB pictures and fetches due sets afterwards", "Series external
// links"): what SeriesDto.Tmdb and SeriesDto.ImdbIds are filled from. Each
// test reads a stored, freshly built series, so the projection runs and the
// graph builder never does. Which sets a franchise draws from, and how they
// are grouped, is TmdbArtworkServiceTests' and TmdbFranchiseKeysTests' subject;
// these tests are about what reaches the series read.
public class SeriesServiceTmdbTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    // graphBuilder is never reached: a series built just now and not partial needs no build.
    private static SeriesService CreateService(
        AnimeTrackerDbContext db, FakeTmdbClient client, string apiKey = "test-key", ICustomIdMappings? custom = null) =>
        new(db, graphBuilder: null!, new RefreshGate(), new FakeEpisodeScheduleService(), new NoOpAnimeRankingService(),
            ArtworkService(db, client, apiKey, custom: custom), TestIdMappings.Resolver(db, custom));

    // Series 1: a main line of two seasons of TV 1429 (anime 1 is the root),
    // and the film 635302 among the extras.
    private static AnimeTracker.Api.Models.Series SeedFranchise(
        AnimeTrackerDbContext db, string[]? rootImdbIds = null, string[]? secondSeasonImdbIds = null)
    {
        AddAnime(db, 1, tvId: 1429, season: 1, imdbIds: rootImdbIds);
        AddAnime(db, 2, tvId: 1429, season: 2, imdbIds: secondSeasonImdbIds);
        AddAnime(db, 3, movieIds: [635302]);
        return AddSeries(db, 1, [1, 2], (3, null, 0));
    }

    private static void SeedAllSetsFresh(AnimeTrackerDbContext db)
    {
        db.TmdbTvImageSets.Add(TvSet(1429, Fresh, Poster("/tv.jpg", "ja"), Backdrop("/tv-bd.jpg", "ja")));
        db.TmdbSeasonImageSets.Add(SeasonSet(1429, 1, Fresh, Poster("/s1.jpg", "ja")));
        db.TmdbSeasonImageSets.Add(SeasonSet(1429, 2, Fresh, Poster("/s2.jpg", null)));
        db.TmdbMovieImageSets.Add(MovieSet(635302, Fresh, Poster("/film.jpg", "ja"), Backdrop("/film-bd.jpg", null)));
    }

    private static List<string> Urls(TmdbLanguageGroupDto group) => group.Pictures.Select(p => p.Url[ImageBase.Length..]).ToList();

    [Fact]
    public async Task TheSeriesReadMixesSeriesSeasonAndMovieImagesByLanguagePostersBeforeBackdrops()
    {
        using var db = CreateDb();
        SeedFranchise(db);
        SeedAllSetsFresh(db);
        await db.SaveChangesAsync();
        var client = new FakeTmdbClient();

        var dto = await CreateService(db, client).GetSeriesAsync(2);

        Assert.True(dto.Tmdb.HasMapping);
        Assert.Equal(["none", "ja"], dto.Tmdb.Languages.Select(g => g.Language)); // by language only: no scope layer at all
        Assert.Equal(["/s2.jpg", "/film-bd.jpg"], Urls(dto.Tmdb.Languages[0]));
        Assert.Equal(["/tv.jpg", "/s1.jpg", "/film.jpg", "/tv-bd.jpg"], Urls(dto.Tmdb.Languages[1]));
        Assert.Equal(0, dto.Tmdb.PendingCount);
        Assert.Empty(client.Calls); // the read serves the cache; the follow-up request fetches
    }

    [Fact]
    public async Task PendingCountIsHowManyOfTheFranchisesSetsAreDue()
    {
        using var db = CreateDb();
        SeedFranchise(db);
        db.TmdbTvImageSets.Add(TvSet(1429, Fresh, Poster("/tv.jpg", "ja")));
        db.TmdbSeasonImageSets.Add(SeasonSet(1429, 1, Fresh, Poster("/s1.jpg", "ja")));
        db.TmdbSeasonImageSets.Add(SeasonSet(1429, 2, TimeSpan.FromDays(31), Poster("/s2.jpg", "ja"))); // stale
        // the film's set was never fetched
        await db.SaveChangesAsync();
        var client = new FakeTmdbClient();

        var dto = await CreateService(db, client).GetSeriesAsync(1);

        Assert.Equal(2, dto.Tmdb.PendingCount);
        Assert.Empty(client.Calls);
    }

    [Fact]
    public async Task WithNoKeyNothingReadsAsDueButImagesCachedEarlierAreStillOffered()
    {
        using var db = CreateDb();
        SeedFranchise(db);
        db.TmdbTvImageSets.Add(TvSet(1429, Fresh, Poster("/tv.jpg", "ja"))); // the other three sets were never fetched
        await db.SaveChangesAsync();

        var dto = await CreateService(db, new FakeTmdbClient(), apiKey: "").GetSeriesAsync(1);

        Assert.Equal(0, dto.Tmdb.PendingCount);
        Assert.Equal(["/tv.jpg"], Urls(Assert.Single(dto.Tmdb.Languages)));
    }

    [Fact]
    public async Task ASeriesWithNoMappedMemberHasNoTmdbMatchAndNoImdbIds()
    {
        using var db = CreateDb();
        AddAnime(db, 1, withMapping: false);
        AddSeries(db, 1, [1]);
        await db.SaveChangesAsync();

        var dto = await CreateService(db, new FakeTmdbClient()).GetSeriesAsync(1);

        Assert.False(dto.Tmdb.HasMapping);
        Assert.Empty(dto.Tmdb.Languages);
        Assert.Equal(0, dto.Tmdb.PendingCount);
        Assert.Empty(dto.ImdbIds);
    }

    // --- The root's IMDb ids (design.md D16) ---

    [Fact]
    public async Task ImdbIdsAreTheRootsEvenWhenReachedFromAnotherMember()
    {
        using var db = CreateDb();
        SeedFranchise(db, rootImdbIds: ["tt2560140"], secondSeasonImdbIds: ["tt7777777"]);
        await db.SaveChangesAsync();

        var dto = await CreateService(db, new FakeTmdbClient()).GetSeriesAsync(2); // arrived from the second season

        Assert.Equal(["tt2560140"], dto.ImdbIds);
    }

    [Fact]
    public async Task ARootWithNoImdbIdShowsNoneEvenWhenAnotherMemberHasOne()
    {
        using var db = CreateDb();
        SeedFranchise(db, rootImdbIds: null, secondSeasonImdbIds: ["tt7777777"]);
        await db.SaveChangesAsync();

        var dto = await CreateService(db, new FakeTmdbClient()).GetSeriesAsync(1);

        Assert.Empty(dto.ImdbIds); // no fallback to another member's id
    }

    [Fact]
    public async Task TheRootsImdbIdsComeFromTheCustomMappingWhenTheSourceHasNone()
    {
        // design.md D19: the series read sees the merged view, for the IMDb link and for the match.
        using var db = CreateDb();
        AddAnime(db, 1, withMapping: false);
        AddSeries(db, 1, [1]);
        await db.SaveChangesAsync();
        var custom = new FakeCustomIdMappings(FakeCustomIdMappings.Fill(1, tv: 280564, season: 1, imdb: ["tt38754770"]));

        var dto = await CreateService(db, new FakeTmdbClient(), custom: custom).GetSeriesAsync(1);

        Assert.Equal(["tt38754770"], dto.ImdbIds);
        Assert.True(dto.Tmdb.HasMapping);
    }

    [Fact]
    public async Task SeveralRootImdbIdsKeepTheMappingsOrder()
    {
        using var db = CreateDb();
        SeedFranchise(db, rootImdbIds: ["tt0000002", "tt0000001"]);
        await db.SaveChangesAsync();

        var dto = await CreateService(db, new FakeTmdbClient()).GetSeriesAsync(1);

        Assert.Equal(["tt0000002", "tt0000001"], dto.ImdbIds);
    }

    // --- A stored choice and the MyAnimeList option list (design.md D13) ---

    [Fact]
    public async Task ATmdbChoiceStaysOutOfTheMyAnimeListPictureOptions()
    {
        using var db = CreateDb();
        var chosen = TmdbImageUrl.Original("/tv.jpg");
        SeedFranchise(db).SelectedPictureUrl = chosen;
        SeedAllSetsFresh(db);
        await db.SaveChangesAsync();

        var dto = await CreateService(db, new FakeTmdbClient()).GetSeriesAsync(1);

        Assert.Equal(chosen, dto.SelectedPictureUrl); // still the series' choice
        Assert.DoesNotContain(chosen, dto.PictureOptions); // but the TMDB sections offer it, not the MyAnimeList list
    }

    [Fact]
    public async Task AMalChoiceOutsideThePoolIsStillAppendedToThePictureOptions()
    {
        using var db = CreateDb();
        SeedFranchise(db).SelectedPictureUrl = "https://mal/no-longer-listed.jpg";
        await db.SaveChangesAsync();

        var dto = await CreateService(db, new FakeTmdbClient()).GetSeriesAsync(1);

        Assert.Contains("https://mal/no-longer-listed.jpg", dto.PictureOptions); // a stored MAL choice is never re-validated away
    }

    // These tests don't score or schedule anything, so an empty ranking and a
    // schedule that is never asked are faithful stand-ins.
    private sealed class FakeEpisodeScheduleService : IEpisodeScheduleService
    {
        public Task<ResolvedEpisode?> ResolveOnLocalDateAsync(AnimeMetadata anime, DateOnly localDate, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<DateTimeOffset?> NextAiringInstantAsync(AnimeMetadata anime, DateTimeOffset afterUtc, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<int?> EpisodesAiredAsOfAsync(AnimeMetadata anime, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<Dictionary<int, int>> EpisodesAiredAsOfAsync(IReadOnlyCollection<AnimeMetadata> anime, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<Dictionary<int, int>> EpisodesAiredAsOfAsync(IReadOnlyCollection<int> animeIds, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    private sealed class NoOpAnimeRankingService : IAnimeRankingService
    {
        public Task<AnimeRankingSnapshot> GetSnapshotAsync(CancellationToken ct = default) =>
            Task.FromResult(AnimeRankingSnapshot.Build([], []));
        public Task<List<AnimeRankingScoreCountDto>> GetScoreCountsAsync(string mediaTypeScope, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<AnimeRankingTierDto?> GetTierAsync(int score, string mediaTypeScope, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task ApplyTierOrderAsync(List<AnimeRankingTierOrderRequest> tiers, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task MoveAdjacentAsync(int promotedAnimeId, int demotedAnimeId, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task PlaceLastInTierAsync(int animeId, int score, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }
}
