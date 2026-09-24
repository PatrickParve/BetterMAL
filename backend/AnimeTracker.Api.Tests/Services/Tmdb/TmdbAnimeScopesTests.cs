using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Tmdb;

namespace AnimeTracker.Api.Tests.Services.Tmdb;

// tmdb-artwork spec "Which TMDB sets an anime draws from" (design.md D7): the
// Series, Season and Movie keys read off one mapping row.
public class TmdbAnimeScopesTests
{
    private static AnimeIdMapping Mapping(
        int? tvId = null, int? season = null, int[]? movieIds = null, string[]? imdbIds = null) => new()
    {
        AnimeId = 1,
        TmdbTvId = tvId,
        TmdbSeasonNumber = season,
        TmdbMovieIds = movieIds?.ToList() ?? [],
        ImdbIds = imdbIds?.ToList() ?? [],
    };

    [Fact]
    public void ATvIdWithSeasonThreeGivesTheSeriesAndSeasonThreeSets()
    {
        var scopes = TmdbAnimeScopes.For(Mapping(tvId: 1429, season: 3));

        Assert.Equal(TmdbSetKey.Tv(1429), scopes.Series);
        Assert.Equal(TmdbSetKey.Season(1429, 3), scopes.Season);
        Assert.Empty(scopes.Movie);
    }

    [Fact]
    public void ATvIdWithNoSeasonGivesTheSeriesSetOnly()
    {
        var scopes = TmdbAnimeScopes.For(Mapping(tvId: 46260));

        Assert.Equal(TmdbSetKey.Tv(46260), scopes.Series);
        Assert.Null(scopes.Season);
        Assert.Empty(scopes.Movie);
    }

    [Fact]
    public void SeasonZeroGivesTheSeriesSetOnly()
    {
        // TMDB's "Specials" bucket: its posters describe the bucket, not the anime.
        var scopes = TmdbAnimeScopes.For(Mapping(tvId: 46260, season: 0));

        Assert.Equal(TmdbSetKey.Tv(46260), scopes.Series);
        Assert.Null(scopes.Season);
        Assert.DoesNotContain(scopes.Keys, key => key.Scope == TmdbScope.Season);
    }

    [Fact]
    public void OneMovieIdGivesAMovieScope()
    {
        var scopes = TmdbAnimeScopes.For(Mapping(movieIds: [635302]));

        Assert.Null(scopes.Series);
        Assert.Null(scopes.Season);
        Assert.Equal([TmdbSetKey.Movie(635302)], scopes.Movie);
    }

    [Fact]
    public void TwoMovieIdsGiveOneMovieScopeWithBothIdsInOrder()
    {
        var scopes = TmdbAnimeScopes.For(Mapping(movieIds: [7001, 7000]));

        Assert.Equal([TmdbSetKey.Movie(7001), TmdbSetKey.Movie(7000)], scopes.Movie);
    }

    [Fact]
    public void AnImdbOnlyMappingGivesNothing()
    {
        var scopes = TmdbAnimeScopes.For(Mapping(imdbIds: ["tt2560140"]));

        Assert.Null(scopes.Series);
        Assert.Null(scopes.Season);
        Assert.Empty(scopes.Movie);
        Assert.Empty(scopes.Keys);
    }

    [Fact]
    public void NoMappingGivesNothing()
    {
        var scopes = TmdbAnimeScopes.For(null);

        Assert.Null(scopes.Series);
        Assert.Null(scopes.Season);
        Assert.Empty(scopes.Movie);
        Assert.Empty(scopes.Keys);
    }

    [Fact]
    public void KeysComeInFetchOrderSeriesThenSeasonThenMovies()
    {
        // The mapping file never carries a TV id and a movie id together today,
        // but the order is a property of the type, not of the data.
        var scopes = TmdbAnimeScopes.For(Mapping(tvId: 1429, season: 2, movieIds: [9, 8]));

        Assert.Equal(
            [TmdbSetKey.Tv(1429), TmdbSetKey.Season(1429, 2), TmdbSetKey.Movie(9), TmdbSetKey.Movie(8)],
            scopes.Keys);
    }
}
