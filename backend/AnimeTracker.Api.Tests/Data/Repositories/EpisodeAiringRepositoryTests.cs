using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Data.Repositories;

// EpisodeAiringRepository.GetMaxAiredEpisodesAsync (tasks.md 2.1-2.4,
// episode-airing-data "Aired-episode counts are readable in bulk").
public class EpisodeAiringRepositoryTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static readonly DateTimeOffset AsOf = new(2024, 6, 1, 0, 0, 0, TimeSpan.Zero);

    private static async Task SeedAnimeAsync(AnimeTrackerDbContext db, params int[] animeIds)
    {
        foreach (var animeId in animeIds)
            db.AnimeMetadata.Add(new AnimeMetadata { Id = animeId, Title = $"Anime {animeId}" });
        await db.SaveChangesAsync();
    }

    private static async Task SeedRowsAsync(AnimeTrackerDbContext db, int animeId, params int[] episodes)
    {
        foreach (var episode in episodes)
            db.EpisodeAirings.Add(new EpisodeAiring { AnimeId = animeId, Episode = episode, AirsAtUtc = AsOf.AddDays(-episode) });
        await db.SaveChangesAsync();
    }

    // daysFromAsOf may be negative (a past row) or positive (a future row) —
    // GetNextAiringInstantsAsync's tests below need both in the same seed.
    private static async Task SeedRowAtAsync(AnimeTrackerDbContext db, int animeId, int episode, double daysFromAsOf)
    {
        db.EpisodeAirings.Add(new EpisodeAiring { AnimeId = animeId, Episode = episode, AirsAtUtc = AsOf.AddDays(daysFromAsOf) });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task ManyAnimeAreResolvedInOneRead()
    {
        using var db = CreateDb();
        await SeedAnimeAsync(db, 1, 2);
        await SeedRowsAsync(db, 1, 1, 2);
        await SeedRowsAsync(db, 2, 1, 2, 3, 4, 5);
        var repository = new EpisodeAiringRepository(db);

        var counts = await repository.GetMaxAiredEpisodesAsync([1, 2], AsOf);

        Assert.Equal(2, counts[1]);
        Assert.Equal(5, counts[2]);
    }

    [Fact]
    public async Task BulkAgreesWithTheSingleAnimeReadAtTheSameInstant()
    {
        using var db = CreateDb();
        await SeedAnimeAsync(db, 1);
        await SeedRowsAsync(db, 1, 1, 2, 3);
        var repository = new EpisodeAiringRepository(db);

        var single = await repository.GetMaxAiredEpisodeAsync(1, AsOf);
        var bulk = await repository.GetMaxAiredEpisodesAsync([1], AsOf);

        Assert.Equal(single, bulk[1]);
    }

    [Fact]
    public async Task AnAnimeWithNoStoredRowsIsAbsentFromTheResult()
    {
        using var db = CreateDb();
        await SeedAnimeAsync(db, 1, 99);
        await SeedRowsAsync(db, 1, 1, 2);
        var repository = new EpisodeAiringRepository(db);

        var counts = await repository.GetMaxAiredEpisodesAsync([1, 99], AsOf);

        Assert.True(counts.ContainsKey(1));
        Assert.False(counts.ContainsKey(99));
    }

    [Fact]
    public async Task AnEmptyRequestReturnsEmptyWithoutTouchingTheDatabase()
    {
        // Disposed first: any real query against `db` would throw
        // ObjectDisposedException, so a clean empty result proves the
        // database was never touched.
        var db = CreateDb();
        await db.DisposeAsync();
        var repository = new EpisodeAiringRepository(db);

        var counts = await repository.GetMaxAiredEpisodesAsync([], AsOf);

        Assert.Empty(counts);
    }

    // EpisodeAiringRepository.GetNextAiringInstantsAsync (tasks.md 4.3,
    // episode-airing-data "The next airing instant is readable in bulk").
    [Fact]
    public async Task ManyAnimeNextInstantsAreResolvedInOneRead()
    {
        using var db = CreateDb();
        await SeedAnimeAsync(db, 1, 2);
        await SeedRowAtAsync(db, 1, episode: 3, daysFromAsOf: 1);
        await SeedRowAtAsync(db, 1, episode: 4, daysFromAsOf: 2);
        await SeedRowAtAsync(db, 2, episode: 1, daysFromAsOf: 5);
        var repository = new EpisodeAiringRepository(db);

        var nextInstants = await repository.GetNextAiringInstantsAsync([1, 2], AsOf);

        Assert.Equal(AsOf.AddDays(1), nextInstants[1]);
        Assert.Equal(AsOf.AddDays(5), nextInstants[2]);
    }

    [Fact]
    public async Task BulkNextInstantAgreesWithTheSingleAnimeReadAtTheSameInstant()
    {
        using var db = CreateDb();
        await SeedAnimeAsync(db, 1);
        await SeedRowAtAsync(db, 1, episode: 3, daysFromAsOf: 1);
        await SeedRowAtAsync(db, 1, episode: 4, daysFromAsOf: 2);
        var repository = new EpisodeAiringRepository(db);

        var single = await repository.GetNextAiringInstantAsync(1, AsOf);
        var bulk = await repository.GetNextAiringInstantsAsync([1], AsOf);

        Assert.Equal(single, bulk[1]);
    }

    [Fact]
    public async Task AnAnimeWithOnlyPastRowsIsAbsentFromTheNextInstantResult()
    {
        using var db = CreateDb();
        await SeedAnimeAsync(db, 1, 2);
        await SeedRowAtAsync(db, 1, episode: 1, daysFromAsOf: -3); // only a past row
        await SeedRowAtAsync(db, 2, episode: 1, daysFromAsOf: 2); // a future row

        var repository = new EpisodeAiringRepository(db);

        var nextInstants = await repository.GetNextAiringInstantsAsync([1, 2], AsOf);

        Assert.False(nextInstants.ContainsKey(1));
        Assert.True(nextInstants.ContainsKey(2));
    }

    [Fact]
    public async Task ARowExactlyAtAfterUtcIsExcludedByStrictlyAfterSemantics()
    {
        using var db = CreateDb();
        await SeedAnimeAsync(db, 1);
        await SeedRowAtAsync(db, 1, episode: 1, daysFromAsOf: 0); // AirsAtUtc == afterUtc
        var repository = new EpisodeAiringRepository(db);

        var nextInstants = await repository.GetNextAiringInstantsAsync([1], AsOf);

        Assert.False(nextInstants.ContainsKey(1));
    }

    [Fact]
    public async Task AnEmptyNextInstantRequestReturnsEmptyWithoutTouchingTheDatabase()
    {
        // Disposed first: any real query against `db` would throw
        // ObjectDisposedException, so a clean empty result proves the
        // database was never touched.
        var db = CreateDb();
        await db.DisposeAsync();
        var repository = new EpisodeAiringRepository(db);

        var nextInstants = await repository.GetNextAiringInstantsAsync([], AsOf);

        Assert.Empty(nextInstants);
    }
}
