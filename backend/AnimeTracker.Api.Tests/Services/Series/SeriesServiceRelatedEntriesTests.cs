using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Infrastructure;
using AnimeTracker.Api.Services.Metadata;
using AnimeTracker.Api.Services.Relations;
using AnimeTracker.Api.Services.Series;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimeTracker.Api.Tests.Services.Series;

// SeriesService's related-entry projection (split-series-by-version design.md
// D5, tasks 7.3-7.4): every anime a main-line member relates to by a relation
// the series traversal doesn't follow — character, adaptation, a
// non-companion other, any unrecognized string — is shown in the More section
// without ever being fetched, without ever duplicating a real member, and
// without entering any average, stat, or count.
public class SeriesServiceRelatedEntriesTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static AnimeMetadata Anime(int id, string mediaType, double? malScore = null) => new()
    {
        Id = id,
        Title = $"Anime {id}",
        MediaType = mediaType,
        MalScore = malScore,
    };

    private static void Relate(AnimeMetadata from, AnimeMetadata to, string relationType) =>
        from.RelatedAnime.Add(new AnimeRelatedAnime
        {
            AnimeId = from.Id,
            RelatedAnimeId = to.Id,
            RelationType = relationType,
            Title = to.Title,
            MediaType = to.MediaType,
        });

    private static SeriesService CreateService(AnimeTrackerDbContext db) =>
        new(
            db,
            new SeriesGraphBuilder(db, new NoOpRefreshService(), new RelationResolver(db), NullLogger<SeriesGraphBuilder>.Instance),
            new RefreshGate(),
            new FakeEpisodeScheduleService());

    [Fact]
    public async Task ARelatedEntryWithNoCachedRowAppearsWithoutMarkingTheSeriesPartial()
    {
        using var db = CreateDb();
        var root = Anime(1, "tv");
        var season2 = Anime(2, "tv");
        Relate(root, season2, "sequel");
        // A character relation to an anime this app has never cached at all —
        // must render from the stored relation row alone, at no fetch cost.
        root.RelatedAnime.Add(new AnimeRelatedAnime
        {
            AnimeId = root.Id, RelatedAnimeId = 99, RelationType = "character", Title = "Uncached Character Anime", MediaType = "tv",
        });

        db.AnimeMetadata.AddRange(root, season2);
        await db.SaveChangesAsync();

        var dto = await CreateService(db).GetSeriesAsync(root.Id);

        var relatedEntry = Assert.Single(dto.Extras, e => e.AnimeId == 99);
        Assert.True(relatedEntry.IsRelatedEntry);
        Assert.Equal("Uncached Character Anime", relatedEntry.Title);
        Assert.Equal(nameof(RelationGroup.Character), relatedEntry.RelationGroup);
        Assert.False(dto.IsPartial);
    }

    [Fact]
    public async Task AMemberIsNeverDuplicatedAsARelatedEntry()
    {
        using var db = CreateDb();
        var root = Anime(1, "tv");
        var season2 = Anime(2, "tv");
        Relate(root, season2, "sequel");
        // Redundant: MAL sometimes also tags an existing member with a
        // relation the traversal doesn't follow. season2 is already a member
        // (main line) — it must not also show up as a related entry.
        Relate(root, season2, "character");

        db.AnimeMetadata.AddRange(root, season2);
        await db.SaveChangesAsync();

        var dto = await CreateService(db).GetSeriesAsync(root.Id);

        Assert.Single(dto.MainLine, e => e.AnimeId == season2.Id);
        Assert.DoesNotContain(dto.Extras, e => e.AnimeId == season2.Id);
    }

    [Fact]
    public async Task RelatedEntriesDoNotEnterAveragesOrCounts()
    {
        using var db = CreateDb();
        // A lone anime with only a non-traversed relation isn't part of any
        // series at all ("not part of a series", not a one-member series) —
        // a real sequel keeps this component an actual series to project.
        var root = Anime(1, "tv", malScore: 8.0);
        var season2 = Anime(2, "tv", malScore: 8.0);
        Relate(root, season2, "sequel");
        var highScoringRelated = Anime(99, "tv", malScore: 10.0);
        Relate(root, highScoringRelated, "character");

        db.AnimeMetadata.AddRange(root, season2, highScoringRelated);
        await db.SaveChangesAsync();

        var dto = await CreateService(db).GetSeriesAsync(root.Id);

        Assert.Contains(dto.Extras, e => e.AnimeId == highScoringRelated.Id && e.IsRelatedEntry);
        Assert.Equal(8.0, dto.Scores.MalAll.Value); // unaffected by the related entry's higher score
        Assert.Equal(2, dto.Scores.MalAll.ScoredCount);
        Assert.Equal(0, dto.Stats.ExtrasCount); // the related entry is shown, not counted as a member
        Assert.Equal(2, dto.Stats.MainLineCount);
    }

    private sealed class NoOpRefreshService : IMetadataRefreshService
    {
        public Task<int> RefreshStaleBatchAsync(int batchSize, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task RefreshOneAsync(int animeId, CancellationToken ct = default) =>
            throw new AnimeMetadataNotFoundException(animeId);
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
