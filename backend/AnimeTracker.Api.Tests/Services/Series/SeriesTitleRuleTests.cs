using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Series;

namespace AnimeTracker.Api.Tests.Services.Series;

// series-identity spec "A custom series title must be a contiguous trim of
// an offered title" (design.md D8, tasks.md 6.1/6.5).
public class SeriesTitleRuleTests
{
    private static readonly string[] BeybladeOffered = ["Beyblade: Metal Fusion"];

    [Fact]
    public void TrimmingToAPrefixIsAccepted()
    {
        Assert.True(SeriesTitleRule.IsAcceptable("Beyblade", BeybladeOffered));
    }

    [Fact]
    public void TrimmingToASuffixIsAccepted()
    {
        Assert.True(SeriesTitleRule.IsAcceptable("Metal Fusion", BeybladeOffered));
    }

    [Fact]
    public void CutsFromTheMiddleAreRefused()
    {
        Assert.False(SeriesTitleRule.IsAcceptable("Beyblade Fusion", BeybladeOffered));
    }

    [Fact]
    public void InventedTextIsRefused()
    {
        Assert.False(SeriesTitleRule.IsAcceptable("bladusin", BeybladeOffered));
    }

    [Fact]
    public void WordsCannotBeAdded()
    {
        Assert.False(SeriesTitleRule.IsAcceptable("Beyblade: Metal Fusion Remastered", BeybladeOffered));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyOrWhitespaceOnlyTitleIsRefused(string candidate)
    {
        Assert.False(SeriesTitleRule.IsAcceptable(candidate, BeybladeOffered));
    }

    [Fact]
    public void WholeOfferedTitleIsAccepted()
    {
        Assert.True(SeriesTitleRule.IsAcceptable("Beyblade: Metal Fusion", BeybladeOffered));
    }

    [Fact]
    public void PunctuationBoundaryIsAccepted()
    {
        // A dangling trailing colon from a naive prefix cut is trimmed before
        // comparing, so it doesn't fail on the punctuation boundary.
        Assert.True(SeriesTitleRule.IsAcceptable("Beyblade:", BeybladeOffered));
    }

    [Fact]
    public void CaseInsensitiveAcceptanceStoresAsTyped()
    {
        Assert.True(SeriesTitleRule.IsAcceptable("BEYBLADE", BeybladeOffered));
        Assert.Equal("BEYBLADE", SeriesTitleRule.Normalize("BEYBLADE"));
    }

    // --- OfferedTitles ---

    private static SeriesMember Member(int animeId, string title, string? englishTitle = null) => new()
    {
        AnimeId = animeId,
        Anime = new AnimeMetadata { Id = animeId, Title = title, EnglishTitle = englishTitle },
    };

    [Fact]
    public void OfferedTitlesSpansMainLineInOrderWithEnglishTitlesIncluded()
    {
        var members = new List<SeriesMember>
        {
            Member(1, "Attack on Titan", "Shingeki no Kyojin"),
            Member(2, "Attack on Titan Season 2"),
            Member(3, "Attack on Titan Final Season", "Attack on Titan: Final Season"),
        };

        var titles = SeriesTitleRule.OfferedTitles(members);

        Assert.Equal(
            ["Attack on Titan", "Shingeki no Kyojin", "Attack on Titan Season 2", "Attack on Titan Final Season", "Attack on Titan: Final Season"],
            titles);
    }

    [Fact]
    public void OfferedTitlesDeduplicates()
    {
        var members = new List<SeriesMember>
        {
            Member(1, "Same Title"),
            Member(2, "Same Title"),
        };

        var titles = SeriesTitleRule.OfferedTitles(members);

        Assert.Equal(["Same Title"], titles);
    }
}
