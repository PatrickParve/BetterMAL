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
        string? englishTitle = null, string? pictureUrl = null, bool isMainLine = true, int? popularityRank = null) =>
        new(seriesId, animeId, title, englishTitle, pictureUrl, isMainLine, popularityRank, rootAnimeId);

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
}
