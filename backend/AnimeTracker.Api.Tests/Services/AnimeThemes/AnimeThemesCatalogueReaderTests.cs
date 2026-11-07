using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.AnimeThemes;

namespace AnimeTracker.Api.Tests.Services.AnimeThemes;

// anime-themes spec, "Openings and endings are kept in a stored catalogue":
// AnimeThemesCatalogueReader, a pure function from pages to rows. The recorded
// answer is the real thing; the shapes it lacks are built by hand.
public class AnimeThemesCatalogueReaderTests
{
    private static async Task<List<AnimeTheme>> ReadRecordingAsync() =>
        AnimeThemesCatalogueReader.Read([await AnimeThemesFixture.ParsedPageAsync()]);

    private static AnimeTheme Row(List<AnimeTheme> rows, int animeId, int themeId) =>
        rows.Single(r => r.AnimeId == animeId && r.ThemeId == themeId);

    // --- The recorded answer ---

    [Fact]
    public async Task OnlyOpeningsAndEndingsAreHeld_AndEveryOneOfThem()
    {
        var rows = await ReadRecordingAsync();

        // The ten insert songs of anime 4514 are the only themes left out.
        Assert.Equal(28, rows.Count);
        Assert.DoesNotContain(rows, r => r.Label == "IN");
        Assert.DoesNotContain(rows, r => r.ThemeId == 13501);
        Assert.Equal(12, rows.Count(r => r.Kind == AnimeThemeKind.Opening));
        Assert.Equal(16, rows.Count(r => r.Kind == AnimeThemeKind.Ending));
    }

    [Fact]
    public async Task AnOpeningHasItsLabelNumberSongAndArtists()
    {
        var rows = await ReadRecordingAsync();

        var row = Row(rows, 60407, 13497);

        Assert.Equal(AnimeThemeKind.Opening, row.Kind);
        Assert.Equal("OP1", row.Label);
        Assert.Equal(1, row.Sequence);
        Assert.Equal(13425, row.SongId);
        Assert.Equal("Kimi to Idol Precure♪ Light Up!", row.SongTitle);
        Assert.Equal(["Akane Kumada", "Chihaya Yoshitake", "Ami Ishii"], row.Artists);
        Assert.Equal("1-21, 22-", row.Episodes);
        Assert.Equal("https://v.animethemes.moe/KimiToIdolPrecure-OP1.webm", row.VideoUrl);
        Assert.Equal("https://a.animethemes.moe/KimiToIdolPrecure-OP1.ogg", row.AudioUrl);
    }

    [Fact]
    public async Task ALabelWithASuffixIsKeptAsAnimeThemesWroteIt()
    {
        var rows = await ReadRecordingAsync();

        var row = Row(rows, 60407, 13946);

        Assert.Equal("OP2-TV", row.Label);
        Assert.Equal(AnimeThemeKind.Opening, row.Kind);
        Assert.Equal(2, row.Sequence);
    }

    [Fact]
    public async Task ASequenceIsOneWhenAnimeThemesGivesNone()
    {
        var rows = await ReadRecordingAsync();

        Assert.Equal(1, Row(rows, 873, 2422).Sequence);   // OP1, no number
        Assert.Equal(1, Row(rows, 41380, 9399).Sequence); // ED1, no number
        Assert.Equal(2, Row(rows, 41380, 9398).Sequence); // OP2, numbered
    }

    [Fact]
    public async Task ASongWithNoArtistHasAnEmptyList()
    {
        var rows = await ReadRecordingAsync();

        var row = Row(rows, 5525, 3413);

        Assert.Empty(row.Artists);
        Assert.Equal("Aka no Kakera", row.SongTitle);
    }

    [Fact]
    public async Task AGroupCreditedOncePerMemberIsListedOnce()
    {
        var rows = await ReadRecordingAsync();

        Assert.Equal(["BiSH"], Row(rows, 37956, 9241).Artists);
    }

    [Fact]
    public async Task ATrulyDifferentArtistIsKept()
    {
        var rows = await ReadRecordingAsync();

        Assert.Equal(["Haruka Yamazaki", "Nozomi Yamamoto"], Row(rows, 25397, 6461).Artists);
    }

    [Fact]
    public async Task AThemeWithNoSongIsStillHeldWithItsLinks()
    {
        var rows = await ReadRecordingAsync();

        var row = Row(rows, 32323, 6013);

        Assert.Null(row.SongId);
        Assert.Null(row.SongTitle);
        Assert.Empty(row.Artists);
        Assert.Equal("https://v.animethemes.moe/AishenQiaokeliing-OP1.webm", row.VideoUrl);
        Assert.Equal("https://a.animethemes.moe/AishenQiaokeliing-OP1.ogg", row.AudioUrl);
    }

    [Fact]
    public async Task ACleanVideoIsPreferredAndMarked()
    {
        var rows = await ReadRecordingAsync();

        var row = Row(rows, 873, 2422);

        Assert.True(row.Clean);
        Assert.Equal("https://v.animethemes.moe/DotHackRoots-OP1-NCBD.webm", row.VideoUrl);
    }

    [Fact]
    public async Task ACreditedVideoIsUsedWhenThereIsNoCleanOne()
    {
        var rows = await ReadRecordingAsync();

        var row = Row(rows, 873, 2423);

        Assert.False(row.Clean);
        Assert.Equal("https://v.animethemes.moe/DotHackRoots-ED1.webm", row.VideoUrl);
    }

    [Fact]
    public async Task ASafeVersionIsUsedOverSpoilerOnesAndItsEpisodesAreEveryVersions()
    {
        var rows = await ReadRecordingAsync();

        // A.I.C.O.'s OP: version 1 is safe, versions 2 to 5 are spoilers, and
        // version 5 has the only clean video.
        var row = Row(rows, 36039, 8538);

        Assert.False(row.Spoiler);
        Assert.False(row.Clean);
        Assert.Equal("https://v.animethemes.moe/AICOIncarnation-OP1.webm", row.VideoUrl);
        Assert.Equal("2-4, 5-7, 8, 9, 10-12", row.Episodes);
    }

    [Fact]
    public async Task AThemeWhoseOnlyVersionIsASpoilerIsMarkedAsOne()
    {
        var rows = await ReadRecordingAsync();

        var row = Row(rows, 40602, 9957);

        Assert.True(row.Spoiler);
        Assert.False(row.Nsfw);
        Assert.True(row.Clean);
        Assert.Equal("https://v.animethemes.moe/7SeedsS2-ED2-NCBD1080.webm", row.VideoUrl);
    }

    [Fact]
    public async Task ANsfwThemeIsMarkedNsfw()
    {
        var rows = await ReadRecordingAsync();

        Assert.True(Row(rows, 9624, 4200).Nsfw);
        Assert.True(Row(rows, 25397, 6462).Nsfw);
        Assert.False(Row(rows, 25397, 6459).Nsfw);
    }

    [Fact]
    public async Task AThemeWithNoEpisodesHasNone()
    {
        var rows = await ReadRecordingAsync();

        Assert.Null(Row(rows, 873, 2422).Episodes);
        Assert.Null(Row(rows, 9624, 4201).Episodes);
    }

    [Fact]
    public async Task EveryHeldLinkIsOnAnAllowedHost()
    {
        var rows = await ReadRecordingAsync();

        Assert.All(rows, row =>
        {
            Assert.StartsWith("https://v.animethemes.moe/", row.VideoUrl);
            Assert.StartsWith("https://a.animethemes.moe/", row.AudioUrl);
        });
    }

    // --- Shapes the recording lacks ---

    [Fact]
    public void AnAnimeWithNoMalIdIsLeftOut()
    {
        var rows = AnimeThemesCatalogueReader.Read([Parsed.Page(
            Parsed.Anime(1, [], Parsed.Theme(10)),
            Parsed.Anime(2, [200], Parsed.Theme(20)))]);

        var row = Assert.Single(rows);
        Assert.Equal(200, row.AnimeId);
    }

    [Fact]
    public void AnInsertSongIsLeftOut_AndSoIsAnythingElseThatIsNotAnOpeningOrEnding()
    {
        var rows = AnimeThemesCatalogueReader.Read([Parsed.Page(Parsed.Anime(1, [100],
            Parsed.Theme(10, type: "OP", slug: "OP1"),
            Parsed.Theme(11, type: "IN", slug: "IN"),
            Parsed.Theme(12, type: "ED", slug: "ED1"),
            Parsed.Theme(13, type: "XX", slug: "XX1"),
            Parsed.Theme(14, type: "", slug: "")))]);

        Assert.Equal([10, 12], rows.Select(r => r.ThemeId));
    }

    [Fact]
    public void AnAnimeWithTwoMalIdsIsHeldUnderBoth()
    {
        var rows = AnimeThemesCatalogueReader.Read([Parsed.Page(
            Parsed.Anime(1, [100, 101], Parsed.Theme(10), Parsed.Theme(11, type: "ED", slug: "ED1")))]);

        Assert.Equal(4, rows.Count);
        Assert.Equal([10, 11], rows.Where(r => r.AnimeId == 100).Select(r => r.ThemeId));
        Assert.Equal([10, 11], rows.Where(r => r.AnimeId == 101).Select(r => r.ThemeId));
    }

    // --- The anime's name (improve-search-page design.md D8, task 2.3) ---

    [Fact]
    public void AnAnimeWithTwoMalIdsCarriesItsNameOnBothRows()
    {
        var rows = AnimeThemesCatalogueReader.Read([Parsed.Page(
            Parsed.Named(1, "Cowboy Bebop", [100, 101], Parsed.Theme(10), Parsed.Theme(11, type: "ED", slug: "ED1")))]);

        Assert.Equal(4, rows.Count);
        Assert.All(rows, r => Assert.Equal("Cowboy Bebop", r.AnimeName));
        // Each id holds both themes (the rows come theme by theme, so the ids interleave).
        Assert.Equal([10, 11], rows.Where(r => r.AnimeId == 100).Select(r => r.ThemeId));
        Assert.Equal([10, 11], rows.Where(r => r.AnimeId == 101).Select(r => r.ThemeId));
    }

    [Fact]
    public void OneMalIdOnTwoAnimeGivesEachRowTheNameOfTheAnimeItCameFrom()
    {
        var rows = AnimeThemesCatalogueReader.Read([Parsed.Page(
            Parsed.Named(1, "Fate/Zero", [100], Parsed.Theme(10, slug: "OP1")),
            Parsed.Named(2, "Fate/Zero 2nd Season", [100], Parsed.Theme(20, slug: "OP1"), Parsed.Theme(21, type: "ED", slug: "ED1")))]);

        Assert.All(rows, r => Assert.Equal(100, r.AnimeId));
        Assert.Equal("Fate/Zero", Assert.Single(rows, r => r.ThemeId == 10).AnimeName);
        Assert.Equal("Fate/Zero 2nd Season", Assert.Single(rows, r => r.ThemeId == 20).AnimeName);
        Assert.Equal("Fate/Zero 2nd Season", Assert.Single(rows, r => r.ThemeId == 21).AnimeName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void ABlankOrMissingNameIsHeldAsNone(string? name)
    {
        var rows = AnimeThemesCatalogueReader.Read([Parsed.Page(Parsed.Named(1, name, [100], Parsed.Theme(10)))]);

        Assert.Null(Assert.Single(rows).AnimeName);
    }

    [Fact]
    public void ANameIsHeldWithoutItsSurroundingSpace()
    {
        var rows = AnimeThemesCatalogueReader.Read([Parsed.Page(Parsed.Named(1, "  Cowboy Bebop \n", [100], Parsed.Theme(10)))]);

        Assert.Equal("Cowboy Bebop", Assert.Single(rows).AnimeName);
    }

    [Fact]
    public void ANameOnlyNamesItsOwnAnimesRows()
    {
        var rows = AnimeThemesCatalogueReader.Read([Parsed.Page(
            Parsed.Named(1, "First", [100], Parsed.Theme(10)),
            Parsed.Named(2, null, [200], Parsed.Theme(20)),
            Parsed.Named(3, "Third", [300], Parsed.Theme(30)))]);

        Assert.Equal(["First", null, "Third"], rows.Select(r => r.AnimeName));
    }

    [Fact]
    public async Task TheRecordingPredatesNamesSoItsRowsHoldNone()
    {
        // See AnimeThemesFixture: recorded before `title { romaji }` was asked for.
        var rows = await ReadRecordingAsync();

        Assert.NotEmpty(rows);
        Assert.All(rows, r => Assert.Null(r.AnimeName));
    }

    [Fact]
    public void TwoAnimeOnOneMalIdAreMerged()
    {
        var rows = AnimeThemesCatalogueReader.Read([Parsed.Page(
            Parsed.Anime(1, [100], Parsed.Theme(10, slug: "OP1")),
            Parsed.Anime(2, [100], Parsed.Theme(20, slug: "OP1"), Parsed.Theme(21, type: "ED", slug: "ED1")))]);

        Assert.All(rows, r => Assert.Equal(100, r.AnimeId));
        Assert.Equal([10, 20, 21], rows.Select(r => r.ThemeId));
    }

    [Fact]
    public void AnAnimeRepeatedAcrossAPageBoundaryIsHeldOnce()
    {
        // AnimeThemes can change between two requests and send the same anime on
        // both pages; a row can only be added once.
        var anime = Parsed.Anime(1, [100], Parsed.Theme(10));

        var rows = AnimeThemesCatalogueReader.Read([Parsed.Page(anime), Parsed.Page(anime)]);

        Assert.Single(rows);
    }

    [Fact]
    public void AnAliasIsPreferredOverTheArtistsName()
    {
        var song = Parsed.Song(1, "Song",
            Parsed.Credit("Kenji Kawai", alias: "Kawai Kenji"),
            Parsed.Credit("Plain Artist"));

        var rows = AnimeThemesCatalogueReader.Read([Parsed.Page(Parsed.Anime(1, [100], Parsed.Theme(10, song: song)))]);

        Assert.Equal(["Kawai Kenji", "Plain Artist"], Assert.Single(rows).Artists);
    }

    [Fact]
    public void ABlankAliasOrNameIsNotACredit()
    {
        var song = Parsed.Song(1, "Song",
            Parsed.Credit("Real Name", alias: "  "),
            new AnimeThemesCredit(null, null),
            new AnimeThemesCredit("", ""));

        var rows = AnimeThemesCatalogueReader.Read([Parsed.Page(Parsed.Anime(1, [100], Parsed.Theme(10, song: song)))]);

        Assert.Equal(["Real Name"], Assert.Single(rows).Artists);
    }

    [Fact]
    public void ASongSharedByTwoAnimeKeepsOneSongIdUnderBoth()
    {
        var song = Parsed.Song(555, "Shared", Parsed.Credit("Someone"));

        var rows = AnimeThemesCatalogueReader.Read([Parsed.Page(
            Parsed.Anime(1, [100], Parsed.Theme(10, song: song)),
            Parsed.Anime(2, [200], Parsed.Theme(20, song: song)))]);

        Assert.Equal([100, 200], rows.Select(r => r.AnimeId));
        Assert.All(rows, r => Assert.Equal(555, r.SongId));
    }

    [Fact]
    public void ARowWithNoSongNoVideoAndNoAudioIsStillHeld()
    {
        var rows = AnimeThemesCatalogueReader.Read([Parsed.Page(Parsed.Anime(1, [100],
            Parsed.Theme(10, slug: "ED2", type: "ED", sequence: 2, song: null, versions: [])))]);

        var row = Assert.Single(rows);
        Assert.Equal("ED2", row.Label);
        Assert.Equal(AnimeThemeKind.Ending, row.Kind);
        Assert.Null(row.SongId);
        Assert.Null(row.VideoUrl);
        Assert.Null(row.AudioUrl);
        Assert.Null(row.Episodes);
        Assert.False(row.Clean);
        Assert.False(row.Nsfw);
        Assert.False(row.Spoiler);
    }

    [Fact]
    public void ABadAudioHostLeavesTheRowHeldWithNoAudio()
    {
        var version = Parsed.Version(1, [new AnimeThemesVideo(true, 60, "https://v.animethemes.moe/x.webm", "http://a.animethemes.moe/x.ogg")]);

        var rows = AnimeThemesCatalogueReader.Read([Parsed.Page(Parsed.Anime(1, [100], Parsed.Theme(10, versions: [version])))]);

        var row = Assert.Single(rows);
        Assert.Null(row.AudioUrl);
        Assert.Equal("https://v.animethemes.moe/x.webm", row.VideoUrl);
    }

    [Fact]
    public void NoPagesGiveNoRows()
    {
        Assert.Empty(AnimeThemesCatalogueReader.Read([]));
        Assert.Empty(AnimeThemesCatalogueReader.Read([Parsed.Page()]));
    }

    [Fact]
    public void TheRowsFollowTheOrderTheyArrivedIn()
    {
        var rows = AnimeThemesCatalogueReader.Read(
        [
            Parsed.Page(Parsed.Anime(1, [100], Parsed.Theme(10), Parsed.Theme(11, type: "ED", slug: "ED1"))),
            Parsed.Page(Parsed.Anime(2, [50], Parsed.Theme(20))),
        ]);

        Assert.Equal([10, 11, 20], rows.Select(r => r.ThemeId));
    }
}
