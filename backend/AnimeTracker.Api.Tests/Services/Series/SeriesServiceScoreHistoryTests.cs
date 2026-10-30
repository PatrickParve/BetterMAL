using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Infrastructure;
using AnimeTracker.Api.Services.Ranking;
using AnimeTracker.Api.Services.Series;
using AnimeTracker.Api.Tests.Services.IdMapping;
using AnimeTracker.Api.Tests.Services.ScoreHistory;
using AnimeTracker.Api.Tests.Services.Tmdb;
using Microsoft.EntityFrameworkCore;
using SeriesModel = AnimeTracker.Api.Models.Series;

namespace AnimeTracker.Api.Tests.Services.Series;

// add-score-history-graph task 4.4/4.5: the series page read carries
// HasScoreHistory, computed from the main line's records by the same builder the
// overlay's read uses. Each test reads a stored, freshly built series, so the
// projection runs and the graph builder never does.
public class SeriesServiceScoreHistoryTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    // graphBuilder is never reached: a series built just now and not partial needs no build.
    private static SeriesService CreateService(AnimeTrackerDbContext db) =>
        new(db, graphBuilder: null!, new RefreshGate(), new FakeEpisodeScheduleService(), new NoOpAnimeRankingService(),
            TmdbTestData.ArtworkService(db, new FakeTmdbClient(), apiKey: ""), TestIdMappings.Resolver(db),
            ScoreHistoryServiceTestKit.Create(db));

    // Series 1: a main line of anime 1 and 2 (all finished) and an extra, anime 3.
    private static void SeedSeries(AnimeTrackerDbContext db)
    {
        db.Series.Add(new SeriesModel { Id = 1, BuiltAt = DateTimeOffset.UtcNow });
        foreach (var id in new[] { 1, 2, 3 })
            db.AnimeMetadata.Add(new AnimeMetadata
            {
                Id = id, Title = $"Anime {id}", AiringStatus = "finished_airing", AiredFrom = new DateOnly(2020, 1, 1), AiredTo = new DateOnly(2020, 6, 1),
            });
        db.SeriesMembers.Add(new SeriesMember { SeriesId = 1, AnimeId = 1, IsMainLine = true, IsPrimary = true, Order = 0 });
        db.SeriesMembers.Add(new SeriesMember { SeriesId = 1, AnimeId = 2, IsMainLine = true, IsPrimary = true, Order = 1 });
        db.SeriesMembers.Add(new SeriesMember { SeriesId = 1, AnimeId = 3, IsMainLine = false, IsPrimary = true, Order = 0, RelationGroup = "SideStory" });
    }

    private static void Record(AnimeTrackerDbContext db, int animeId, int daysAgo, double score) =>
        db.AnimeScoreRecords.Add(new AnimeScoreRecord { AnimeId = animeId, Score = score, DetectedAt = DateTimeOffset.UtcNow.AddDays(-daysAgo) });

    [Fact]
    public async Task ASeriesWhoseMainLineScoreHasChangedOffersScoreHistory()
    {
        using var db = CreateDb();
        SeedSeries(db);
        Record(db, 1, 10, 8.0);
        Record(db, 2, 10, 7.0);
        Record(db, 2, 2, 7.2);
        await db.SaveChangesAsync();

        var dto = await CreateService(db).GetSeriesAsync(1);

        Assert.True(dto.HasScoreHistory);
    }

    [Fact]
    public async Task ASeriesWhoseMainLineHasNotChangedOffersNone()
    {
        using var db = CreateDb();
        SeedSeries(db);
        Record(db, 1, 10, 8.0);
        Record(db, 2, 10, 7.0);
        await db.SaveChangesAsync();

        var dto = await CreateService(db).GetSeriesAsync(1);

        Assert.False(dto.HasScoreHistory);
    }

    [Fact]
    public async Task AChangeOnAnExtraAloneOffersNone()
    {
        using var db = CreateDb();
        SeedSeries(db);
        Record(db, 1, 10, 8.0);
        Record(db, 3, 10, 6.0);
        Record(db, 3, 2, 6.5); // the extra's score moved
        await db.SaveChangesAsync();

        var dto = await CreateService(db).GetSeriesAsync(1);

        Assert.False(dto.HasScoreHistory);
    }

    [Fact]
    public async Task ASeriesWithNoRecordsAtAllOffersNone()
    {
        using var db = CreateDb();
        SeedSeries(db);
        await db.SaveChangesAsync();

        Assert.False((await CreateService(db).GetSeriesAsync(1)).HasScoreHistory);
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
