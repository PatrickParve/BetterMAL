using AnimeTracker.Api.Controllers;
using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Ranking;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Controllers;

// design.md D7 (tasks.md 4.1/4.2/4.4): the ranking editor's read endpoint
// and its save endpoint's validation, exercised through the real
// AnimeRankingService over an in-memory database.
public class RankingControllerTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static RankingController CreateController(AnimeTrackerDbContext db) =>
        new(new AnimeRankingService(new UserAnimeEntryRepository(db), new TopAnimeSelectionRepository(db)));

    private static void Seed(AnimeTrackerDbContext db, int animeId, string title, int? myScore, string? mediaType = "tv")
    {
        var anime = new AnimeMetadata { Id = animeId, Title = title, MediaType = mediaType, AiringStatus = "finished_airing" };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = animeId, Anime = anime, Status = WatchStatus.Completed, MyScore = myScore });
    }

    [Fact]
    public async Task UnknownMediaTypeIsRejected()
    {
        using var db = CreateDb();
        var controller = CreateController(db);

        var result = await controller.Get("not-a-scope", null, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task UnknownScoreIsRejected()
    {
        using var db = CreateDb();
        Seed(db, 1, "Only an 8", myScore: 8);
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        var result = await controller.Get("all", 3, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task AnOmittedScoreReturnsTheHighestNonEmptyTier()
    {
        using var db = CreateDb();
        Seed(db, 1, "An 8", myScore: 8);
        Seed(db, 2, "A 10", myScore: 10);
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        var result = await controller.Get("all", null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<AnimeRankingResponseDto>(ok.Value);
        Assert.Equal(10, dto.Tier?.Score);
        Assert.Equal([10, 8], dto.Scores.Select(s => s.Score));
    }

    [Fact]
    public async Task ATierScoreMismatchOnSaveIsRejected()
    {
        using var db = CreateDb();
        Seed(db, 1, "An 8", myScore: 8);
        await db.SaveChangesAsync();
        var controller = CreateController(db);
        var request = new AnimeRankingOrderRequest
        {
            MediaType = "all",
            Tiers = [new AnimeRankingTierOrderRequest(9, [1])], // anime 1 is actually an 8, not a 9
        };

        var result = await controller.ReplaceOrder(request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task AValidSaveIsPersisted()
    {
        using var db = CreateDb();
        Seed(db, 1, "First", myScore: 8);
        Seed(db, 2, "Second", myScore: 8);
        await db.SaveChangesAsync();
        var controller = CreateController(db);
        var request = new AnimeRankingOrderRequest
        {
            MediaType = "all",
            Tiers = [new AnimeRankingTierOrderRequest(8, [2, 1])],
        };

        var result = await controller.ReplaceOrder(request, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        var order = await new TopAnimeSelectionRepository(db).GetOrderedAnimeIdsAsync();
        Assert.Equal([2, 1], order);
    }
}
