using AnimeTracker.Api.Services.Games;

namespace AnimeTracker.Api.Tests.Services.Games;

// add-guess-the-anime design D8: Compare is a pure function of two AnimeStats, so
// each stat's rule is pinned here on its own, with every other stat held equal.
public class GuessComparisonTests
{
    private static readonly DateOnly Aired = new(2013, 4, 7);

    private static AnimeStats Stats(
        int id = 1,
        DateOnly? airedFrom = null,
        List<string>? genres = null,
        int? episodes = 12,
        List<string>? studios = null,
        string? mediaType = "tv",
        double? score = 7.5) =>
        new(id, $"Anime {id}", $"English {id}", $"https://img/{id}.jpg", airedFrom ?? Aired,
            genres ?? ["Action"], episodes, studios ?? ["Madhouse"], mediaType, score);

    private static AnimeStats Unknown(int id = 1) =>
        new(id, $"Anime {id}", null, null, null, null, null, null, null, null);

    private static GuessTheAnimeRowDto Compare(AnimeStats answer, AnimeStats guess) =>
        GuessComparison.Compare(answer, guess);

    // ---- the row is the guess's ----

    [Fact]
    public void TheRowCarriesTheGuessesPictureAndTitles()
    {
        var row = Compare(Stats(1), Stats(2));

        Assert.Equal(2, row.AnimeId);
        Assert.Equal("Anime 2", row.Title);
        Assert.Equal("English 2", row.EnglishTitle);
        Assert.Equal("https://img/2.jpg", row.PictureUrl);
    }

    // ---- year ----

    [Fact]
    public void TheSameYearIsSame()
    {
        var year = Compare(Stats(airedFrom: new DateOnly(2013, 1, 1)), Stats(airedFrom: new DateOnly(2013, 12, 31))).Year;

        Assert.Equal(2013, year.Value);
        Assert.Equal(GuessCompared.Same, year.Compared);
    }

    [Fact]
    public void AGuessFromBeforeTheAnswerSaysTheAnswerIsHigher()
    {
        var year = Compare(Stats(airedFrom: new DateOnly(2019, 1, 1)), Stats(airedFrom: new DateOnly(2013, 1, 1))).Year;

        Assert.Equal(2013, year.Value);
        Assert.Equal(GuessCompared.AnswerHigher, year.Compared);
    }

    [Fact]
    public void AGuessFromAfterTheAnswerSaysTheAnswerIsLower()
    {
        var year = Compare(Stats(airedFrom: new DateOnly(2013, 1, 1)), Stats(airedFrom: new DateOnly(2019, 1, 1))).Year;

        Assert.Equal(GuessCompared.AnswerLower, year.Compared);
    }

    [Fact]
    public void AGuessWithNoStartDateHasNoYearToCompare()
    {
        var year = Compare(Stats(), Unknown()).Year;

        Assert.Null(year.Value);
        Assert.Null(year.Compared);
    }

    [Fact]
    public void AnAnswerWithNoStartDateLeavesTheGuessesYearUncompared()
    {
        var year = Compare(Unknown(), Stats()).Year;

        Assert.Equal(2013, year.Value);
        Assert.Null(year.Compared);
    }

    [Fact]
    public void TwoUnknownYearsAreNotAMatch()
    {
        var year = Compare(Unknown(1), Unknown(2)).Year;

        Assert.Null(year.Value);
        Assert.Null(year.Compared);
    }

    // ---- season ----

    [Theory]
    [InlineData(2013, 1, 1, "winter")]
    [InlineData(2013, 3, 31, "winter")]
    [InlineData(2013, 4, 1, "spring")]
    [InlineData(2013, 6, 30, "spring")]
    [InlineData(2013, 7, 1, "summer")]
    [InlineData(2013, 9, 30, "summer")]
    [InlineData(2013, 10, 1, "fall")]
    [InlineData(2013, 12, 31, "fall")]
    public void TheSeasonIsTheQuarterTheStartDateFallsIn(int year, int month, int day, string season)
    {
        var row = Compare(Stats(), Stats(airedFrom: new DateOnly(year, month, day)));

        Assert.Equal(season, row.Season.Value);
    }

    [Fact]
    public void TheSameSeasonOfAnotherYearIsSame()
    {
        var answer = Stats(airedFrom: new DateOnly(2019, 5, 10));
        var guess = Stats(airedFrom: new DateOnly(2013, 4, 7));

        var row = Compare(answer, guess);

        Assert.True(row.Season.Same);
        Assert.NotEqual(GuessCompared.Same, row.Year.Compared);
    }

    [Theory]
    [InlineData(3, 31, 4, 1)]
    [InlineData(6, 30, 7, 1)]
    [InlineData(9, 30, 10, 1)]
    [InlineData(12, 31, 1, 1)]
    public void AdjacentQuartersAreNotTheSameSeason(int answerMonth, int answerDay, int guessMonth, int guessDay)
    {
        var row = Compare(
            Stats(airedFrom: new DateOnly(2013, answerMonth, answerDay)),
            Stats(airedFrom: new DateOnly(2013, guessMonth, guessDay)));

        Assert.False(row.Season.Same);
    }

    [Fact]
    public void ASeasonNeedsAStartDateOnBothSides()
    {
        var noGuessDate = Compare(Stats(), Unknown()).Season;
        var noAnswerDate = Compare(Unknown(), Stats()).Season;
        var neither = Compare(Unknown(1), Unknown(2)).Season;

        Assert.Equal((null, false), (noGuessDate.Value, noGuessDate.Same));
        Assert.Equal(("spring", false), (noAnswerDate.Value, noAnswerDate.Same));
        Assert.Equal((null, false), (neither.Value, neither.Same));
    }

    // ---- episodes ----

    [Theory]
    [InlineData(26, 26, GuessCompared.Same)]
    [InlineData(26, 148, GuessCompared.AnswerLower)]
    [InlineData(148, 26, GuessCompared.AnswerHigher)]
    public void EpisodesAreComparedByCount(int answer, int guess, GuessCompared expected)
    {
        var episodes = Compare(Stats(episodes: answer), Stats(episodes: guess)).Episodes;

        Assert.Equal(guess, episodes.Value);
        Assert.Equal(expected, episodes.Compared);
    }

    [Fact]
    public void AnUnknownEpisodeCountOnEitherSideIsNotCompared()
    {
        var noGuessTotal = Compare(Stats(episodes: 26), Stats(episodes: null)).Episodes;
        var noAnswerTotal = Compare(Stats(episodes: null), Stats(episodes: 26)).Episodes;
        var neither = Compare(Stats(episodes: null), Stats(episodes: null)).Episodes;

        Assert.Equal((null, null), (noGuessTotal.Value, noGuessTotal.Compared));
        Assert.Equal((26.0, null), (noAnswerTotal.Value, noAnswerTotal.Compared));
        Assert.Equal((null, null), (neither.Value, neither.Compared));
    }

    // ---- type ----

    [Fact]
    public void TheSameTypeIsSame()
    {
        var type = Compare(Stats(mediaType: "movie"), Stats(mediaType: "movie")).MediaType;

        Assert.Equal("movie", type.Value);
        Assert.True(type.Same);
    }

    [Fact]
    public void ADifferentTypeIsNotSame()
    {
        var type = Compare(Stats(mediaType: "tv"), Stats(mediaType: "ova")).MediaType;

        Assert.Equal("ova", type.Value);
        Assert.False(type.Same);
    }

    [Fact]
    public void AnUnknownTypeIsNeverSame()
    {
        Assert.False(Compare(Stats(mediaType: "tv"), Stats(mediaType: null)).MediaType.Same);
        Assert.False(Compare(Stats(mediaType: null), Stats(mediaType: "tv")).MediaType.Same);
        Assert.False(Compare(Stats(mediaType: null), Stats(mediaType: null)).MediaType.Same);
    }

    // ---- score ----

    [Theory]
    [InlineData(8.39, 8.58, GuessCompared.AnswerLower)]
    [InlineData(8.58, 8.39, GuessCompared.AnswerHigher)]
    [InlineData(8.39, 8.39, GuessCompared.Same)]
    public void ScoresAreComparedNumerically(double answer, double guess, GuessCompared expected)
    {
        var score = Compare(Stats(score: answer), Stats(score: guess)).Score;

        Assert.Equal(guess, score.Value);
        Assert.Equal(expected, score.Compared);
    }

    [Theory]
    [InlineData(7.85, 7.8500000001)]
    [InlineData(7.85, 7.854)]
    [InlineData(7.85, 7.846)]
    [InlineData(0.1 + 0.2, 0.3)]
    public void ScoresThatAgreeAtTwoDecimalsAreSame(double answer, double guess)
    {
        Assert.Equal(GuessCompared.Same, Compare(Stats(score: answer), Stats(score: guess)).Score.Compared);
    }

    [Fact]
    public void ScoresThatDifferAtTwoDecimalsAreNotSame()
    {
        Assert.Equal(GuessCompared.AnswerHigher, Compare(Stats(score: 7.86), Stats(score: 7.85)).Score.Compared);
        Assert.Equal(GuessCompared.AnswerLower, Compare(Stats(score: 7.85), Stats(score: 7.86)).Score.Compared);
    }

    [Fact]
    public void AnUnknownScoreOnEitherSideIsNotCompared()
    {
        Assert.Null(Compare(Stats(score: 7.5), Stats(score: null)).Score.Compared);
        Assert.Null(Compare(Stats(score: null), Stats(score: 7.5)).Score.Compared);
        Assert.Null(Compare(Stats(score: null), Stats(score: null)).Score.Compared);
    }

    // ---- genres and studios ----

    [Fact]
    public void GenresTheAnswerHasAreSameOnTheirOwn()
    {
        var genres = Compare(Stats(genres: ["Action", "Fantasy"]), Stats(genres: ["Action", "Drama", "Military"])).Genres;

        Assert.Equal(
            [("Action", true), ("Drama", false), ("Military", false)],
            genres.Select(g => (g.Name, g.Same)));
    }

    [Fact]
    public void NoSharedGenresAreAllUncoloured()
    {
        var genres = Compare(Stats(genres: ["Comedy"]), Stats(genres: ["Action", "Drama"])).Genres;

        Assert.All(genres, g => Assert.False(g.Same));
    }

    [Fact]
    public void EveryGenreSharedIsAllSame()
    {
        var genres = Compare(Stats(genres: ["Action", "Drama", "Comedy"]), Stats(genres: ["Drama", "Action"])).Genres;

        Assert.All(genres, g => Assert.True(g.Same));
    }

    [Fact]
    public void GenresStayInMyAnimeListsOrderWhateverMatches()
    {
        var guess = Stats(genres: ["Sci-Fi", "Action", "Mystery", "Drama"]);

        var genres = Compare(Stats(genres: ["Drama", "Sci-Fi"]), guess).Genres;

        Assert.Equal(["Sci-Fi", "Action", "Mystery", "Drama"], genres.Select(g => g.Name));
    }

    [Fact]
    public void ADuplicateInTheGuessIsKept()
    {
        var genres = Compare(Stats(genres: ["Action"]), Stats(genres: ["Action", "Action"])).Genres;

        Assert.Equal(2, genres.Count);
    }

    [Fact]
    public void NoGenresOnTheGuessIsNoTags()
    {
        Assert.Empty(Compare(Stats(), Stats(genres: [])).Genres);
        Assert.Empty(Compare(Stats(), Unknown()).Genres);
    }

    [Fact]
    public void NoGenresOnTheAnswerMatchesNothing()
    {
        Assert.All(Compare(Stats(genres: []), Stats(genres: ["Action"])).Genres, g => Assert.False(g.Same));
        Assert.All(Compare(Unknown(), Stats(genres: ["Action"])).Genres, g => Assert.False(g.Same));
    }

    [Fact]
    public void OneOfSeveralStudiosMatches()
    {
        var studios = Compare(Stats(studios: ["Bones"]), Stats(studios: ["Madhouse", "Bones"])).Studios;

        Assert.Equal([("Madhouse", false), ("Bones", true)], studios.Select(s => (s.Name, s.Same)));
    }

    [Fact]
    public void StudiosDoNotMatchWhenNoneIsShared()
    {
        var studios = Compare(Stats(studios: ["Bones"]), Stats(studios: ["Madhouse"])).Studios;

        Assert.All(studios, s => Assert.False(s.Same));
    }

    [Fact]
    public void NoStudiosOnEitherSideIsNoMatch()
    {
        Assert.Empty(Compare(Stats(), Stats(studios: [])).Studios);
        Assert.Empty(Compare(Stats(), Unknown()).Studios);
        Assert.All(Compare(Stats(studios: []), Stats(studios: ["Bones"])).Studios, s => Assert.False(s.Same));
        Assert.All(Compare(Unknown(), Stats(studios: ["Bones"])).Studios, s => Assert.False(s.Same));
    }

    // ---- an anime compared with itself ----

    [Fact]
    public void AnAnimeComparedWithItselfIsAllSame()
    {
        var anime = Stats(
            airedFrom: new DateOnly(2016, 8, 26), genres: ["Drama", "Romance"], episodes: 1,
            studios: ["CoMix Wave Films", "Madhouse"], mediaType: "movie", score: 8.39);

        var row = Compare(anime, anime);

        Assert.Equal(GuessCompared.Same, row.Year.Compared);
        Assert.True(row.Season.Same);
        Assert.All(row.Genres, g => Assert.True(g.Same));
        Assert.Equal(GuessCompared.Same, row.Episodes.Compared);
        Assert.All(row.Studios, s => Assert.True(s.Same));
        Assert.True(row.MediaType.Same);
        Assert.Equal(GuessCompared.Same, row.Score.Compared);
    }

    [Fact]
    public void AStatItDoesNotHaveStaysPlainWhenComparedWithItself()
    {
        // One Piece's shape: no episode total, and no studios ever given.
        var anime = Stats(episodes: null) with { Studios = null, Genres = [] };

        var row = Compare(anime, anime);

        Assert.Null(row.Episodes.Value);
        Assert.Null(row.Episodes.Compared);
        Assert.Empty(row.Studios);
        Assert.Empty(row.Genres);
        // What it does have is Same.
        Assert.Equal(GuessCompared.Same, row.Year.Compared);
        Assert.True(row.MediaType.Same);
    }

    [Fact]
    public void AnAnimeWithNothingKnownComparedWithItselfHasNothingGreen()
    {
        var row = Compare(Unknown(), Unknown());

        Assert.Null(row.Year.Compared);
        Assert.False(row.Season.Same);
        Assert.Empty(row.Genres);
        Assert.Null(row.Episodes.Compared);
        Assert.Empty(row.Studios);
        Assert.False(row.MediaType.Same);
        Assert.Null(row.Score.Compared);
    }
}
