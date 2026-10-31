using AnimeTracker.Api.Services.AnimeThemes;

namespace AnimeTracker.Api.Tests.Services.AnimeThemes;

// anime-themes spec, "An opening or ending uses one version and one video": each
// rule on its own (design D4).
public class AnimeThemeChoiceTests
{
    private static AnimeThemesVersion[] Versions(params AnimeThemesVersion[] versions) => versions;

    // --- The video ---

    [Fact]
    public void ACleanVideoIsUsedOverOneWithCredits()
    {
        // The credited one has the higher priority, so it is the cleanness that decides.
        var chosen = AnimeThemeChoice.Choose(Versions(Parsed.Version(1,
        [
            Parsed.Video("Show-OP1", clean: false, priority: 80),
            Parsed.Video("Show-OP1-NC", clean: true, priority: 20),
        ])));

        Assert.True(chosen.Clean);
        Assert.Equal("https://v.animethemes.moe/Show-OP1-NC.webm", chosen.VideoUrl);
    }

    [Fact]
    public void AmongCleanVideosTheHighestPriorityIsUsed()
    {
        var chosen = AnimeThemeChoice.Choose(Versions(Parsed.Version(1,
        [
            Parsed.Video("Show-OP1-NC720", clean: true, priority: 30),
            Parsed.Video("Show-OP1-NC1080", clean: true, priority: 60),
            Parsed.Video("Show-OP1-NC480", clean: true, priority: 10),
        ])));

        Assert.Equal("https://v.animethemes.moe/Show-OP1-NC1080.webm", chosen.VideoUrl);
    }

    [Fact]
    public void WithNoCleanVideoTheHighestPriorityIsUsed_AndItIsNotClean()
    {
        var chosen = AnimeThemeChoice.Choose(Versions(Parsed.Version(1,
        [
            Parsed.Video("Show-OP1-low", priority: 20),
            Parsed.Video("Show-OP1-high", priority: 60),
        ])));

        Assert.False(chosen.Clean);
        Assert.Equal("https://v.animethemes.moe/Show-OP1-high.webm", chosen.VideoUrl);
    }

    [Fact]
    public void TheAudioIsTheChosenVideosOwn()
    {
        var chosen = AnimeThemeChoice.Choose(Versions(Parsed.Version(1,
        [
            Parsed.Video("Credited", clean: false, priority: 80),
            Parsed.Video("Clean", clean: true, priority: 20),
        ])));

        Assert.Equal("https://a.animethemes.moe/Clean.ogg", chosen.AudioUrl);
    }

    [Fact]
    public void AVideoWithNoAudioLosesToOneWithAudioInTheSameVersion()
    {
        var chosen = AnimeThemeChoice.Choose(Versions(Parsed.Version(1,
        [
            Parsed.Video("Silent-NC", clean: true, priority: 60, audio: false),
            Parsed.Video("Sound", clean: false, priority: 20),
        ])));

        Assert.Equal("https://v.animethemes.moe/Sound.webm", chosen.VideoUrl);
        Assert.Equal("https://a.animethemes.moe/Sound.ogg", chosen.AudioUrl);
    }

    // --- The version ---

    [Fact]
    public void AnUnflaggedVersionIsUsedOverAFlaggedOne()
    {
        // The spoiler cut is the lower number, and has a clean video, and still loses.
        var chosen = AnimeThemeChoice.Choose(Versions(
            Parsed.Version(1, [Parsed.Video("v1", clean: true)], spoiler: true),
            Parsed.Version(2, [Parsed.Video("v2")])));

        Assert.False(chosen.Spoiler);
        Assert.False(chosen.Nsfw);
        Assert.Equal("https://v.animethemes.moe/v2.webm", chosen.VideoUrl);
        Assert.False(chosen.Clean);
    }

    [Fact]
    public void ANsfwOnlyVersionIsUsedOverASpoilerOne()
    {
        var chosen = AnimeThemeChoice.Choose(Versions(
            Parsed.Version(1, [Parsed.Video("v1")], spoiler: true),
            Parsed.Version(2, [Parsed.Video("v2")], nsfw: true)));

        Assert.True(chosen.Nsfw);
        Assert.False(chosen.Spoiler);
        Assert.Equal("https://v.animethemes.moe/v2.webm", chosen.VideoUrl);
    }

    [Fact]
    public void AVersionBothNsfwAndSpoilerRanksWithTheSpoilerOnes()
    {
        var chosen = AnimeThemeChoice.Choose(Versions(
            Parsed.Version(1, [Parsed.Video("v1")], nsfw: true, spoiler: true),
            Parsed.Version(2, [Parsed.Video("v2")], spoiler: true)));

        // Equal rank, so the lowest number, flagged as both.
        Assert.True(chosen.Nsfw);
        Assert.True(chosen.Spoiler);
        Assert.Equal("https://v.animethemes.moe/v1.webm", chosen.VideoUrl);
    }

    [Fact]
    public void AVersionFlaggedOnlyNsfwBeatsOneFlaggedBoth()
    {
        var chosen = AnimeThemeChoice.Choose(Versions(
            Parsed.Version(1, [Parsed.Video("v1")], nsfw: true, spoiler: true),
            Parsed.Version(2, [Parsed.Video("v2")], nsfw: true)));

        Assert.True(chosen.Nsfw);
        Assert.False(chosen.Spoiler);
    }

    [Fact]
    public void TheLowestNumberWinsAmongEqualsWhateverTheOrderGiven()
    {
        var chosen = AnimeThemeChoice.Choose(Versions(
            Parsed.Version(3, [Parsed.Video("v3")]),
            Parsed.Version(1, [Parsed.Video("v1")]),
            Parsed.Version(2, [Parsed.Video("v2")])));

        Assert.Equal("https://v.animethemes.moe/v1.webm", chosen.VideoUrl);
    }

    [Fact]
    public void EveryVersionFlaggedUsesTheLowestNumberedAndMarksItAsFlagged()
    {
        var chosen = AnimeThemeChoice.Choose(Versions(
            Parsed.Version(2, [Parsed.Video("v2")], spoiler: true),
            Parsed.Version(1, [Parsed.Video("v1")], spoiler: true)));

        Assert.True(chosen.Spoiler);
        Assert.Equal("https://v.animethemes.moe/v1.webm", chosen.VideoUrl);
    }

    [Fact]
    public void AVersionWithNoAudioIsPassedOver()
    {
        var chosen = AnimeThemeChoice.Choose(Versions(
            Parsed.Version(1, [Parsed.Video("v1", audio: false)]),
            Parsed.Version(2, [Parsed.Video("v2")], spoiler: true)));

        // Version 1 is unflagged and lower, but has nothing to play.
        Assert.True(chosen.Spoiler);
        Assert.Equal("https://a.animethemes.moe/v2.ogg", chosen.AudioUrl);
    }

    [Fact]
    public void AThemeWhoseVersionsHaveNoAudioIsStillHeldWithItsVideo()
    {
        var chosen = AnimeThemeChoice.Choose(Versions(
            Parsed.Version(1, [Parsed.Video("v1", audio: false)])));

        Assert.Equal("https://v.animethemes.moe/v1.webm", chosen.VideoUrl);
        Assert.Null(chosen.AudioUrl);
    }

    [Fact]
    public void AThemeWithNoVideoAtAllIsStillHeld()
    {
        var chosen = AnimeThemeChoice.Choose(Versions(Parsed.Version(1, [], episodes: "1-3")));

        Assert.Null(chosen.VideoUrl);
        Assert.Null(chosen.AudioUrl);
        Assert.False(chosen.Clean);
        Assert.Equal("1-3", chosen.Episodes);
    }

    [Fact]
    public void AThemeWithNoVersionsIsHeldWithNothing()
    {
        var chosen = AnimeThemeChoice.Choose([]);

        Assert.Equal(new ChosenTheme(null, false, false, false, null, null), chosen);
    }

    [Fact]
    public void FlagsAreTheChosenVersionsNotAnyOther()
    {
        // Version 2 is the flagged one. It isn't used, so it flags nothing.
        var chosen = AnimeThemeChoice.Choose(Versions(
            Parsed.Version(1, [Parsed.Video("v1")]),
            Parsed.Version(2, [Parsed.Video("v2")], nsfw: true, spoiler: true)));

        Assert.False(chosen.Nsfw);
        Assert.False(chosen.Spoiler);
    }

    // --- The episodes ---

    [Fact]
    public void EpisodesAreEveryVersionsInOrder()
    {
        var chosen = AnimeThemeChoice.Choose(Versions(
            Parsed.Version(2, [Parsed.Video("v2")], episodes: "13-24"),
            Parsed.Version(1, [Parsed.Video("v1")], episodes: "1-12")));

        Assert.Equal("1-12, 13-24", chosen.Episodes);
    }

    [Fact]
    public void EpisodesIncludeAVersionThatIsNotUsed()
    {
        var chosen = AnimeThemeChoice.Choose(Versions(
            Parsed.Version(1, [Parsed.Video("v1")], episodes: "1-4", spoiler: true),
            Parsed.Version(2, [Parsed.Video("v2")], episodes: "5-8")));

        Assert.Equal("https://v.animethemes.moe/v2.webm", chosen.VideoUrl);
        Assert.Equal("1-4, 5-8", chosen.Episodes);
    }

    [Fact]
    public void AVersionWithNoEpisodesAddsNothing()
    {
        var chosen = AnimeThemeChoice.Choose(Versions(
            Parsed.Version(1, [Parsed.Video("v1")], episodes: "1-4"),
            Parsed.Version(2, [Parsed.Video("v2")], episodes: null),
            Parsed.Version(3, [Parsed.Video("v3")], episodes: "  ")));

        Assert.Equal("1-4", chosen.Episodes);
    }

    [Fact]
    public void NoEpisodesAnywhereIsNull()
    {
        var chosen = AnimeThemeChoice.Choose(Versions(Parsed.Version(1, [Parsed.Video("v1")], episodes: null)));

        Assert.Null(chosen.Episodes);
    }

    // --- The allow-list ---

    [Fact]
    public void AnAudioLinkOnAnotherHostIsDropped_AndTheRowStillHasItsVideo()
    {
        var video = new AnimeThemesVideo(false, 20, "https://v.animethemes.moe/Show-OP1.webm", "https://evil.example/Show-OP1.ogg");

        var chosen = AnimeThemeChoice.Choose(Versions(Parsed.Version(1, [video])));

        Assert.Null(chosen.AudioUrl);
        Assert.Equal("https://v.animethemes.moe/Show-OP1.webm", chosen.VideoUrl);
    }

    [Fact]
    public void AVideoLinkOnAnotherHostIsDropped_AndTheRowStillHasItsAudio()
    {
        var video = new AnimeThemesVideo(false, 20, "https://evil.example/Show-OP1.webm", "https://a.animethemes.moe/Show-OP1.ogg");

        var chosen = AnimeThemeChoice.Choose(Versions(Parsed.Version(1, [video])));

        Assert.Null(chosen.VideoUrl);
        Assert.Equal("https://a.animethemes.moe/Show-OP1.ogg", chosen.AudioUrl);
    }

    [Theory]
    [InlineData("http://a.animethemes.moe/Show-OP1.ogg")]
    [InlineData("https://v.animethemes.moe/Show-OP1.ogg")]
    [InlineData("https://a.animethemes.moe.evil.example/Show-OP1.ogg")]
    [InlineData("https://evil.example/a.animethemes.moe/Show-OP1.ogg")]
    [InlineData("https://a.animethemes.moe@evil.example/Show-OP1.ogg")]
    [InlineData("https://user:pass@a.animethemes.moe/Show-OP1.ogg")]
    [InlineData("https://a.animethemes.moe:8443/Show-OP1.ogg")]
    [InlineData("//a.animethemes.moe/Show-OP1.ogg")]
    [InlineData("/Show-OP1.ogg")]
    [InlineData("ftp://a.animethemes.moe/Show-OP1.ogg")]
    [InlineData("a.animethemes.moe/Show-OP1.ogg")]
    [InlineData("not a link")]
    [InlineData("")]
    [InlineData("   ")]
    public void AnAudioLinkThatIsNotHttpsOnTheAudioHostIsRefused(string link)
    {
        Assert.Null(AnimeThemeChoice.AudioLink(link));
    }

    [Theory]
    [InlineData("http://v.animethemes.moe/Show-OP1.webm")]
    [InlineData("https://a.animethemes.moe/Show-OP1.webm")]
    [InlineData("https://v.animethemes.moe.evil.example/Show-OP1.webm")]
    public void AVideoLinkThatIsNotHttpsOnTheVideoHostIsRefused(string link)
    {
        Assert.Null(AnimeThemeChoice.VideoLink(link));
    }

    [Fact]
    public void AnAllowedLinkIsKeptAsItIs()
    {
        Assert.Equal("https://a.animethemes.moe/DeathNote-OP1.ogg", AnimeThemeChoice.AudioLink("https://a.animethemes.moe/DeathNote-OP1.ogg"));
        Assert.Equal("https://v.animethemes.moe/DeathNote-OP1.webm", AnimeThemeChoice.VideoLink("https://v.animethemes.moe/DeathNote-OP1.webm"));
    }

    [Fact]
    public void TheHostIsMatchedWhateverItsCase()
    {
        Assert.NotNull(AnimeThemeChoice.AudioLink("https://A.AnimeThemes.MOE/Show-OP1.ogg"));
    }

    [Fact]
    public void ALinkWithSpacesIsHeldEscapedSoWhatIsStoredIsWhatIsFetched()
    {
        var link = AnimeThemeChoice.AudioLink("https://a.animethemes.moe/Show OP1.ogg");

        Assert.Equal("https://a.animethemes.moe/Show%20OP1.ogg", link);
    }
}
