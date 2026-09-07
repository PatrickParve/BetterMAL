using AnimeTracker.Api.Services.Search;

namespace AnimeTracker.Api.Tests.Services.Search;

// SeriesSearchLookup itself is a thin SeriesMembers ⋈ AnimeMetadata query; the
// matching/collapsing/ordering logic it feeds lives in SeriesSearchIndex, so
// these tests build the index directly from projections (as LoadAsync would)
// rather than going through a database. seriesId is the root entry's anime
// id (key-series-by-root-anime-id design.md D1) — every series here must
// include a member row whose AnimeId equals its own seriesId, since
// SeriesSearchIndex.ToResultDto resolves display fields from that row.
public class SeriesSearchLookupTests
{
    private static SeriesMemberProjection Member(
        int seriesId, int animeId, string title,
        string? englishTitle = null, string? malPictureUrl = null, bool isMainLine = true, int? popularityRank = null,
        string? selectedTitle = null, string? selectedPictureUrl = null) =>
        new(seriesId, animeId, title, englishTitle, malPictureUrl, isMainLine, popularityRank, selectedTitle, selectedPictureUrl);

    [Fact]
    public void MatchesOnNonRootMembersTitle()
    {
        var index = new SeriesSearchIndex(
        [
            Member(100, 100, "Attack on Titan", malPictureUrl: "root.jpg"),
            Member(100, 101, "Attack on Titan Final Season"),
        ]);

        var results = index.Match("final season", exact: false);

        var series = Assert.Single(results);
        Assert.Equal(100, series.SeriesId);
    }

    [Fact]
    public void DisplayFieldsComeFromRootMemberRegardlessOfWhichMemberMatched()
    {
        var index = new SeriesSearchIndex(
        [
            Member(100, 100, "Attack on Titan", englishTitle: "Attack on Titan", malPictureUrl: "root.jpg"),
            Member(100, 101, "Shingeki no Kyojin: Final Season", englishTitle: null, malPictureUrl: "final-season.jpg"),
        ]);

        var results = index.Match("shingeki", exact: false);

        var series = Assert.Single(results);
        Assert.Equal(100, series.SeriesId);
        Assert.Equal("Attack on Titan", series.Title);
        Assert.Equal("Attack on Titan", series.EnglishTitle);
        Assert.Equal("root.jpg", series.PictureUrl);
    }

    [Fact]
    public void CollapsesToOneRowPerSeriesKeepingTheStrongestMatchQuality()
    {
        var index = new SeriesSearchIndex(
        [
            // Series 100: one member only contains-matches, its sibling prefix-matches —
            // the collapsed row should carry the stronger (prefix) quality.
            Member(100, 100, "The Great Attack Chronicles"),
            Member(100, 101, "Attack on Titan"),
            // Series 200: every member is contains-only, never prefix — should rank behind series 100.
            Member(200, 200, "Some Other Attack Story"),
        ]);

        var results = index.Match("attack", exact: false);

        Assert.Equal(2, results.Count); // collapsed to one row per series, not per member
        Assert.Equal(100, results[0].SeriesId); // prefix-quality series ranks ahead of contains-only
        Assert.Equal(200, results[1].SeriesId);
    }

    [Fact]
    public void ExactModeOnlyMatchesEqualTitles()
    {
        var index = new SeriesSearchIndex(
        [
            Member(100, 100, "Attack on Titan"),
            Member(100, 101, "Attack on Titan Final Season"),
        ]);

        var exactMatch = index.Match("Attack on Titan", exact: true);
        var noMatch = index.Match("Attack on Titan Final", exact: true);

        Assert.Single(exactMatch);
        Assert.Empty(noMatch);
    }

    [Fact]
    public void EntryCountIncludesExtrasAlongsideMainLine()
    {
        var index = new SeriesSearchIndex(
        [
            Member(100, 100, "Attack on Titan", isMainLine: true),
            Member(100, 101, "Attack on Titan Season 2", isMainLine: true),
            Member(100, 102, "Attack on Titan Season 3", isMainLine: true),
            Member(100, 103, "Attack on Titan Final Season", isMainLine: true),
            Member(100, 104, "Attack on Titan: No Regrets (OVA)", isMainLine: false),
            Member(100, 105, "Attack on Titan: Lost Girls (OVA)", isMainLine: false),
            Member(100, 106, "Attack on Titan: Chronicle (Recap)", isMainLine: false),
        ]);

        var results = index.Match("attack on titan", exact: false);

        var series = Assert.Single(results);
        Assert.Equal(7, series.EntryCount);
    }

    [Fact]
    public void UnmatchedQueryReturnsNoSeries()
    {
        var index = new SeriesSearchIndex(
        [
            Member(100, 100, "Attack on Titan"),
        ]);

        Assert.Empty(index.Match("one piece", exact: false));
    }

    [Fact]
    public void EmptyIndexReturnsNoSeriesCheaply()
    {
        Assert.Empty(SeriesSearchIndex.Empty.Match("anything", exact: false));
        Assert.False(SeriesSearchIndex.Empty.HasSeries(100));
    }

    // --- 5.5: series-identity spec "A renamed series is still found by its members' titles" ---

    [Fact]
    public void RenamedSeriesMatchesAMemberTitle()
    {
        var index = new SeriesSearchIndex(
        [
            Member(100, 100, "Beyblade: Metal Fusion", selectedTitle: "Beyblade"),
        ]);

        var results = index.Match("Metal Fusion", exact: false);

        var series = Assert.Single(results);
        Assert.Equal("Beyblade", series.Title);
    }

    [Fact]
    public void RenamedSeriesMatchesItsOwnChosenTitle()
    {
        var index = new SeriesSearchIndex(
        [
            Member(100, 100, "Beyblade: Metal Fusion", selectedTitle: "Beyblade"),
        ]);

        var results = index.Match("Beyblade", exact: false);

        Assert.Single(results);
    }

    [Fact]
    public void RenamedSeriesIsDisplayedUnderItsChosenTitle()
    {
        var index = new SeriesSearchIndex(
        [
            Member(100, 100, "Beyblade: Metal Fusion", englishTitle: "Beyblade: Metal Fusion", selectedTitle: "Beyblade"),
        ]);

        var results = index.Match("Metal Fusion", exact: false);

        var series = Assert.Single(results);
        Assert.Equal("Beyblade", series.Title);
        Assert.Null(series.EnglishTitle);
    }

    // --- Normalized matching (design.md D1-D3, tasks.md 1.8) ---

    [Fact]
    public void MemberTitleWithPunctuationMatchedByADePunctuatedQuery()
    {
        var index = new SeriesSearchIndex(
        [
            Member(100, 100, "Re:ZERO -Starting Life in Another World-"),
        ]);

        var results = index.Match("re zero starting life", exact: false);

        var series = Assert.Single(results);
        Assert.Equal(100, series.SeriesId);
    }

    [Fact]
    public void ChosenTitleWithPunctuationMatchedByADePunctuatedQuery()
    {
        var index = new SeriesSearchIndex(
        [
            // The member's own title doesn't match at all — only the chosen
            // (renamed) title does, once its punctuation is stripped.
            Member(100, 100, "Hagane no Renkinjutsushi", selectedTitle: "Full-Metal: Alchemist!"),
        ]);

        var results = index.Match("full metal alchemist", exact: false);

        Assert.Single(results);
    }

    [Fact]
    public void MatchQualityStillOrdersResultsUnderNormalization()
    {
        var index = new SeriesSearchIndex(
        [
            // Prefix once de-punctuated (query is the leading words, minus punctuation).
            Member(100, 100, "Kaguya-sama: Love is War"),
            // Contains only: the query sits in the middle of the de-punctuated title.
            Member(200, 200, "Some Preamble Kaguya-sama Love Story"),
        ]);

        var results = index.Match("kaguya sama love", exact: false);

        Assert.Equal([100, 200], results.Select(s => s.SeriesId));
    }

    // --- Whole-word contains matching ---

    [Fact]
    public void ContainsMatchIgnoresAMidWordFragmentInAnotherMember()
    {
        var index = new SeriesSearchIndex(
        [
            Member(100, 100, "My Hero Academia"),
            Member(100, 101, "My Hero Academia: World Heroes' Mission"),
        ]);

        // "miss" is a raw substring of "Mission" but not a whole word in it —
        // the franchise must not surface for a query that names no real word
        // in any of its titles.
        Assert.Empty(index.Match("miss", exact: false));
    }

    [Fact]
    public void ContainsMatchStillFindsAWholeWordInsideATitle()
    {
        var index = new SeriesSearchIndex(
        [
            Member(100, 100, "My Hero Academia"),
        ]);

        var results = index.Match("academia", exact: false);

        Assert.Single(results);
    }
}
