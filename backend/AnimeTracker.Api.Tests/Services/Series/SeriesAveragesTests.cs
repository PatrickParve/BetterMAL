using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Series;

namespace AnimeTracker.Api.Tests.Services.Series;

// SeriesAverages is the shared "mean of scores over a group" computation
// lifted out of SeriesService (design.md decision 1/task 1.1) so
// SeriesRankingLookup can reuse it without drifting from the series page.
// These tests pin the score-0-means-unscored rule, the null-when-nothing-
// scored case, the count fields, and — since Mal/Mine used to be private
// statics on SeriesService computing exactly these numbers over
// SeriesMembers — a parity check against that same shape of input (task
// 1.2/12.1).
public class SeriesAveragesTests
{
    [Fact]
    public void Mal_AveragesOnlyNonNullScores()
    {
        var result = SeriesAverages.Mal([8.5, null, 7.0, null]);

        Assert.Equal(7.75, result.Value);
        Assert.Equal(2, result.ScoredCount);
        Assert.Equal(4, result.TotalCount);
    }

    [Fact]
    public void Mal_NullWhenNothingScored()
    {
        var result = SeriesAverages.Mal([null, null]);

        Assert.Null(result.Value);
        Assert.Equal(0, result.ScoredCount);
        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public void Mine_ExcludesScoreZeroAsUnscored()
    {
        // MAL's score of 0 means "unscored", not a rating (design.md
        // decision 8 of the series-page redesign) — it must not pull the
        // average down or count as a scored entry.
        var result = SeriesAverages.Mine([9, 0, 7, null]);

        Assert.Equal(8.0, result.Value);
        Assert.Equal(2, result.ScoredCount);
        Assert.Equal(4, result.TotalCount);
    }

    [Fact]
    public void Mine_NullWhenNothingScored()
    {
        var result = SeriesAverages.Mine([0, null]);

        Assert.Null(result.Value);
        Assert.Equal(0, result.ScoredCount);
        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public void Mine_CountsIncludeEveryMemberRegardlessOfScoreState()
    {
        var result = SeriesAverages.Mine([10, 0, null, 8]);

        Assert.Equal(2, result.ScoredCount);
        Assert.Equal(4, result.TotalCount);
    }

    // Parity with the pre-extraction SeriesService.MalAverage/MyAverage: same
    // inputs (scores pulled straight off SeriesMember.Anime), same outputs.
    [Fact]
    public void ParityWithSeriesMemberDerivedScores()
    {
        var members = new List<SeriesMember>
        {
            MemberWithScores(malScore: 9.1, myScore: 9),
            MemberWithScores(malScore: 7.8, myScore: 0), // MAL score present, but unscored by me
            MemberWithScores(malScore: null, myScore: 8), // not yet MAL-scored, but I scored it
        };

        var mal = SeriesAverages.Mal(members.Select(m => m.Anime.MalScore));
        var mine = SeriesAverages.Mine(members.Select(m => m.Anime.UserEntry?.MyScore));

        Assert.Equal((9.1 + 7.8) / 2, mal.Value);
        Assert.Equal(2, mal.ScoredCount);
        Assert.Equal(3, mal.TotalCount);

        Assert.Equal((9.0 + 8.0) / 2, mine.Value);
        Assert.Equal(2, mine.ScoredCount);
        Assert.Equal(3, mine.TotalCount);
    }

    [Theory]
    [InlineData("finished_airing", WatchStatus.Completed, true)]
    [InlineData("finished_airing", WatchStatus.Watching, false)]
    [InlineData("currently_airing", null, false)]
    public void MainLineCompletedByMe_RequiresEveryFinishedAiringMemberCompleted(
        string airingStatus, WatchStatus? entryStatus, bool expected)
    {
        var result = SeriesAverages.MainLineCompletedByMe([(airingStatus, entryStatus)]);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void MainLineCompletedByMe_FalseWhenNoMemberHasFinishedAiring()
    {
        var result = SeriesAverages.MainLineCompletedByMe([("currently_airing", WatchStatus.Watching), ("not_yet_aired", null)]);

        Assert.False(result);
    }

    private static SeriesMember MemberWithScores(double? malScore, int? myScore) =>
        new()
        {
            AnimeId = 1,
            SeriesId = 1,
            Anime = new AnimeMetadata
            {
                Id = 1,
                Title = "Anime",
                MalScore = malScore,
                UserEntry = new UserAnimeEntry { AnimeId = 1, MyScore = myScore },
            },
        };
}
