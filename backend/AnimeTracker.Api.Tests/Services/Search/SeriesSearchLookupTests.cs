using AnimeTracker.Api.Services.Search;

namespace AnimeTracker.Api.Tests.Services.Search;

// SeriesSearchLookup itself is a thin SeriesMembers ⋈ AnimeMetadata query; the
// matching/collapsing/ordering logic it feeds lives in SeriesSearchIndex, so
// these tests build the index directly from projections (as LoadAsync would)
// rather than going through a database.
public class SeriesSearchLookupTests
{
    private static SeriesMemberProjection Member(
        int seriesId, int animeId, string title, int rootAnimeId,
        string? englishTitle = null, string? pictureUrl = null, bool isMainLine = true, int? popularityRank = null,
        string? selectedTitle = null, string? selectedPictureUrl = null) =>
        new(seriesId, animeId, title, englishTitle, pictureUrl, isMainLine, popularityRank, rootAnimeId, selectedTitle, selectedPictureUrl);

    [Fact]
    public void MatchesOnNonRootMembersTitle()
    {
        var index = new SeriesSearchIndex(
        [
            Member(1, 100, "Attack on Titan", rootAnimeId: 100, pictureUrl: "root.jpg"),
            Member(1, 101, "Attack on Titan Final Season", rootAnimeId: 100),
        ]);

        var results = index.Match("final season", exact: false);

        var series = Assert.Single(results);
        Assert.Equal(1, series.SeriesId);
    }

    [Fact]
    public void DisplayFieldsComeFromRootMemberRegardlessOfWhichMemberMatched()
    {
        var index = new SeriesSearchIndex(
        [
            Member(1, 100, "Attack on Titan", rootAnimeId: 100, englishTitle: "Attack on Titan", pictureUrl: "root.jpg"),
            Member(1, 101, "Shingeki no Kyojin: Final Season", rootAnimeId: 100, englishTitle: null, pictureUrl: "final-season.jpg"),
        ]);

        var results = index.Match("shingeki", exact: false);

        var series = Assert.Single(results);
        Assert.Equal(100, series.RootAnimeId);
        Assert.Equal("Attack on Titan", series.Title);
        Assert.Equal("Attack on Titan", series.EnglishTitle);
        Assert.Equal("root.jpg", series.PictureUrl);
    }

    [Fact]
    public void CollapsesToOneRowPerSeriesKeepingTheStrongestMatchQuality()
    {
        var index = new SeriesSearchIndex(
        [
            // Series 1: one member only contains-matches, its sibling prefix-matches —
            // the collapsed row should carry the stronger (prefix) quality.
            Member(1, 100, "The Great Attack Chronicles", rootAnimeId: 100),
            Member(1, 101, "Attack on Titan", rootAnimeId: 100),
            // Series 2: every member is contains-only, never prefix — should rank behind series 1.
            Member(2, 200, "Some Other Attack Story", rootAnimeId: 200),
        ]);

        var results = index.Match("attack", exact: false);

        Assert.Equal(2, results.Count); // collapsed to one row per series, not per member
        Assert.Equal(1, results[0].SeriesId); // prefix-quality series ranks ahead of contains-only
        Assert.Equal(2, results[1].SeriesId);
    }

    [Fact]
    public void ExactModeOnlyMatchesEqualTitles()
    {
        var index = new SeriesSearchIndex(
        [
            Member(1, 100, "Attack on Titan", rootAnimeId: 100),
            Member(1, 101, "Attack on Titan Final Season", rootAnimeId: 100),
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
            Member(1, 100, "Attack on Titan", rootAnimeId: 100, isMainLine: true),
            Member(1, 101, "Attack on Titan Season 2", rootAnimeId: 100, isMainLine: true),
            Member(1, 102, "Attack on Titan Season 3", rootAnimeId: 100, isMainLine: true),
            Member(1, 103, "Attack on Titan Final Season", rootAnimeId: 100, isMainLine: true),
            Member(1, 104, "Attack on Titan: No Regrets (OVA)", rootAnimeId: 100, isMainLine: false),
            Member(1, 105, "Attack on Titan: Lost Girls (OVA)", rootAnimeId: 100, isMainLine: false),
            Member(1, 106, "Attack on Titan: Chronicle (Recap)", rootAnimeId: 100, isMainLine: false),
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
            Member(1, 100, "Attack on Titan", rootAnimeId: 100),
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
            Member(1, 100, "Beyblade: Metal Fusion", rootAnimeId: 100, selectedTitle: "Beyblade"),
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
            Member(1, 100, "Beyblade: Metal Fusion", rootAnimeId: 100, selectedTitle: "Beyblade"),
        ]);

        var results = index.Match("Beyblade", exact: false);

        Assert.Single(results);
    }

    [Fact]
    public void RenamedSeriesIsDisplayedUnderItsChosenTitle()
    {
        var index = new SeriesSearchIndex(
        [
            Member(1, 100, "Beyblade: Metal Fusion", rootAnimeId: 100, englishTitle: "Beyblade: Metal Fusion", selectedTitle: "Beyblade"),
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
            Member(1, 100, "Re:ZERO -Starting Life in Another World-", rootAnimeId: 100),
        ]);

        var results = index.Match("re zero starting life", exact: false);

        var series = Assert.Single(results);
        Assert.Equal(1, series.SeriesId);
    }

    [Fact]
    public void ChosenTitleWithPunctuationMatchedByADePunctuatedQuery()
    {
        var index = new SeriesSearchIndex(
        [
            // The member's own title doesn't match at all — only the chosen
            // (renamed) title does, once its punctuation is stripped.
            Member(1, 100, "Hagane no Renkinjutsushi", rootAnimeId: 100, selectedTitle: "Full-Metal: Alchemist!"),
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
            Member(1, 100, "Kaguya-sama: Love is War", rootAnimeId: 100),
            // Contains only: the query sits in the middle of the de-punctuated title.
            Member(2, 200, "Some Preamble Kaguya-sama Love Story", rootAnimeId: 200),
        ]);

        var results = index.Match("kaguya sama love", exact: false);

        Assert.Equal([1, 2], results.Select(s => s.SeriesId));
    }
}
