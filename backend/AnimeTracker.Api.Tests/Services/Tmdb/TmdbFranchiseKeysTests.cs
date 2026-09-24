using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Tmdb;

namespace AnimeTracker.Api.Tests.Services.Tmdb;

// tmdb-artwork spec "Which TMDB sets a series draws from" (design.md D8): TV
// ids from the main line, seasons of those shows from every member, movies
// from every member.
public class TmdbFranchiseKeysTests
{
    private static Dictionary<int, AnimeIdMapping> Mappings(params AnimeIdMapping[] mappings) =>
        mappings.ToDictionary(m => m.AnimeId);

    private static AnimeIdMapping Tv(int animeId, int tvId, int? season = null) =>
        new() { AnimeId = animeId, TmdbTvId = tvId, TmdbSeasonNumber = season };

    private static AnimeIdMapping Movie(int animeId, params int[] movieIds) =>
        new() { AnimeId = animeId, TmdbMovieIds = movieIds.ToList() };

    [Fact]
    public void TheDemonSlayerShapeIsOneShowItsFiveSeasonsAndTheFilmAmongTheExtras()
    {
        // Main line: seasons 1-5 of TV 85937. Extra: the Mugen Train film (MAL 40456).
        int[] mainLine = [38000, 49926, 47778, 51019, 55701];
        var mappings = Mappings(
            Tv(38000, 85937, 1), Tv(49926, 85937, 2), Tv(47778, 85937, 3), Tv(51019, 85937, 4), Tv(55701, 85937, 5),
            Movie(40456, 635302));
        int[] allMembers = [.. mainLine, 40456];

        var keys = TmdbFranchiseKeys.For(mainLine, allMembers, mappings);

        Assert.Equal([TmdbSetKey.Tv(85937)], keys.Series);
        Assert.Equal(
            Enumerable.Range(1, 5).Select(n => TmdbSetKey.Season(85937, n)),
            keys.Seasons);
        Assert.Equal([TmdbSetKey.Movie(635302)], keys.Movies);
    }

    [Fact]
    public void TheNarutoShapeDrawsFromBothMainLineShows()
    {
        // 20 is Naruto (TMDB 46260), 1735 is Shippuden (TMDB 31910). Both have
        // a "whole show" mapping with no season.
        var mappings = Mappings(Tv(20, 46260), Tv(1735, 31910));

        var keys = TmdbFranchiseKeys.For([20, 1735], [20, 1735], mappings);

        Assert.Equal([TmdbSetKey.Tv(46260), TmdbSetKey.Tv(31910)], keys.Series);
        Assert.Empty(keys.Seasons);
        Assert.Empty(keys.Movies);
    }

    [Fact]
    public void ASpinOffExtraWithItsOwnTvIdIsExcluded()
    {
        // "Attack on Titan: Junior High" style: an extra that TMDB lists as a
        // separate show. Neither its series set nor its seasons count.
        var mappings = Mappings(Tv(16498, 1429, 1), Tv(28623, 70000, 1));

        var keys = TmdbFranchiseKeys.For([16498], [16498, 28623], mappings);

        Assert.Equal([TmdbSetKey.Tv(1429)], keys.Series);
        Assert.Equal([TmdbSetKey.Season(1429, 1)], keys.Seasons);
        Assert.DoesNotContain(keys.Keys, key => key.Id == 70000);
    }

    [Fact]
    public void AnExtraMappedToAMainShowsNumberedSeasonIsIncluded()
    {
        // A special MAL files as an extra but TMDB counts as season 6 of the
        // main show, which no main-line member maps to.
        var mappings = Mappings(Tv(1, 1429, 1), Tv(2, 1429, 2), Tv(99, 1429, 6));

        var keys = TmdbFranchiseKeys.For([1, 2], [1, 2, 99], mappings);

        Assert.Equal(new[] { 1, 2, 6 }.Select(n => TmdbSetKey.Season(1429, n)), keys.Seasons);
    }

    [Fact]
    public void AnExtraInTmdbsSpecialsBucketAddsNoSeason()
    {
        var mappings = Mappings(Tv(1, 1429, 1), Tv(99, 1429, 0));

        var keys = TmdbFranchiseKeys.For([1], [1, 99], mappings);

        Assert.Equal([TmdbSetKey.Season(1429, 1)], keys.Seasons);
    }

    [Fact]
    public void AMovieExtraIsIncluded()
    {
        var mappings = Mappings(Tv(1, 1429, 1), Movie(50, 8123));

        var keys = TmdbFranchiseKeys.For([1], [1, 50], mappings);

        Assert.Equal([TmdbSetKey.Movie(8123)], keys.Movies);
    }

    [Fact]
    public void OrderFollowsTheMainLineAndThenTheExtras()
    {
        // TV ids in main-line order; seasons by their show's position and then
        // by number, whichever member holds them; movies main line first, then
        // the extras in the order given.
        var mappings = Mappings(
            Tv(1, 500, 2), Tv(2, 400, 1), Tv(3, 500, 1),
            Movie(4, 30), Movie(5, 10),
            Tv(6, 400, 3), Movie(7, 20));

        var keys = TmdbFranchiseKeys.For([1, 2, 3, 4], [1, 2, 3, 4, 5, 6, 7], mappings);

        Assert.Equal([TmdbSetKey.Tv(500), TmdbSetKey.Tv(400)], keys.Series);
        Assert.Equal(
            [TmdbSetKey.Season(500, 1), TmdbSetKey.Season(500, 2), TmdbSetKey.Season(400, 1), TmdbSetKey.Season(400, 3)],
            keys.Seasons);
        Assert.Equal([TmdbSetKey.Movie(30), TmdbSetKey.Movie(10), TmdbSetKey.Movie(20)], keys.Movies);
    }

    [Fact]
    public void KeysAreDeduplicatedWhereSeveralMembersShareOne()
    {
        // Re:Zero's seasons 2, 2-part-2 and 3 all map to TMDB season 1.
        var mappings = Mappings(Tv(1, 8000, 1), Tv(2, 8000, 1), Tv(3, 8000, 1), Movie(4, 5), Movie(5, 5));

        var keys = TmdbFranchiseKeys.For([1, 2, 3], [1, 2, 3, 4, 5], mappings);

        Assert.Equal([TmdbSetKey.Tv(8000)], keys.Series);
        Assert.Equal([TmdbSetKey.Season(8000, 1)], keys.Seasons);
        Assert.Equal([TmdbSetKey.Movie(5)], keys.Movies);
    }

    [Fact]
    public void AMemberWithNoMappingContributesNothing()
    {
        var keys = TmdbFranchiseKeys.For([1, 2], [1, 2, 3], Mappings(Tv(1, 1429, 1)));

        Assert.Equal([TmdbSetKey.Tv(1429)], keys.Series);
        Assert.Equal([TmdbSetKey.Season(1429, 1)], keys.Seasons);
        Assert.Empty(keys.Movies);
    }

    [Fact]
    public void AnImdbOnlyMemberContributesNothing()
    {
        var mappings = Mappings(new AnimeIdMapping { AnimeId = 1, ImdbIds = ["tt2560140"] });

        var keys = TmdbFranchiseKeys.For([1], [1], mappings);

        Assert.Empty(keys.Keys);
    }

    [Fact]
    public void KeysListSeriesThenSeasonsThenMovies()
    {
        var mappings = Mappings(Tv(1, 1429, 1), Movie(2, 77));

        var keys = TmdbFranchiseKeys.For([1], [1, 2], mappings);

        Assert.Equal([TmdbSetKey.Tv(1429), TmdbSetKey.Season(1429, 1), TmdbSetKey.Movie(77)], keys.Keys);
    }
}
