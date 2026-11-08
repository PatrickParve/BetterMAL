using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing.AniList;
using AnimeTracker.Api.Services.Studios;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Services.Studios;

// StudioLinkStore (data-persistence "Studio link records", studio-links "Nothing
// stored is fetched again", add-genre-and-studio-stats task 2.6): a stored id is
// kept, an unmatched record is told apart from no record and filled in by a later
// match, and a lookup writes matches and nothing else.
public class StudioLinkStoreTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<Dictionary<string, int?>> RecordsAsync(AnimeTrackerDbContext db) =>
        await db.StudioLinks.AsNoTracking().ToDictionaryAsync(l => l.Name, l => l.AniListStudioId);

    [Fact]
    public async Task AMatchedStudioIsStored()
    {
        using var db = CreateDb();
        var store = new StudioLinkStore(db);

        await store.LearnAsync(["MAPPA"], [new AniListStudio(569, "MAPPA")]);

        Assert.Equal(569, (await store.FindAsync("MAPPA"))!.AniListStudioId);
    }

    [Fact]
    public async Task AStoredIdIsNeverOverwrittenByALaterLookup()
    {
        using var db = CreateDb();
        var store = new StudioLinkStore(db);
        await store.LearnAsync(["MAPPA"], [new AniListStudio(569, "MAPPA")]);

        await store.LearnAsync(["MAPPA"], [new AniListStudio(999, "MAPPA")]);

        Assert.Equal(569, (await store.FindAsync("MAPPA"))!.AniListStudioId);
    }

    [Fact]
    public async Task AStoredIdIsNeverOverwrittenOrClearedByAFollowedLink()
    {
        using var db = CreateDb();
        var store = new StudioLinkStore(db);
        await store.LearnAsync(["MAPPA"], [new AniListStudio(569, "MAPPA")]);

        await store.RecordAsync("MAPPA", 123);
        Assert.Equal(569, (await store.FindAsync("MAPPA"))!.AniListStudioId);

        await store.RecordAsync("MAPPA", aniListStudioId: null);
        Assert.Equal(569, (await store.FindAsync("MAPPA"))!.AniListStudioId);
    }

    [Fact]
    public async Task ANameFollowedLinkFoundNoMatchForIsRecordedAsUnmatched()
    {
        using var db = CreateDb();
        var store = new StudioLinkStore(db);

        await store.RecordAsync("Obscure Studio", aniListStudioId: null);

        var record = await store.FindAsync("Obscure Studio");
        Assert.NotNull(record); // told apart from a name that has no record
        Assert.Null(record.AniListStudioId);
        Assert.Null(await store.FindAsync("Never Asked"));
    }

    [Fact]
    public async Task AnUnmatchedRecordBecomesMatchedWhenALaterAnswerMatches()
    {
        using var db = CreateDb();
        var store = new StudioLinkStore(db);
        await store.RecordAsync("Obscure Studio", aniListStudioId: null);

        await store.LearnAsync(["Obscure Studio"], [new AniListStudio(4242, "Obscure Studio")]);

        Assert.Equal(4242, (await store.FindAsync("Obscure Studio"))!.AniListStudioId);
        Assert.Single(await RecordsAsync(db));
    }

    [Fact]
    public async Task AFollowedLinkThatMatchesAlsoFillsAnUnmatchedRecordIn()
    {
        using var db = CreateDb();
        var store = new StudioLinkStore(db);
        await store.RecordAsync("Obscure Studio", aniListStudioId: null);

        await store.RecordAsync("Obscure Studio", 4242);

        Assert.Equal(4242, (await store.FindAsync("Obscure Studio"))!.AniListStudioId);
    }

    [Fact]
    public async Task ALookupWritesMatchesAndNothingElse()
    {
        using var db = CreateDb();
        var store = new StudioLinkStore(db);

        await store.LearnAsync(
            ["MAPPA", "Aniplex"], [new AniListStudio(569, "MAPPA"), new AniListStudio(88, "Some Producer")]);

        // MAPPA matched; Aniplex is not on the anime's AniList record, and that is no verdict on Aniplex.
        var records = await RecordsAsync(db);
        Assert.Equal(569, Assert.Single(records).Value);
        Assert.False(records.ContainsKey("Aniplex"));
    }

    [Fact]
    public async Task ALookupOfAnAnimeAniListListsNoStudioForWritesNothing()
    {
        using var db = CreateDb();
        var store = new StudioLinkStore(db);

        await store.LearnAsync(["MAPPA"], []);

        Assert.Empty(await RecordsAsync(db));
    }

    [Fact]
    public async Task ALookupThatFindsNoMatchLeavesEveryExistingRecordAsItWas()
    {
        using var db = CreateDb();
        var store = new StudioLinkStore(db);
        await store.LearnAsync(["Matched"], [new AniListStudio(1, "Matched")]);
        await store.RecordAsync("Unmatched", aniListStudioId: null);
        var before = await RecordsAsync(db);

        // The anime credits all three, and AniList lists other studios only.
        await store.LearnAsync(["Matched", "Unmatched", "Unknown"], [new AniListStudio(2, "Elsewhere")]);

        Assert.Equal(before, await RecordsAsync(db)); // nothing changed, nothing added for "Unknown"
    }

    [Fact]
    public async Task AnyNameWithoutAnIdMeansAskingForStudios()
    {
        using var db = CreateDb();
        var store = new StudioLinkStore(db);
        await store.LearnAsync(["MAPPA", "Aniplex"], [new AniListStudio(569, "MAPPA"), new AniListStudio(17, "Aniplex")]);
        await store.RecordAsync("Obscure", aniListStudioId: null);

        Assert.False(await store.AnyWithoutIdAsync(["MAPPA", "Aniplex"]));
        Assert.False(await store.AnyWithoutIdAsync(["MAPPA", "MAPPA"])); // a repeated name is one name
        Assert.True(await store.AnyWithoutIdAsync(["MAPPA", "Never Asked"])); // no record
        Assert.True(await store.AnyWithoutIdAsync(["MAPPA", "Obscure"])); // an unmatched record has no id
        Assert.False(await store.AnyWithoutIdAsync([])); // an anime with no studios asks for none
    }

    [Fact]
    public async Task RemovingAnAnimeLeavesTheStudiosItsLinkLearned()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Only MAPPA anime", Studios = ["MAPPA"] });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Status = WatchStatus.Completed });
        await db.SaveChangesAsync();
        await new StudioLinkStore(db).LearnAsync(["MAPPA"], [new AniListStudio(569, "MAPPA")]);

        db.UserAnimeEntries.RemoveRange(db.UserAnimeEntries);
        db.AnimeMetadata.RemoveRange(db.AnimeMetadata);
        await db.SaveChangesAsync();

        Assert.Equal(569, Assert.Single(await RecordsAsync(db)).Value);
    }
}
