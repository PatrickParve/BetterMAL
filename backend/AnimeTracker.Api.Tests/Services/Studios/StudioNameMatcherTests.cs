using AnimeTracker.Api.Services.Airing.AniList;
using AnimeTracker.Api.Services.Studios;

namespace AnimeTracker.Api.Tests.Services.Studios;

// StudioNameMatcher (studio-links "Studio ids are learned from AniList's record of an
// anime", add-genre-and-studio-stats task 2.6): the two sites spell a name differently
// often enough that names are compared normalised, and only inside one anime's credits.
public class StudioNameMatcherTests
{
    [Theory]
    [InlineData("J.C.Staff", "J.C. Staff")]
    [InlineData("Studio Deen", "Studio DEEN")]
    [InlineData("Pierrot", "Studio Pierrot")]
    [InlineData("Diomedéa", "diomedéa")]
    [InlineData("Diomedéa", "Diomedea")]
    [InlineData("SILVER LINK.", "Silver Link")]
    [InlineData("Production I.G", "Production IG")]
    public void TwoSpellingsOfOneNameNormaliseAlike(string mal, string aniList)
    {
        Assert.Equal(StudioNameMatcher.Normalise(mal), StudioNameMatcher.Normalise(aniList));
    }

    [Theory]
    [InlineData("MAPPA", "Wit Studio")]
    [InlineData("Studio Pierrot", "Pierrot Plus")]
    [InlineData("Bones", "Bones Film")]
    public void TwoDifferentNamesStayDifferent(string a, string b)
    {
        Assert.NotEqual(StudioNameMatcher.Normalise(a), StudioNameMatcher.Normalise(b));
    }

    [Fact]
    public void ALeadingStudioIsDroppedOnlyWhenSomethingRemains()
    {
        Assert.Equal("pierrot", StudioNameMatcher.Normalise("Studio Pierrot"));
        Assert.Equal("studio", StudioNameMatcher.Normalise("Studio"));
        Assert.Equal("studio", StudioNameMatcher.Normalise("STUDIO!"));
    }

    [Fact]
    public void AStudioWordAnywhereButTheStartStays()
    {
        Assert.Equal("witstudio", StudioNameMatcher.Normalise("Wit Studio"));
    }

    [Fact]
    public void ANameWithNothingToCompareNormalisesToNothing()
    {
        Assert.Equal("", StudioNameMatcher.Normalise("!!! ..."));
    }

    [Fact]
    public void ANameIsMatchedToTheSameAnimesStudioOfTheSameNormalisedName()
    {
        var matches = StudioNameMatcher.Match(
            ["J.C.Staff", "Aniplex"],
            [new AniListStudio(16, "J.C. Staff"), new AniListStudio(17, "Aniplex"), new AniListStudio(99, "Bandai Visual")]);

        Assert.Equal(16, matches["J.C.Staff"]);
        Assert.Equal(17, matches["Aniplex"]);
        Assert.Equal(2, matches.Count);
    }

    [Fact]
    public void ANameIsMatchedOnlyWithinTheAnimeThatCreditsIt()
    {
        // One anime's AniList credits are Wit Studio; another anime's include MAPPA. Matching
        // MAPPA against the first finds nothing, and against the second finds it.
        var first = StudioNameMatcher.Match(["MAPPA"], [new AniListStudio(858, "Wit Studio")]);
        var second = StudioNameMatcher.Match(["MAPPA"], [new AniListStudio(569, "MAPPA")]);

        Assert.Empty(first);
        Assert.Equal(569, second["MAPPA"]);
    }

    [Fact]
    public void ANameAniListDoesNotListIsAbsentFromTheResult()
    {
        var matches = StudioNameMatcher.Match(["MAPPA", "Aniplex"], [new AniListStudio(569, "MAPPA")]);

        Assert.True(matches.ContainsKey("MAPPA"));
        Assert.False(matches.ContainsKey("Aniplex"));
    }

    [Fact]
    public void AnAnimeAniListListsNoStudioForMatchesNothing()
    {
        Assert.Empty(StudioNameMatcher.Match(["MAPPA"], []));
    }

    [Fact]
    public void ANameThatNormalisesToNothingNeverMatches()
    {
        Assert.Empty(StudioNameMatcher.Match(["???"], [new AniListStudio(1, "!!!")]));
    }

    [Fact]
    public void WhereAniListListsTwoStudiosThatNormaliseAlikeTheFirstListedIsTaken()
    {
        var matches = StudioNameMatcher.Match(["Pierrot"], [new AniListStudio(7, "Studio Pierrot"), new AniListStudio(8, "PIERROT")]);

        Assert.Equal(7, matches["Pierrot"]);
    }
}
