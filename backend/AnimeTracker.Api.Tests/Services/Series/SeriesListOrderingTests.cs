using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Ranking;
using AnimeTracker.Api.Services.Series;
using Microsoft.EntityFrameworkCore;
using SeriesModel = AnimeTracker.Api.Models.Series;

namespace AnimeTracker.Api.Tests.Services.Series;

// SeriesListService's default order (design.md D1, spec "Series list read
// endpoint"): my main-line average descending, nulls last, then raw title
// case-insensitively — deterministic and stable across repeat reads.
public class SeriesListOrderingTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static void AddSeries(AnimeTrackerDbContext db, int animeId, string title, int? myScore)
    {
        db.Series.Add(new SeriesModel { Id = animeId, BuiltAt = DateTimeOffset.UtcNow });
        db.AnimeMetadata.Add(new AnimeMetadata { Id = animeId, Title = title, AiringStatus = "finished_airing" });
        db.SeriesMembers.Add(new SeriesMember { AnimeId = animeId, SeriesId = animeId, IsMainLine = true, Order = 0 });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = animeId, Status = WatchStatus.Completed, MyScore = myScore });
    }

    private sealed class UnusedEpisodeScheduleService : IEpisodeScheduleService
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
            Task.FromResult(new Dictionary<int, int>());
    }

    // None of these tests order by ranking — a stub returning the empty
    // ranking is enough (design.md D7/tasks.md 2.7).
    private sealed class UnusedAnimeRankingService : IAnimeRankingService
    {
        public Task<AnimeRankingSnapshot> GetSnapshotAsync(CancellationToken ct = default) =>
            Task.FromResult(AnimeRankingSnapshot.Empty);
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

    [Fact]
    public async Task DefaultOrder_MyAverageDescendingWithNullsLastThenTitle()
    {
        using var db = CreateDb();
        AddSeries(db, 100, "Zeta", myScore: 7);
        AddSeries(db, 200, "Alpha", myScore: null); // unscored — nulls last
        AddSeries(db, 300, "Beta", myScore: 9);
        await db.SaveChangesAsync();

        var service = new SeriesListService(new SeriesRankingLookup(db), new UnusedEpisodeScheduleService(), new UnusedAnimeRankingService());
        var result = await service.GetSeriesListAsync();

        Assert.Equal(["Beta", "Zeta", "Alpha"], result.Items.Select(i => i.Title));
    }

    [Fact]
    public async Task TiedAverages_BreakOnRawTitleCaseInsensitively()
    {
        using var db = CreateDb();
        AddSeries(db, 100, "banana", myScore: 8);
        AddSeries(db, 200, "Apple", myScore: 8);
        await db.SaveChangesAsync();

        var service = new SeriesListService(new SeriesRankingLookup(db), new UnusedEpisodeScheduleService(), new UnusedAnimeRankingService());
        var result = await service.GetSeriesListAsync();

        Assert.Equal(["Apple", "banana"], result.Items.Select(i => i.Title));
    }

    [Fact]
    public async Task StableAcrossTwoCallsOnUnchangedData()
    {
        using var db = CreateDb();
        AddSeries(db, 100, "Zeta", myScore: 7);
        AddSeries(db, 200, "Alpha", myScore: null);
        AddSeries(db, 300, "Beta", myScore: 9);
        await db.SaveChangesAsync();

        var service = new SeriesListService(new SeriesRankingLookup(db), new UnusedEpisodeScheduleService(), new UnusedAnimeRankingService());
        var first = await service.GetSeriesListAsync();
        var second = await service.GetSeriesListAsync();

        Assert.Equal(first.Items.Select(i => i.SeriesId), second.Items.Select(i => i.SeriesId));
    }
}
