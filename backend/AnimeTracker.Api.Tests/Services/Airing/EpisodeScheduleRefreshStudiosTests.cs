using System.Text.Json;
using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Airing.AniList;
using Microsoft.EntityFrameworkCore;
using Kit = AnimeTracker.Api.Tests.Services.Airing.AiringRefreshTestKit;

namespace AnimeTracker.Api.Tests.Services.Airing;

// Studios ride along in the AniList lookups the refresh already makes (studio-links
// "Studio ids are learned from AniList's record of an anime", add-genre-and-studio-stats
// task 2.6). The lookup asks for them only while an anime it covers credits a studio with
// no stored id, learns matches and nothing else, and changes nothing else it stores.
public class EpisodeScheduleRefreshStudiosTests
{
    private static async Task SeedAsync(AnimeTrackerDbContext db, int id, params string[] studios)
    {
        await Kit.SeedAsync(db, id);
        var anime = await db.AnimeMetadata.SingleAsync(a => a.Id == id);
        anime.Studios = studios.ToList();
        await db.SaveChangesAsync();
    }

    private static async Task StoreAsync(AnimeTrackerDbContext db, string name, int? aniListStudioId)
    {
        db.StudioLinks.Add(new StudioLink { Name = name, AniListStudioId = aniListStudioId });
        await db.SaveChangesAsync();
    }

    private static Task<RefreshBatchResult> RefreshAsync(EpisodeScheduleRefreshService service, params int[] ids) =>
        service.RefreshBatchAsync(ids, new RefreshBatchOptions());

    private static async Task<Dictionary<string, int?>> LinksAsync(AnimeTrackerDbContext db) =>
        await db.StudioLinks.AsNoTracking().ToDictionaryAsync(l => l.Name, l => l.AniListStudioId);

    // Everything a lookup stores apart from the studio links, reduced to values that do not
    // depend on the clock, so two runs can be compared.
    private static async Task<string> StoredStateAsync(AnimeTrackerDbContext db)
    {
        var syncs = await db.AnimeAiringSyncs.AsNoTracking().OrderBy(s => s.AnimeId)
            .Select(s => new
            {
                s.AnimeId, s.AniListId, Fetched = s.LastFetchedAt != null, RelationsFetched = s.RelationsFetchedAt != null,
                s.NextAiringEpisodeAtUtc, s.HasCompleteData, Recheck = s.NextRecheckAtUtc != null,
            })
            .ToListAsync();
        var episodes = await db.EpisodeAirings.AsNoTracking().OrderBy(e => e.AnimeId).ThenBy(e => e.Episode)
            .Select(e => new { e.AnimeId, e.Episode, e.AirsAtUtc })
            .ToListAsync();
        var relations = await db.AniListRelations.AsNoTracking().OrderBy(r => r.AnimeId).ThenBy(r => r.RelatedAnimeId)
            .Select(r => new { r.AnimeId, r.RelatedAnimeId, r.RelationType })
            .ToListAsync();
        var anime = await db.AnimeMetadata.AsNoTracking().OrderBy(a => a.Id)
            .Select(a => new { a.Id, a.AiringStatus, a.AniListTotalEpisodes, a.TotalEpisodes })
            .ToListAsync();
        return JsonSerializer.Serialize(new { syncs, episodes, relations, anime });
    }

    // --- Learning ---------------------------------------------------------------

    [Fact]
    public async Task ALookupBatchStoresItsAnimesMatches()
    {
        using var db = Kit.CreateDb();
        await SeedAsync(db, 1, "MAPPA", "Aniplex");
        var aniList = new ScriptedAniListClient();
        aniList.Add(1, rows: 2).Studios =
            [new AniListStudio(569, "MAPPA"), new AniListStudio(17, "Aniplex"), new AniListStudio(88, "Some Producer")];

        await RefreshAsync(Kit.CreateService(db, aniList), 1);

        // Each MyAnimeList name is paired with the same anime's AniList studio; AniList's other credits are not ours.
        Assert.Equal(new Dictionary<string, int?> { ["MAPPA"] = 569, ["Aniplex"] = 17 }, await LinksAsync(db));
        Assert.Equal([true], aniList.LookupBatchStudioFlags);
    }

    [Fact]
    public async Task ASingleLookupStoresItsAnimesMatchesToo()
    {
        using var db = Kit.CreateDb();
        await SeedAsync(db, 1, "MAPPA");
        var aniList = new ScriptedAniListClient();
        aniList.Add(1, rows: 2).Studios = [new AniListStudio(569, "MAPPA")];

        await Kit.CreateService(db, aniList).RefreshOneAsync(1);

        Assert.Equal([true], aniList.SingleLookupStudioFlags);
        Assert.Equal(569, Assert.Single(await LinksAsync(db)).Value);
    }

    [Fact]
    public async Task EverythingElseALookupStoresIsIdenticalToTheSameBatchWithNoStudiosInTheAnswer()
    {
        // Three anime: one with relations and a total, one finished, one AniList has no entry for.
        async Task<(string State, Dictionary<string, int?> Links)> RunAsync(bool aniListListsStudios)
        {
            using var db = Kit.CreateDb();
            await SeedAsync(db, 1, "MAPPA", "Aniplex");
            await SeedAsync(db, 2, "Wit Studio");
            await SeedAsync(db, 3, "Pierrot");
            var aniList = new ScriptedAniListClient();
            var first = aniList.Add(1, rows: 3, status: "RELEASING", episodes: 12);
            first.Relations = [new AniListRelationEdge("SEQUEL", 2), new AniListRelationEdge("ADAPTATION", 99)];
            first.NextAiringAt = new DateTimeOffset(2026, 12, 1, 12, 0, 0, TimeSpan.Zero);
            var second = aniList.Add(2, rows: 12, status: "FINISHED", episodes: 12);
            if (aniListListsStudios)
            {
                first.Studios = [new AniListStudio(569, "MAPPA"), new AniListStudio(17, "Aniplex")];
                second.Studios = [new AniListStudio(858, "Wit Studio")];
            }

            await RefreshAsync(Kit.CreateService(db, aniList), 1, 2, 3);

            // Both runs asked for studios, since none was stored; only the answers differ.
            Assert.Equal([true], aniList.LookupBatchStudioFlags);
            return (await StoredStateAsync(db), await LinksAsync(db));
        }

        var withStudios = await RunAsync(aniListListsStudios: true);
        var withoutStudios = await RunAsync(aniListListsStudios: false);

        Assert.Equal(withoutStudios.State, withStudios.State);
        Assert.Equal(3, withStudios.Links.Count);
        Assert.Empty(withoutStudios.Links);
    }

    // --- Asking only when something is missing --------------------------------------

    [Fact]
    public async Task ABatchWhoseStudiosAllHaveIdsIsSentWithoutStudios()
    {
        using var db = Kit.CreateDb();
        await SeedAsync(db, 1, "MAPPA");
        await SeedAsync(db, 2, "MAPPA", "Aniplex");
        await StoreAsync(db, "MAPPA", 569);
        await StoreAsync(db, "Aniplex", 17);
        var aniList = new ScriptedAniListClient();
        aniList.Add(1, rows: 1).Studios = [new AniListStudio(569, "MAPPA")];
        aniList.Add(2, rows: 1).Studios = [new AniListStudio(569, "MAPPA")];

        await RefreshAsync(Kit.CreateService(db, aniList), 1, 2);

        Assert.Equal([false], aniList.LookupBatchStudioFlags);
        Assert.Empty(aniList.StudioReads);
    }

    [Fact]
    public async Task ABatchWithASingleUnknownStudioIsSentWithStudios()
    {
        using var db = Kit.CreateDb();
        await SeedAsync(db, 1, "MAPPA");
        await SeedAsync(db, 2, "MAPPA", "Aniplex");
        await SeedAsync(db, 3, "Pierrot");
        await StoreAsync(db, "MAPPA", 569);
        await StoreAsync(db, "Pierrot", 1);
        // Aniplex alone has no stored id, out of three anime and three studios.
        var aniList = new ScriptedAniListClient();
        aniList.Add(1, rows: 1);
        aniList.Add(2, rows: 1).Studios = [new AniListStudio(17, "Aniplex")];
        aniList.Add(3, rows: 1);

        await RefreshAsync(Kit.CreateService(db, aniList), 1, 2, 3);

        Assert.Equal([true], aniList.LookupBatchStudioFlags);
        Assert.Equal(17, (await LinksAsync(db))["Aniplex"]);
    }

    [Fact]
    public async Task AnUnmatchedStudioCountsAsHavingNoIdSoTheLookupAsksAgain()
    {
        using var db = Kit.CreateDb();
        await SeedAsync(db, 1, "Obscure Studio");
        await StoreAsync(db, "Obscure Studio", null);
        var aniList = new ScriptedAniListClient();
        aniList.Add(1, rows: 1);

        await RefreshAsync(Kit.CreateService(db, aniList), 1);

        Assert.Equal([true], aniList.LookupBatchStudioFlags);
    }

    [Fact]
    public async Task AnAnimeWithNoStudiosAsksForNone()
    {
        using var db = Kit.CreateDb();
        await Kit.SeedAsync(db, 1); // no studios stored for it
        var aniList = new ScriptedAniListClient();
        aniList.Add(1, rows: 1);

        await RefreshAsync(Kit.CreateService(db, aniList), 1);

        Assert.Equal([false], aniList.LookupBatchStudioFlags);
    }

    [Fact]
    public async Task AddingAnAnimeByAKnownStudioMakesNoStudioRequestOfAnyKind()
    {
        using var db = Kit.CreateDb();
        await SeedAsync(db, 1, "MAPPA"); // the new anime, right after it was added
        await StoreAsync(db, "MAPPA", 569);
        var aniList = new ScriptedAniListClient();
        aniList.Add(1, rows: 2).Studios = [new AniListStudio(569, "MAPPA")];

        await Kit.CreateService(db, aniList).RefreshOneAsync(1);

        Assert.Equal([1], aniList.SingleLookups); // the lookup the app makes anyway
        Assert.Equal([false], aniList.SingleLookupStudioFlags); // and it did not ask for studios
        Assert.Empty(aniList.StudioReads);
        Assert.Equal(569, Assert.Single(await LinksAsync(db)).Value);
    }

    // --- A lookup records matches and nothing else ----------------------------------------

    [Fact]
    public async Task ALookupNeverRecordsANameAsUnmatchedWhenAniListListsOtherStudios()
    {
        using var db = Kit.CreateDb();
        await SeedAsync(db, 1, "MAPPA");
        var aniList = new ScriptedAniListClient();
        aniList.Add(1, rows: 1).Studios = [new AniListStudio(17, "Aniplex")];

        await RefreshAsync(Kit.CreateService(db, aniList), 1);

        Assert.Empty(await LinksAsync(db));
    }

    [Fact]
    public async Task ALookupNeverRecordsANameAsUnmatchedWhenAniListListsNoStudioForTheAnime()
    {
        using var db = Kit.CreateDb();
        await SeedAsync(db, 1, "MAPPA");
        var aniList = new ScriptedAniListClient();
        aniList.Add(1, rows: 1); // AniList holds the anime and credits no studio to it yet

        await RefreshAsync(Kit.CreateService(db, aniList), 1);

        Assert.Empty(await LinksAsync(db));
    }

    [Fact]
    public async Task ALookupLeavesARecordTheNameAlreadyHasAsItWas()
    {
        using var db = Kit.CreateDb();
        await SeedAsync(db, 1, "MAPPA", "Obscure Studio", "New Studio");
        await StoreAsync(db, "MAPPA", 569);
        await StoreAsync(db, "Obscure Studio", null);
        var aniList = new ScriptedAniListClient();
        aniList.Add(1, rows: 1).Studios = [new AniListStudio(17, "Aniplex")]; // none of the three is listed

        await RefreshAsync(Kit.CreateService(db, aniList), 1);

        Assert.Equal(
            new Dictionary<string, int?> { ["MAPPA"] = 569, ["Obscure Studio"] = null },
            await LinksAsync(db)); // no record made for "New Studio" either
    }

    [Fact]
    public async Task ALaterLookupThatMatchesAnUnmatchedNameFillsItIn()
    {
        using var db = Kit.CreateDb();
        await SeedAsync(db, 1, "Obscure Studio");
        await StoreAsync(db, "Obscure Studio", null);
        var aniList = new ScriptedAniListClient();
        aniList.Add(1, rows: 1).Studios = [new AniListStudio(4242, "Obscure Studio")];

        await RefreshAsync(Kit.CreateService(db, aniList), 1);

        Assert.Equal([true], aniList.LookupBatchStudioFlags); // an unmatched record has no id, so studios were asked for
        Assert.Equal(4242, Assert.Single(await LinksAsync(db)).Value);
        Assert.Empty(aniList.StudioReads); // at no extra request
    }

    [Fact]
    public async Task AStoredIdIsNeverReplacedByALaterLookupThatListsADifferentOne()
    {
        using var db = Kit.CreateDb();
        await SeedAsync(db, 1, "MAPPA", "Aniplex");
        await StoreAsync(db, "MAPPA", 569);
        var aniList = new ScriptedAniListClient();
        aniList.Add(1, rows: 1).Studios = [new AniListStudio(999, "MAPPA"), new AniListStudio(17, "Aniplex")];

        await RefreshAsync(Kit.CreateService(db, aniList), 1);

        Assert.Equal(new Dictionary<string, int?> { ["MAPPA"] = 569, ["Aniplex"] = 17 }, await LinksAsync(db));
    }

    [Fact]
    public async Task AFailedLookupRecordsNothing()
    {
        using var db = Kit.CreateDb();
        await SeedAsync(db, 1, "MAPPA");
        var aniList = new ScriptedAniListClient { Fail = (what, _) => what == "lookup" ? new HttpRequestException("AniList is down") : null };
        aniList.Add(1, rows: 1).Studios = [new AniListStudio(569, "MAPPA")];

        var result = await RefreshAsync(Kit.CreateService(db, aniList), 1);

        Assert.Equal(1, result.Failed);
        Assert.Empty(await LinksAsync(db));
    }
}
