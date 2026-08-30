using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Metadata;
using AnimeTracker.Api.Services.Relations;
using AnimeTracker.Api.Services.Series;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimeTracker.Api.Tests.Services.Series;

// SeriesGraphBuilder.TopologicalMainLineOrder's slot contraction
// (rebuild-series-by-story-component design.md decision D4, task 5.3): every
// alternative of a version slot is contracted to one node before the
// topological sort runs, so every alternative of a slot shares one Order and
// a branch entry sorts around the contracted slot rather than always after
// whichever single alternative it happens to edge to.
public class SeriesGraphBuilderVersionSlotOrderingTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static AnimeMetadata Anime(int id, string mediaType, DateOnly? airedFrom = null) => new()
    {
        Id = id,
        Title = $"Anime {id}",
        MediaType = mediaType,
        AiredFrom = airedFrom,
    };

    private static void Relate(AnimeMetadata from, AnimeMetadata to, string relationType) =>
        from.RelatedAnime.Add(new AnimeRelatedAnime
        {
            AnimeId = from.Id,
            RelatedAnimeId = to.Id,
            RelationType = relationType,
            Title = to.Title,
        });

    private static SeriesGraphBuilder CreateBuilder(AnimeTrackerDbContext db) =>
        new(db, new NoOpRefreshService(), new RelationResolver(db), NullLogger<SeriesGraphBuilder>.Instance);

    [Fact]
    public async Task BothAlternativesOfASlotShareOneOrder()
    {
        using var db = CreateDb();
        var s1 = Anime(1, "tv");
        var movie = Anime(2, "movie");
        var tvArc = Anime(3, "tv");
        var s2 = Anime(4, "tv");

        Relate(s1, movie, "sequel");
        Relate(s1, tvArc, "sequel");
        Relate(movie, tvArc, "alternative_version");
        Relate(movie, s2, "sequel");
        Relate(tvArc, s2, "sequel");

        db.AnimeMetadata.AddRange(s1, movie, tvArc, s2);
        await db.SaveChangesAsync();

        var series = await CreateBuilder(db).BuildAsync(s1.Id, fetchBudget: 0, probeBudget: 0, expandLeanMembers: false);
        var members = await db.SeriesMembers.Where(m => m.SeriesId == series!.Id).ToListAsync();

        var s1Order = members.Single(m => m.AnimeId == s1.Id).Order;
        var movieOrder = members.Single(m => m.AnimeId == movie.Id).Order;
        var tvArcOrder = members.Single(m => m.AnimeId == tvArc.Id).Order;
        var s2Order = members.Single(m => m.AnimeId == s2.Id).Order;

        Assert.Equal(movieOrder, tvArcOrder); // one shared position for the slot
        Assert.True(s1Order < movieOrder);
        Assert.True(movieOrder < s2Order); // the slot's position, not either alternative alone, is what S2 follows
    }

    // A branch entry reachable only through one alternative — never contracted
    // itself — sorts around the slot via its own edge, redirected onto the
    // contracted node: a prequel of the alternative lands before the slot's
    // shared position, not stuck between S1 and the slot only by coincidence.
    [Fact]
    public async Task ABranchPrequelIsOrderedBeforeTheSlotItBelongsTo()
    {
        using var db = CreateDb();
        var s1 = Anime(1, "tv");
        var movie = Anime(2, "movie");
        var tvArc = Anime(3, "tv");
        var s2 = Anime(4, "tv");
        var tvArcPrologue = Anime(5, "tv_special"); // prequel of tvArc alone, not of movie

        Relate(s1, movie, "sequel");
        Relate(s1, tvArc, "sequel");
        Relate(movie, tvArc, "alternative_version");
        Relate(movie, s2, "sequel");
        Relate(tvArc, s2, "sequel");
        Relate(tvArcPrologue, tvArc, "sequel"); // prologue --sequel--> tvArc: tvArc follows the prologue

        db.AnimeMetadata.AddRange(s1, movie, tvArc, s2, tvArcPrologue);
        await db.SaveChangesAsync();

        var series = await CreateBuilder(db).BuildAsync(s1.Id, fetchBudget: 0, probeBudget: 0, expandLeanMembers: false);
        var members = await db.SeriesMembers.Where(m => m.SeriesId == series!.Id).ToListAsync();

        var s1Order = members.Single(m => m.AnimeId == s1.Id).Order;
        var prologueOrder = members.Single(m => m.AnimeId == tvArcPrologue.Id).Order;
        var movieOrder = members.Single(m => m.AnimeId == movie.Id).Order;
        var tvArcOrder = members.Single(m => m.AnimeId == tvArc.Id).Order;
        var s2Order = members.Single(m => m.AnimeId == s2.Id).Order;

        Assert.Equal(movieOrder, tvArcOrder);
        Assert.True(s1Order < prologueOrder);
        Assert.True(prologueOrder < movieOrder); // before the slot's shared position, not after it
        Assert.True(movieOrder < s2Order);

        // The prologue is its own position — never contracted, unlike the
        // alternatives themselves.
        Assert.NotEqual(movieOrder, prologueOrder);
    }

    // A series with no version slot at all orders exactly as it always has:
    // contraction is a no-op when SeriesVersionSlots.Resolve finds nothing.
    [Fact]
    public async Task OrdinarySeriesWithNoSlotOrdersSequentially()
    {
        using var db = CreateDb();
        var s1 = Anime(1, "tv", new DateOnly(2013, 1, 1));
        var s2 = Anime(2, "tv", new DateOnly(2015, 1, 1));
        var movie = Anime(3, "movie", new DateOnly(2016, 1, 1));
        var s3 = Anime(4, "tv", new DateOnly(2019, 1, 1));

        Relate(s1, s2, "sequel");
        Relate(s2, movie, "sequel");
        Relate(movie, s3, "sequel");

        db.AnimeMetadata.AddRange(s1, s2, movie, s3);
        await db.SaveChangesAsync();

        var series = await CreateBuilder(db).BuildAsync(s1.Id, fetchBudget: 0, probeBudget: 0, expandLeanMembers: false);
        var members = await db.SeriesMembers.Where(m => m.SeriesId == series!.Id).ToListAsync();

        var ordered = members.OrderBy(m => m.Order).Select(m => m.AnimeId).ToList();
        Assert.Equal(new[] { s1.Id, s2.Id, movie.Id, s3.Id }, ordered);
        Assert.Equal(new[] { 0, 1, 2, 3 }, members.OrderBy(m => m.AnimeId).Select(m => m.Order));
    }

    private sealed class NoOpRefreshService : IMetadataRefreshService
    {
        public Task<int> RefreshStaleBatchAsync(int batchSize, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task RefreshOneAsync(int animeId, CancellationToken ct = default) =>
            throw new AnimeMetadataNotFoundException(animeId);
    }
}
