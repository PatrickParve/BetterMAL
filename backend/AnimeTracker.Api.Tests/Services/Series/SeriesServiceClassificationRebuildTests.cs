using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Infrastructure;
using AnimeTracker.Api.Services.Metadata;
using AnimeTracker.Api.Services.Relations;
using AnimeTracker.Api.Services.Series;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SeriesModel = AnimeTracker.Api.Models.Series;

namespace AnimeTracker.Api.Tests.Services.Series;

// SeriesService.NeedsBuild's ClassificationRevisedAt clause (design.md
// decision 3/task 3.1): a stored series built before the current main-line
// classification rules took effect is rebuilt on its next read, so a
// correction reaches already-stored series without the user having to find
// and rebuild each one by hand.
public class SeriesServiceClassificationRebuildTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static AnimeMetadata Anime(int id, DateOnly airedFrom) => new()
    {
        Id = id,
        Title = $"Anime {id}",
        MediaType = "tv",
        AiredFrom = airedFrom,
    };

    private static SeriesService CreateService(AnimeTrackerDbContext db) =>
        new(
            db,
            new SeriesGraphBuilder(db, new FakeMetadataRefreshService(), new RelationResolver(db), NullLogger<SeriesGraphBuilder>.Instance),
            new RefreshGate(),
            new FakeEpisodeScheduleService());

    [Fact]
    public async Task SeriesBuiltBeforeClassificationRevisionIsRebuiltOnRead()
    {
        using var db = CreateDb();
        var seasonOne = Anime(1, new DateOnly(2018, 1, 1));
        var seasonTwo = Anime(2, new DateOnly(2019, 1, 1));
        seasonOne.RelatedAnime.Add(new AnimeRelatedAnime { AnimeId = 1, RelatedAnimeId = 2, RelationType = "sequel", Title = "Season Two" });
        db.AnimeMetadata.AddRange(seasonOne, seasonTwo);

        var staleBuiltAt = SeriesGraphBuilder.ClassificationRevisedAt - TimeSpan.FromDays(1);
        db.Series.Add(new SeriesModel { Id = 1, RootAnimeId = 1, BuiltAt = staleBuiltAt });
        db.SeriesMembers.Add(new SeriesMember { AnimeId = 1, SeriesId = 1, IsMainLine = true, Order = 0 });
        db.SeriesMembers.Add(new SeriesMember { AnimeId = 2, SeriesId = 1, IsMainLine = true, Order = 1 });
        await db.SaveChangesAsync();

        await CreateService(db).GetSeriesAsync(1);

        var rebuilt = await db.Series.AsNoTracking().SingleAsync(s => s.Id == 1);
        Assert.True(rebuilt.BuiltAt >= SeriesGraphBuilder.ClassificationRevisedAt);
    }

    [Fact]
    public async Task SeriesBuiltAfterClassificationRevisionIsServedFromStorage()
    {
        using var db = CreateDb();
        var seasonOne = Anime(1, new DateOnly(2018, 1, 1));
        var seasonTwo = Anime(2, new DateOnly(2019, 1, 1));
        db.AnimeMetadata.AddRange(seasonOne, seasonTwo);

        var freshBuiltAt = SeriesGraphBuilder.ClassificationRevisedAt + TimeSpan.FromDays(1);
        db.Series.Add(new SeriesModel { Id = 1, RootAnimeId = 1, BuiltAt = freshBuiltAt });
        db.SeriesMembers.Add(new SeriesMember { AnimeId = 1, SeriesId = 1, IsMainLine = true, Order = 0 });
        db.SeriesMembers.Add(new SeriesMember { AnimeId = 2, SeriesId = 1, IsMainLine = false, Order = 0 });
        await db.SaveChangesAsync();

        await CreateService(db).GetSeriesAsync(1);

        var stored = await db.Series.AsNoTracking().SingleAsync(s => s.Id == 1);
        Assert.Equal(freshBuiltAt, stored.BuiltAt); // unchanged: no rebuild happened
    }

    private sealed class FakeMetadataRefreshService : IMetadataRefreshService
    {
        public Task<int> RefreshStaleBatchAsync(int batchSize, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task RefreshOneAsync(int animeId, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

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
}
