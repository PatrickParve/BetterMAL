using System.Text.Json;
using System.Text.Json.Serialization;
using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.AnimeThemes;
using AnimeTracker.Api.Tests.Services.Search;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimeTracker.Api.Tests.Services.AnimeThemes;

// anime-themes spec, "The themes section lists openings and endings" and "No page
// depends on AnimeThemes": the reader both pages use, over the stored catalogue.
public class AnimeThemesReadsTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<AnimeThemesDto> ReadAsync(AnimeTrackerDbContext db, int animeId) =>
        await new AnimeThemesReads(db).GetForAnimeAsync(animeId);

    // --- The order ---

    [Fact]
    public async Task OpeningsComeBeforeEndings_EachByNumberThenLabel()
    {
        using var db = CreateDb();
        StoredThemes.MarkSynced(db);
        // Stored in no sensible order. OP1 and OP1-BD share a number, so the label settles them.
        db.AnimeThemes.AddRange(
            StoredThemes.Row(1, 50, AnimeThemeKind.Ending, 2),
            StoredThemes.Row(1, 40, AnimeThemeKind.Opening, 2),
            StoredThemes.Row(1, 30, AnimeThemeKind.Opening, 1, label: "OP1-BD"),
            StoredThemes.Row(1, 20, AnimeThemeKind.Ending, 1),
            StoredThemes.Row(1, 10, AnimeThemeKind.Opening, 1));
        await db.SaveChangesAsync();

        var themes = await ReadAsync(db, 1);

        Assert.Equal(["OP1", "OP1-BD", "OP2", "ED1", "ED2"], themes.Themes.Select(t => t.Label));
    }

    [Fact]
    public async Task ANumberAheadOfLabelOrder_StillSortsByNumber()
    {
        using var db = CreateDb();
        StoredThemes.MarkSynced(db);
        // Label order alone would put OP10 before OP2.
        db.AnimeThemes.AddRange(
            StoredThemes.Row(1, 10, AnimeThemeKind.Opening, 10),
            StoredThemes.Row(1, 20, AnimeThemeKind.Opening, 2));
        await db.SaveChangesAsync();

        var themes = await ReadAsync(db, 1);

        Assert.Equal(["OP2", "OP10"], themes.Themes.Select(t => t.Label));
    }

    [Fact]
    public async Task TwoRowsThatAreAlikeKeepAStableOrderByThemeId()
    {
        using var db = CreateDb();
        StoredThemes.MarkSynced(db);
        db.AnimeThemes.AddRange(
            StoredThemes.Row(1, 22, label: "OP1"),
            StoredThemes.Row(1, 11, label: "OP1"));
        await db.SaveChangesAsync();

        var themes = await ReadAsync(db, 1);

        Assert.Equal([11, 22], themes.Themes.Select(t => t.Id));
    }

    // --- Ready ---

    [Fact]
    public async Task BeforeAnyRefreshHasFinishedItIsNotReady()
    {
        using var db = CreateDb();

        var themes = await ReadAsync(db, 1);

        Assert.False(themes.Ready);
        Assert.Empty(themes.Themes);
    }

    [Fact]
    public async Task AFailedFirstAttemptIsStillNotReady()
    {
        using var db = CreateDb();
        db.AnimeThemesSyncStates.Add(new AnimeThemesSyncState { LastAttemptAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        Assert.False((await ReadAsync(db, 1)).Ready);
    }

    [Fact]
    public async Task AfterARefreshHasFinishedItIsReady_AndAnAnimeWithNoRowsHasNoThemes()
    {
        using var db = CreateDb();
        StoredThemes.MarkSynced(db);
        await db.SaveChangesAsync();

        var themes = await ReadAsync(db, 1);

        Assert.True(themes.Ready);
        Assert.Empty(themes.Themes);
    }

    [Fact]
    public async Task ARefreshThatFailedAfterASuccessIsStillReady()
    {
        using var db = CreateDb();
        db.AnimeThemesSyncStates.Add(new AnimeThemesSyncState
        {
            LastSyncedAt = DateTimeOffset.UtcNow.AddDays(-9),
            LastAttemptAt = DateTimeOffset.UtcNow.AddHours(-1),
        });
        await db.SaveChangesAsync();

        Assert.True((await ReadAsync(db, 1)).Ready);
    }

    [Fact]
    public async Task ARealRefreshMakesItReadyAndFillsTheRows()
    {
        // The whole path: the recorded answer, through the real client, the sync and the reader.
        var options = new DbContextOptionsBuilder<AnimeTrackerDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var handler = new FakeAnimeThemesHandler();
        handler.Enqueue(() => FakeAnimeThemesHandler.Json(AnimeThemesFixture.PageJson().Replace("\"hasMorePages\":true", "\"hasMorePages\":false")));
        using (var syncDb = new AnimeTrackerDbContext(options))
        {
            var sync = new AnimeThemesCatalogueSyncService(syncDb, new InstantClient(handler), new FakeThemeSearchIndex(), NullLogger<AnimeThemesCatalogueSyncService>.Instance);
            Assert.Equal(AnimeThemesCatalogueSyncOutcome.Synced, await sync.SyncIfDueAsync());
        }

        using var db = new AnimeTrackerDbContext(options);
        var themes = await ReadAsync(db, 60407); // Kimi to Idol Precure: the recorded anime with insert songs

        Assert.True(themes.Ready);
        Assert.Equal(["OP1", "OP2-TV", "ED1", "ED2", "ED3-TV"], themes.Themes.Select(t => t.Label));
        Assert.Equal(["Akane Kumada", "Chihaya Yoshitake", "Ami Ishii"], themes.Themes[0].Artists);
    }

    // --- What a row carries ---

    [Fact]
    public async Task ARowCarriesEveryFieldAPageShows()
    {
        using var db = CreateDb();
        StoredThemes.MarkSynced(db);
        db.AnimeThemes.Add(StoredThemes.Row(
            1, 77, AnimeThemeKind.Ending, 3, songTitle: "Song title", artists: ["First", "Second"],
            episodes: "1-12", nsfw: true, spoiler: true, clean: true));
        await db.SaveChangesAsync();

        var theme = Assert.Single((await ReadAsync(db, 1)).Themes);

        Assert.Equal(77, theme.Id);
        Assert.Equal(AnimeThemeKind.Ending, theme.Kind);
        Assert.Equal("ED3", theme.Label);
        Assert.Equal("Song title", theme.SongTitle);
        Assert.Equal(["First", "Second"], theme.Artists);
        Assert.Equal("1-12", theme.Episodes);
        Assert.True(theme.Nsfw);
        Assert.True(theme.Spoiler);
        Assert.True(theme.Clean);
        Assert.Equal("https://v.animethemes.moe/T77.webm", theme.VideoUrl);
        Assert.Equal("https://a.animethemes.moe/T77.ogg", theme.AudioUrl);
    }

    [Fact]
    public async Task WhatIsNotKnownIsNullOrEmptyAndNotInvented()
    {
        using var db = CreateDb();
        StoredThemes.MarkSynced(db);
        var row = StoredThemes.Row(1, 5, songTitle: null);
        row.VideoUrl = null;
        row.AudioUrl = null;
        db.AnimeThemes.Add(row);
        await db.SaveChangesAsync();

        var theme = Assert.Single((await ReadAsync(db, 1)).Themes);

        Assert.Null(theme.SongTitle);
        Assert.Empty(theme.Artists);
        Assert.Null(theme.Episodes);
        Assert.Null(theme.VideoUrl);
        Assert.Null(theme.AudioUrl);
    }

    [Fact]
    public void KindTravelsAsItsNameAndFieldsAsCamelCase()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
        var dto = new AnimeThemesDto(true, [new AnimeThemeDto(1, AnimeThemeKind.Opening, "OP1", "S", ["A"], null, false, false, true, null, null)]);

        var json = JsonSerializer.Serialize(dto, options);

        Assert.Contains("\"ready\":true", json);
        Assert.Contains("\"kind\":\"Opening\"", json);
        Assert.Contains("\"songTitle\":\"S\"", json);
        Assert.Contains("\"clean\":true", json);
    }

    // --- One anime, and several ---

    [Fact]
    public async Task OneAnimeIsReadAlone()
    {
        using var db = CreateDb();
        StoredThemes.MarkSynced(db);
        db.AnimeThemes.AddRange(StoredThemes.Row(1, 10), StoredThemes.Row(2, 20), StoredThemes.Row(2, 21, AnimeThemeKind.Ending));
        await db.SaveChangesAsync();

        Assert.Equal([10], (await ReadAsync(db, 1)).Themes.Select(t => t.Id));
        Assert.Equal([20, 21], (await ReadAsync(db, 2)).Themes.Select(t => t.Id));
    }

    [Fact]
    public async Task SeveralAnimeAreReadTogetherInTheOrderGiven()
    {
        using var db = CreateDb();
        StoredThemes.MarkSynced(db);
        db.AnimeThemes.AddRange(
            StoredThemes.Row(1, 10), StoredThemes.Row(1, 11, AnimeThemeKind.Ending),
            StoredThemes.Row(2, 20),
            StoredThemes.Row(3, 30),
            StoredThemes.Row(9, 90)); // not asked for
        await db.SaveChangesAsync();

        var themes = await new AnimeThemesReads(db).GetForSeriesAsync([3, 1, 2]);

        Assert.True(themes.Ready);
        Assert.Equal([3, 1, 2], themes.Groups.Select(g => g.AnimeId));
        Assert.Equal([10, 11], themes.Groups[1].Themes.Select(t => t.Id));
        Assert.DoesNotContain(themes.Groups, g => g.AnimeId == 9);
    }

    [Fact]
    public async Task AnAnimeWithNoRowsHasNoGroup()
    {
        using var db = CreateDb();
        StoredThemes.MarkSynced(db);
        db.AnimeThemes.AddRange(StoredThemes.Row(1, 10), StoredThemes.Row(3, 30));
        await db.SaveChangesAsync();

        var themes = await new AnimeThemesReads(db).GetForSeriesAsync([1, 2, 3]);

        Assert.Equal([1, 3], themes.Groups.Select(g => g.AnimeId));
    }

    [Fact]
    public async Task EachGroupIsOrderedLikeASinglePage()
    {
        using var db = CreateDb();
        StoredThemes.MarkSynced(db);
        db.AnimeThemes.AddRange(
            StoredThemes.Row(1, 12, AnimeThemeKind.Ending), StoredThemes.Row(1, 11, AnimeThemeKind.Opening, 2), StoredThemes.Row(1, 10));
        await db.SaveChangesAsync();

        var single = await ReadAsync(db, 1);
        var group = Assert.Single((await new AnimeThemesReads(db).GetForSeriesAsync([1])).Groups);

        Assert.Equal(single.Themes.Select(t => t.Id), group.Themes.Select(t => t.Id));
    }

    [Fact]
    public async Task AnIdGivenTwiceIsReadOnce()
    {
        using var db = CreateDb();
        StoredThemes.MarkSynced(db);
        db.AnimeThemes.Add(StoredThemes.Row(1, 10));
        await db.SaveChangesAsync();

        var themes = await new AnimeThemesReads(db).GetForSeriesAsync([1, 1, 1]);

        var group = Assert.Single(themes.Groups);
        Assert.Single(group.Themes);
    }

    [Fact]
    public async Task NoAnimeAtAllIsNoGroups_AndStillSaysWhetherItIsReady()
    {
        using var db = CreateDb();

        var before = await new AnimeThemesReads(db).GetForSeriesAsync([]);
        StoredThemes.MarkSynced(db);
        await db.SaveChangesAsync();
        var after = await new AnimeThemesReads(db).GetForSeriesAsync([]);

        Assert.False(before.Ready);
        Assert.Empty(before.Groups);
        Assert.True(after.Ready);
        Assert.Empty(after.Groups);
    }

    [Fact]
    public async Task AnimeWithNoMetadataRowAreStillRead()
    {
        // The catalogue holds anime the app never stored, and so does a read of it.
        using var db = CreateDb();
        StoredThemes.MarkSynced(db);
        db.AnimeThemes.Add(StoredThemes.Row(424242, 10));
        await db.SaveChangesAsync();

        Assert.Empty(db.AnimeMetadata);
        Assert.Single((await ReadAsync(db, 424242)).Themes);
    }

    // --- Nothing else is reached ---

    [Fact]
    public void TheReaderCanReachNothingButTheDatabase()
    {
        var parameters = typeof(AnimeThemesReads).GetConstructors().Single().GetParameters();

        Assert.Equal([typeof(AnimeTrackerDbContext)], parameters.Select(p => p.ParameterType));
    }
}
