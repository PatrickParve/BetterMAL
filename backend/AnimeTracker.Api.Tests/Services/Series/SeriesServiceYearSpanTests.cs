using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Infrastructure;
using AnimeTracker.Api.Services.Ranking;
using AnimeTracker.Api.Services.Series;
using AnimeTracker.Api.Tests.Services.IdMapping;
using AnimeTracker.Api.Tests.Services.Tmdb;
using Microsoft.EntityFrameworkCore;
using SeriesModel = AnimeTracker.Api.Models.Series;

namespace AnimeTracker.Api.Tests.Services.Series;

// The series page's year span (series-page "Series page header": the year span
// covers the main line only). SeriesYearSpan's arithmetic is SeriesYearSpanTests'
// subject and the card's side is SeriesListFiguresTests'; these tests pin that
// the page read hands SeriesYearSpan the main line and nothing else, so the
// page and the card can never show different years for one series. Each test
// reads a stored, freshly built series, so the projection runs and the graph
// builder never does.
public class SeriesServiceYearSpanTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    // graphBuilder is never reached: a series built just now and not partial needs no build.
    private static SeriesService CreateService(AnimeTrackerDbContext db) =>
        new(db, graphBuilder: null!, new RefreshGate(), new FakeEpisodeScheduleService(), new NoOpAnimeRankingService(),
            TmdbTestData.ArtworkService(db, new FakeTmdbClient(), apiKey: ""), TestIdMappings.Resolver(db));

    private static void AddAnime(AnimeTrackerDbContext db, int id, DateOnly? airedFrom, DateOnly? airedTo) =>
        db.AnimeMetadata.Add(new AnimeMetadata
        {
            Id = id, Title = $"Anime {id}", AiringStatus = "finished_airing", AiredFrom = airedFrom, AiredTo = airedTo,
        });

    private static void AddMember(AnimeTrackerDbContext db, int animeId, bool isMainLine, int order) =>
        db.SeriesMembers.Add(new SeriesMember
        {
            SeriesId = 1, AnimeId = animeId, IsMainLine = isMainLine, IsPrimary = true, Order = order,
        });

    // Series 1: a main line of 2013-2019, a 2023 film extra and a 2011 pilot extra.
    private static void SeedSeries(AnimeTrackerDbContext db)
    {
        db.Series.Add(new SeriesModel { Id = 1, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 1, new DateOnly(2013, 4, 1), new DateOnly(2013, 9, 1));
        AddAnime(db, 2, new DateOnly(2019, 1, 1), new DateOnly(2019, 6, 1));
        AddAnime(db, 3, new DateOnly(2023, 2, 1), new DateOnly(2023, 2, 1)); // film extra
        AddAnime(db, 4, new DateOnly(2011, 5, 1), new DateOnly(2011, 5, 1)); // pilot extra
        AddMember(db, 1, isMainLine: true, order: 0);
        AddMember(db, 2, isMainLine: true, order: 1);
        AddMember(db, 3, isMainLine: false, order: 0);
        AddMember(db, 4, isMainLine: false, order: 1);
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Status = WatchStatus.Completed });
    }

    [Fact]
    public async Task ExtrasOutsideTheMainLineDoNotStretchTheSpan()
    {
        using var db = CreateDb();
        SeedSeries(db);
        await db.SaveChangesAsync();

        var dto = await CreateService(db).GetSeriesAsync(1);

        Assert.Equal(2013, dto.FirstYear); // not the 2011 pilot
        Assert.Equal(2019, dto.LastYear); // not the 2023 film
    }

    [Fact]
    public async Task ThePageAndTheCardShowTheSameSpan()
    {
        using var db = CreateDb();
        SeedSeries(db);
        await db.SaveChangesAsync();

        var page = await CreateService(db).GetSeriesAsync(1);
        var index = await new SeriesRankingLookup(db).LoadAsync();
        var card = Assert.Single(index.ListedSeries([], AnimeRankingSnapshot.Empty));

        Assert.Equal(card.FirstYear, page.FirstYear);
        Assert.Equal(card.LastYear, page.LastYear);
    }

    // The same stand-ins as the other SeriesService tests: these don't score or
    // schedule anything, so an empty ranking and a schedule that is never asked
    // are faithful.
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
