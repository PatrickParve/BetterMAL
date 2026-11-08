using System.Diagnostics;
using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing.AniList;
using AnimeTracker.Api.Services.Studios;
using AnimeTracker.Api.Tests.Services.Airing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimeTracker.Api.Tests.Services.Studios;

// StudioLinkResolver (studio-links "A studio link opens the studio's AniList page",
// add-genre-and-studio-stats task 2.6), over ScriptedAniListClient: a stored id or an
// unmatched record answers with no request; a name never asked about makes one request
// about stored anime crediting it; and only an answer that lists studios is held against
// the studio. No network is involved anywhere.
public class StudioLinkResolverTests
{
    private static AnimeTrackerDbContext CreateDb() => AiringRefreshTestKit.CreateDb();

    private static StudioLinkResolver CreateResolver(AnimeTrackerDbContext db, ScriptedAniListClient aniList) =>
        new(db, aniList, new StudioLinkStore(db), NullLogger<StudioLinkResolver>.Instance);

    // An anime crediting the given studios, optionally in my list and optionally with a
    // stored AniList id (an anime AniList is known to hold).
    private static async Task SeedAsync(
        AnimeTrackerDbContext db, int animeId, string[] studios, bool inList = false, int? aniListId = null)
    {
        db.AnimeMetadata.Add(new AnimeMetadata { Id = animeId, Title = $"Anime {animeId}", Studios = studios.ToList() });
        if (inList)
            db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = animeId, Status = WatchStatus.Completed });
        if (aniListId is { } id)
            db.AnimeAiringSyncs.Add(new AnimeAiringSync { AnimeId = animeId, AniListId = id, LastFetchedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
    }

    private static async Task<StudioLink?> RecordAsync(AnimeTrackerDbContext db, string name) =>
        await db.StudioLinks.AsNoTracking().FirstOrDefaultAsync(l => l.Name == name);

    [Fact]
    public async Task AKnownNameMakesNoRequest()
    {
        using var db = CreateDb();
        await SeedAsync(db, 1, ["MAPPA"], inList: true);
        db.StudioLinks.Add(new StudioLink { Name = "MAPPA", AniListStudioId = 569 });
        await db.SaveChangesAsync();
        var aniList = new ScriptedAniListClient();

        var id = await CreateResolver(db, aniList).ResolveAsync("MAPPA");

        Assert.Equal(569, id);
        Assert.Empty(aniList.StudioReads);
    }

    [Fact]
    public async Task AnUnknownNameMakesOneRequestIsStoredAndTheNextCallMakesNone()
    {
        using var db = CreateDb();
        await SeedAsync(db, 10, ["MAPPA"], inList: true);
        var aniList = new ScriptedAniListClient();
        aniList.Add(10).Studios = [new AniListStudio(569, "MAPPA"), new AniListStudio(17, "Aniplex")];
        var resolver = CreateResolver(db, aniList);

        var first = await resolver.ResolveAsync("MAPPA");
        var second = await resolver.ResolveAsync("MAPPA");

        Assert.Equal(569, first);
        Assert.Equal(569, second);
        Assert.Equal([10], Assert.Single(aniList.StudioReads));
        Assert.Equal(569, (await RecordAsync(db, "MAPPA"))!.AniListStudioId);
    }

    [Fact]
    public async Task AnAnswerListingStudiosNoneOfThemTheNameRecordsItAsUnmatchedAndTheNextCallMakesNoRequest()
    {
        using var db = CreateDb();
        await SeedAsync(db, 10, ["Obscure Studio"], inList: true);
        var aniList = new ScriptedAniListClient();
        aniList.Add(10).Studios = [new AniListStudio(17, "Aniplex")];
        var resolver = CreateResolver(db, aniList);

        var first = await resolver.ResolveAsync("Obscure Studio");
        var second = await resolver.ResolveAsync("Obscure Studio");

        Assert.Null(first);
        Assert.Null(second);
        Assert.Single(aniList.StudioReads); // asked once only
        var record = await RecordAsync(db, "Obscure Studio");
        Assert.NotNull(record);
        Assert.Null(record.AniListStudioId);
    }

    [Fact]
    public async Task AnAnswerListingNoStudioAtAllRecordsNothingAndTheNextCallAsksAgain()
    {
        using var db = CreateDb();
        await SeedAsync(db, 10, ["MAPPA"], inList: true);
        var aniList = new ScriptedAniListClient();
        aniList.Add(10); // AniList holds the anime and credits no studio to it yet
        var resolver = CreateResolver(db, aniList);

        Assert.Null(await resolver.ResolveAsync("MAPPA"));
        Assert.Null(await RecordAsync(db, "MAPPA"));

        // AniList now lists the studio, and the next click finds it.
        aniList.Add(10).Studios = [new AniListStudio(569, "MAPPA")];
        Assert.Equal(569, await resolver.ResolveAsync("MAPPA"));
        Assert.Equal(2, aniList.StudioReads.Count);
    }

    [Fact]
    public async Task AnAnswerThatHoldsNoneOfTheAnimeRecordsNothingAndTheNextCallAsksAgain()
    {
        using var db = CreateDb();
        await SeedAsync(db, 10, ["MAPPA"], inList: true);
        var aniList = new ScriptedAniListClient(); // AniList holds no media for it at all
        var resolver = CreateResolver(db, aniList);

        Assert.Null(await resolver.ResolveAsync("MAPPA"));
        Assert.Null(await resolver.ResolveAsync("MAPPA"));

        Assert.Null(await RecordAsync(db, "MAPPA"));
        Assert.Equal(2, aniList.StudioReads.Count);
    }

    [Fact]
    public async Task AFailureReturnsNullRecordsNothingAndTheNextCallAsksAgain()
    {
        using var db = CreateDb();
        await SeedAsync(db, 10, ["MAPPA"], inList: true);
        var aniList = new ScriptedAniListClient();
        aniList.Add(10).Studios = [new AniListStudio(569, "MAPPA")];
        aniList.Fail = (what, _) => what == "studios" ? new HttpRequestException("AniList is down") : null;
        var resolver = CreateResolver(db, aniList);

        Assert.Null(await resolver.ResolveAsync("MAPPA"));
        Assert.Null(await RecordAsync(db, "MAPPA"));

        aniList.Fail = null;
        Assert.Equal(569, await resolver.ResolveAsync("MAPPA"));
        Assert.Equal(2, aniList.StudioReads.Count);
    }

    [Fact]
    public async Task AnAnswerSlowerThanFiveSecondsReturnsNullRecordsNothingAndTheNextCallAsksAgain()
    {
        using var db = CreateDb();
        await SeedAsync(db, 10, ["MAPPA"], inList: true);
        var aniList = new ScriptedAniListClient();
        aniList.Add(10).Studios = [new AniListStudio(569, "MAPPA")];
        aniList.BeforeStudiosAnswer = ct => Task.Delay(Timeout.Infinite, ct); // AniList never answers
        var resolver = CreateResolver(db, aniList);

        // WaitAsync only guards the test: without the cap this call would never come back.
        var clock = Stopwatch.StartNew();
        var id = await resolver.ResolveAsync("MAPPA").WaitAsync(TimeSpan.FromSeconds(20));
        clock.Stop();

        Assert.Null(id);
        Assert.InRange(clock.Elapsed, TimeSpan.FromSeconds(4.5), TimeSpan.FromSeconds(12));
        Assert.Null(await RecordAsync(db, "MAPPA"));

        aniList.BeforeStudiosAnswer = null;
        Assert.Equal(569, await resolver.ResolveAsync("MAPPA"));
        Assert.Equal(2, aniList.StudioReads.Count);
    }

    [Fact]
    public async Task AnAskerWhoGoesAwayIsNotMistakenForASlowAnswer()
    {
        using var db = CreateDb();
        await SeedAsync(db, 10, ["MAPPA"], inList: true);
        var aniList = new ScriptedAniListClient();
        aniList.Add(10).Studios = [new AniListStudio(569, "MAPPA")];
        using var caller = new CancellationTokenSource();
        aniList.BeforeStudiosAnswer = async ct =>
        {
            await caller.CancelAsync();
            await Task.Delay(Timeout.Infinite, ct);
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateResolver(db, aniList).ResolveAsync("MAPPA", caller.Token));

        Assert.Null(await RecordAsync(db, "MAPPA"));
    }

    [Fact]
    public async Task ANameNoStoredAnimeCreditsMakesNoRequestAndRecordsNothing()
    {
        using var db = CreateDb();
        await SeedAsync(db, 10, ["Aniplex"], inList: true);
        var aniList = new ScriptedAniListClient();

        Assert.Null(await CreateResolver(db, aniList).ResolveAsync("MAPPA"));

        Assert.Empty(aniList.StudioReads);
        Assert.Null(await RecordAsync(db, "MAPPA"));
    }

    [Fact]
    public async Task AnUnmatchedRecordAnswersWithNoRequest()
    {
        using var db = CreateDb();
        await SeedAsync(db, 10, ["Obscure Studio"], inList: true);
        db.StudioLinks.Add(new StudioLink { Name = "Obscure Studio", AniListStudioId = null });
        await db.SaveChangesAsync();
        var aniList = new ScriptedAniListClient();

        Assert.Null(await CreateResolver(db, aniList).ResolveAsync("Obscure Studio"));

        Assert.Empty(aniList.StudioReads);
    }

    [Fact]
    public async Task OneRequestAsksAboutUpToFiveAnimeMyListFirstThenThoseAniListHoldsThenByNumber()
    {
        using var db = CreateDb();
        // Eight anime credit MAPPA: 7 and 8 are in my list; 5 and 6 have a stored AniList id; 1-4 have neither.
        foreach (var id in new[] { 1, 2, 3, 4 })
            await SeedAsync(db, id, ["MAPPA"]);
        foreach (var id in new[] { 5, 6 })
            await SeedAsync(db, id, ["MAPPA"], aniListId: 900 + id);
        foreach (var id in new[] { 7, 8 })
            await SeedAsync(db, id, ["MAPPA"], inList: true);
        await SeedAsync(db, 9, ["Aniplex"], inList: true); // credits another studio: never asked about
        var aniList = new ScriptedAniListClient();

        await CreateResolver(db, aniList).ResolveAsync("MAPPA");

        Assert.Equal([7, 8, 5, 6, 1], Assert.Single(aniList.StudioReads));
    }

    [Fact]
    public async Task TheNameIsMatchedAnimeByAnimeWithinEachAnimesOwnCredits()
    {
        using var db = CreateDb();
        await SeedAsync(db, 1, ["MAPPA"], inList: true);
        await SeedAsync(db, 2, ["MAPPA"], inList: true);
        var aniList = new ScriptedAniListClient();
        aniList.Add(1).Studios = [new AniListStudio(858, "Wit Studio")]; // AniList credits another studio here
        aniList.Add(2).Studios = [new AniListStudio(569, "MAPPA")];

        var id = await CreateResolver(db, aniList).ResolveAsync("MAPPA");

        Assert.Equal(569, id);
        Assert.Equal(569, (await RecordAsync(db, "MAPPA"))!.AniListStudioId);
    }

    [Fact]
    public async Task AMatchThroughOneAnimeSpellingTheNameAnotherWayStillMatches()
    {
        using var db = CreateDb();
        await SeedAsync(db, 1, ["J.C.Staff"], inList: true);
        var aniList = new ScriptedAniListClient();
        aniList.Add(1).Studios = [new AniListStudio(16, "J.C. Staff")];

        Assert.Equal(16, await CreateResolver(db, aniList).ResolveAsync("J.C.Staff"));
    }

    [Fact]
    public async Task AFollowedLinkMatchingAnUnmatchedNameIsNotNeededButALaterLookupCan()
    {
        // A name recorded as unmatched is answered with no request (the click path), and a
        // lookup of another anime by that studio fills the id in through the store.
        using var db = CreateDb();
        await SeedAsync(db, 1, ["Obscure Studio"], inList: true);
        var store = new StudioLinkStore(db);
        await store.RecordAsync("Obscure Studio", aniListStudioId: null);
        var aniList = new ScriptedAniListClient();
        var resolver = CreateResolver(db, aniList);
        Assert.Null(await resolver.ResolveAsync("Obscure Studio"));

        await store.LearnAsync(["Obscure Studio"], [new AniListStudio(4242, "Obscure Studio")]);

        Assert.Equal(4242, await resolver.ResolveAsync("Obscure Studio"));
        Assert.Empty(aniList.StudioReads);
    }
}
