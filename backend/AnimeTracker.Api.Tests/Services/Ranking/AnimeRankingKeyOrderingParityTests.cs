using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Ranking;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Services.Ranking;

// design.md D3's drift guard (tasks.md 1.7): the in-memory comparator and
// the IQueryable extension are two independent implementations of one
// ordering rule and must never disagree. One fixture spans every band, a mix
// of placed and never-placed hand-ordered members, and an unscored entry;
// both orderings must produce the identical anime-id sequence.
public class AnimeRankingKeyOrderingParityTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task InMemoryAndQueryableOrderingsAgree()
    {
        using var db = CreateDb();

        Seed(db, 1, "Placed ten", myScore: 10, position: 0);
        Seed(db, 2, "Zebra ten", myScore: 10);
        Seed(db, 3, "Apple ten", myScore: 10);
        Seed(db, 4, "Hand ordered nine", myScore: 9);
        Seed(db, 5, "Dropped nine", myScore: 9, status: WatchStatus.Dropped);
        Seed(db, 6, "Music nine", myScore: 9, mediaType: "music");
        Seed(db, 7, "Dropped music nine", myScore: 9, status: WatchStatus.Dropped, mediaType: "music");
        Seed(db, 8, "Plan to watch eight", myScore: 8, status: WatchStatus.PlanToWatch);
        Seed(db, 9, "Unscored", myScore: null);
        await db.SaveChangesAsync();

        var positionByAnimeId = await db.TopAnimeSelections.AsNoTracking().ToDictionaryAsync(p => p.AnimeId, p => p.Position);
        var entries = await db.UserAnimeEntries.Include(e => e.Anime).AsNoTracking().ToListAsync();

        var inMemoryOrder = AnimeRankingKey.OrderEntries(entries, positionByAnimeId).Select(e => e.AnimeId).ToList();
        var queryableOrder = await db.UserAnimeEntries.AsNoTracking()
            .OrderByRanking(db.TopAnimeSelections.AsNoTracking())
            .Select(e => e.AnimeId)
            .ToListAsync();

        Assert.Equal(9, inMemoryOrder.Count);
        Assert.Equal(inMemoryOrder, queryableOrder);
    }

    private static void Seed(
        AnimeTrackerDbContext db, int animeId, string title, int? myScore,
        WatchStatus status = WatchStatus.Completed, string? mediaType = "tv", int? position = null)
    {
        var anime = new AnimeMetadata { Id = animeId, Title = title, MediaType = mediaType, AiringStatus = "finished_airing" };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = animeId, Anime = anime, Status = status, MyScore = myScore });
        if (position is { } p)
            db.TopAnimeSelections.Add(new TopAnimeSelection { AnimeId = animeId, Anime = anime, Position = p });
    }
}
