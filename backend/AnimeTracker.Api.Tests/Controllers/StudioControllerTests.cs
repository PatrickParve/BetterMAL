using AnimeTracker.Api.Controllers;
using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing.AniList;
using AnimeTracker.Api.Services.Studios;
using AnimeTracker.Api.Tests.Services.Airing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimeTracker.Api.Tests.Controllers;

// StudioController (studio-links "A studio link opens the studio's AniList page",
// add-genre-and-studio-stats task 2.6): a plain GET answers with a 302 to the studio's
// own page when its id is known and to AniList's studio search otherwise, so a studio
// link is never broken.
public class StudioControllerTests
{
    private static async Task<(StudioController Controller, ScriptedAniListClient AniList, AnimeTrackerDbContext Db)> CreateAsync(
        params (string Name, int? Id)[] stored)
    {
        var db = AiringRefreshTestKit.CreateDb();
        foreach (var (name, id) in stored)
            db.StudioLinks.Add(new StudioLink { Name = name, AniListStudioId = id });
        await db.SaveChangesAsync();

        var aniList = new ScriptedAniListClient();
        var resolver = new StudioLinkResolver(db, aniList, new StudioLinkStore(db), NullLogger<StudioLinkResolver>.Instance);
        var controller = new StudioController(resolver)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
        return (controller, aniList, db);
    }

    [Fact]
    public async Task AKnownStudioRedirectsToItsOwnPage()
    {
        var (controller, aniList, db) = await CreateAsync(("MAPPA", 569));
        using var _ = db;

        var result = Assert.IsType<RedirectResult>(await controller.Open("MAPPA", CancellationToken.None));

        Assert.Equal("https://anilist.co/studio/569", result.Url);
        Assert.False(result.Permanent); // a 302: what the link leads to changes as the app learns
        Assert.Empty(aniList.StudioReads);
    }

    [Fact]
    public async Task AnUnmatchedStudioRedirectsToTheSearchWithTheNameEncoded()
    {
        var (controller, aniList, db) = await CreateAsync(("Kyoto Animation & Co", null));
        using var _ = db;

        var result = Assert.IsType<RedirectResult>(await controller.Open("Kyoto Animation & Co", CancellationToken.None));

        Assert.Equal("https://anilist.co/search/studios?search=Kyoto%20Animation%20%26%20Co", result.Url);
        Assert.Empty(aniList.StudioReads);
    }

    [Fact]
    public async Task AStudioNobodyCreditsRedirectsToTheSearchWithAnAccentedNameEncoded()
    {
        var (controller, aniList, db) = await CreateAsync();
        using var _ = db;

        var result = Assert.IsType<RedirectResult>(await controller.Open("Diomedéa", CancellationToken.None));

        Assert.Equal("https://anilist.co/search/studios?search=Diomed%C3%A9a", result.Url);
        Assert.Empty(aniList.StudioReads); // no stored anime credits it, so there is nothing to ask about
    }

    [Fact]
    public async Task AnUnknownStudioIsFoundAndSentToItsPage()
    {
        var (controller, aniList, db) = await CreateAsync();
        using var _ = db;
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 10, Title = "Anime 10", Studios = ["MAPPA"] });
        await db.SaveChangesAsync();
        aniList.Add(10).Studios = [new AniListStudio(569, "MAPPA")];

        var result = Assert.IsType<RedirectResult>(await controller.Open("MAPPA", CancellationToken.None));

        Assert.Equal("https://anilist.co/studio/569", result.Url);
        Assert.Single(aniList.StudioReads);
    }

    [Fact]
    public async Task TheRedirectIsNeverCached()
    {
        var (controller, _, db) = await CreateAsync(("MAPPA", 569));
        using var _ = db;

        await controller.Open("MAPPA", CancellationToken.None);

        Assert.Equal("no-store", controller.Response.Headers.CacheControl.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ABlankNameIsABadRequest(string? name)
    {
        var (controller, aniList, db) = await CreateAsync();
        using var _ = db;

        Assert.IsType<BadRequestObjectResult>(await controller.Open(name, CancellationToken.None));

        Assert.Empty(aniList.StudioReads);
    }

    [Fact]
    public async Task ANameOverTwoHundredCharactersIsABadRequest()
    {
        var (controller, aniList, db) = await CreateAsync();
        using var _ = db;

        Assert.IsType<BadRequestObjectResult>(await controller.Open(new string('x', 201), CancellationToken.None));
        Assert.IsType<RedirectResult>(await controller.Open(new string('x', 200), CancellationToken.None));

        Assert.Empty(aniList.StudioReads);
    }
}
