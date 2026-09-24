using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.IdMapping;
using Microsoft.EntityFrameworkCore;
using static AnimeTracker.Api.Tests.Services.IdMapping.FakeCustomIdMappings;

namespace AnimeTracker.Api.Tests.Services.IdMapping;

// design.md D19: the mapping as readers see it, the synced rows with the custom
// entries applied on top, over EF InMemory. The precedence rules themselves are
// AnimeIdMappingMergeTests'; these tests are about the lookups: one anime, many
// anime, and that nothing is ever written back.
public class AnimeIdMappingResolverTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static void AddSynced(
        AnimeTrackerDbContext db, int animeId, int? tv = null, int? season = null, int[]? movies = null, string[]? imdb = null) =>
        db.AnimeIdMappings.Add(new AnimeIdMapping
        {
            AnimeId = animeId,
            TmdbTvId = tv,
            TmdbSeasonNumber = season,
            TmdbMovieIds = [.. movies ?? []],
            ImdbIds = [.. imdb ?? []],
        });

    // --- One anime ---

    [Fact]
    public async Task ASyncedOnlyAnimeIsReadAsStored()
    {
        using var db = CreateDb();
        AddSynced(db, 1, tv: 10, season: 2, imdb: ["tt0000001"]);
        await db.SaveChangesAsync();

        var mapping = await TestIdMappings.Resolver(db).FindAsync(1);

        Assert.Equal(10, mapping!.TmdbTvId);
        Assert.Equal(2, mapping.TmdbSeasonNumber);
        Assert.Equal(["tt0000001"], mapping.ImdbIds);
    }

    [Fact]
    public async Task ACustomOnlyAnimeGetsItsMappingFromTheEntry()
    {
        using var db = CreateDb();
        var resolver = TestIdMappings.Resolver(db, new FakeCustomIdMappings(Fill(60568, tv: 280564, season: 1, imdb: ["tt38754770"])));

        var mapping = await resolver.FindAsync(60568);

        Assert.Equal(280564, mapping!.TmdbTvId);
        Assert.Equal(["tt38754770"], mapping.ImdbIds);
    }

    [Fact]
    public async Task ASyncedRowAndAnOverrideAreMerged()
    {
        using var db = CreateDb();
        AddSynced(db, 11741, tv: 45845, season: 1, imdb: ["tt2051178"]);
        await db.SaveChangesAsync();
        var resolver = TestIdMappings.Resolver(db, new FakeCustomIdMappings(Override(11741, tv: 45845, season: 2)));

        var mapping = await resolver.FindAsync(11741);

        Assert.Equal(2, mapping!.TmdbSeasonNumber);
        Assert.Equal(["tt2051178"], mapping.ImdbIds);
    }

    [Fact]
    public async Task AnAnimeWithNeitherHasNoMapping()
    {
        using var db = CreateDb();
        var resolver = TestIdMappings.Resolver(db, new FakeCustomIdMappings(Fill(2, tv: 20)));

        Assert.Null(await resolver.FindAsync(1));
    }

    [Fact]
    public async Task ADefaultEntryYieldsOnceTheSyncedMappingHasTheAnime()
    {
        // The source catching up: the same file, and now a synced row for the id.
        using var db = CreateDb();
        var resolver = TestIdMappings.Resolver(db, new FakeCustomIdMappings(Fill(60568, tv: 280564, season: 1)));
        Assert.Equal(280564, (await resolver.FindAsync(60568))!.TmdbTvId);

        AddSynced(db, 60568, tv: 999, season: 1);
        await db.SaveChangesAsync();

        Assert.Equal(999, (await resolver.FindAsync(60568))!.TmdbTvId);
    }

    [Fact]
    public async Task NothingIsWrittenBack()
    {
        var options = new DbContextOptionsBuilder<AnimeTrackerDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        using (var seed = new AnimeTrackerDbContext(options))
        {
            AddSynced(seed, 11741, tv: 45845, season: 1);
            await seed.SaveChangesAsync();
        }

        // A context of its own, as a request has: the lookups must leave nothing
        // tracked, so that no save could ever write a merged value back.
        using (var db = new AnimeTrackerDbContext(options))
        {
            var resolver = TestIdMappings.Resolver(db, new FakeCustomIdMappings(
                Override(11741, tv: 45845, season: 2), Fill(60568, tv: 280564, season: 1)));

            await resolver.FindAsync(11741);
            await resolver.FindAsync(60568);
            await resolver.FindManyAsync([11741, 60568]);

            Assert.Empty(db.ChangeTracker.Entries());
            await db.SaveChangesAsync();
        }

        // The table is still exactly what the sync stored: the override did not
        // reach the row, and the custom-only anime got none.
        using var check = new AnimeTrackerDbContext(options);
        var rows = await check.AnimeIdMappings.AsNoTracking().ToListAsync();
        Assert.Single(rows);
        Assert.Equal(1, rows[0].TmdbSeasonNumber);
    }

    // --- Many anime ---

    [Fact]
    public async Task ManyAreReadInOneCallWithEachMergedAndTheUnmappedAbsent()
    {
        using var db = CreateDb();
        AddSynced(db, 1, tv: 10, season: 1);                      // synced only
        AddSynced(db, 2, tv: 20, season: 1, imdb: ["tt0000002"]); // synced + override
        AddSynced(db, 9, tv: 90);                                 // synced, not asked for
        await db.SaveChangesAsync();
        var resolver = TestIdMappings.Resolver(db, new FakeCustomIdMappings(
            Override(2, tv: 20, season: 3),          // corrects a synced season
            Fill(3, movies: [635302]),               // custom only
            Fill(8, tv: 80)));                       // custom only, not asked for

        var mappings = await resolver.FindManyAsync([1, 2, 3, 4]);

        Assert.Equal([1, 2, 3], mappings.Keys.Order());
        Assert.Equal(1, mappings[1].TmdbSeasonNumber);
        Assert.Equal(3, mappings[2].TmdbSeasonNumber);
        Assert.Equal(["tt0000002"], mappings[2].ImdbIds);
        Assert.Equal([635302], mappings[3].TmdbMovieIds);
    }
}
