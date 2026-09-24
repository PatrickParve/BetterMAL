using System.Text;
using System.Text.Json;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.IdMapping;

namespace AnimeTracker.Api.Tests.Services.IdMapping;

// external-id-mapping spec: AnimeIdMappingParser keeps four values per MAL id
// out of the Fribb anime-list-mini.json and nothing else. The entries below
// were copied verbatim from the real file (fetched 2026-09-24); the ones that
// are not, because the real file has no example of the shape, say so.
public class AnimeIdMappingParserTests
{
    // Attack on Titan: a TV season with TMDB season 1 and one IMDb id.
    private const string AttackOnTitan =
        """{"type":"TV","anidb_id":9541,"anilist_id":16498,"animecountdown_id":39687,"animenewsnetwork_id":14950,"anime-planet_id":"attack-on-titan","anisearch_id":8219,"imdb_id":["tt2560140"],"kitsu_id":7442,"livechart_id":38,"mal_id":16498,"simkl_id":39687,"themoviedb_id":{"tv":1429},"tvdb_id":267440,"season":{"tvdb":1,"tmdb":1}}""";

    // Demon Slayer: Mugen Train: a movie, with a TheTVDB-only season and an
    // episode offset. Also carries a type, AniList/AniDB/Kitsu ids and a
    // TheTVDB id, none of which may be stored.
    private const string MugenTrain =
        """{"type":"MOVIE","anidb_id":15113,"anilist_id":112151,"animecountdown_id":1176707,"animenewsnetwork_id":23040,"anime-planet_id":"demon-slayer-kimetsu-no-yaiba-movie-mugen-train","anisearch_id":14653,"imdb_id":["tt11032374"],"kitsu_id":42586,"livechart_id":9614,"mal_id":40456,"simkl_id":1176707,"themoviedb_id":{"movie":[635302]},"tvdb_id":348545,"season":{"tvdb":0},"episode_offset":{"tvdb":1}}""";

    // Naruto: a whole show, so a TV id and no season at all.
    private const string Naruto =
        """{"type":"TV","anidb_id":239,"anilist_id":20,"animecountdown_id":39508,"animenewsnetwork_id":1825,"anime-planet_id":"naruto","anisearch_id":2788,"imdb_id":["tt0409591"],"kitsu_id":11,"livechart_id":3585,"mal_id":20,"simkl_id":39508,"themoviedb_id":{"tv":46260},"tvdb_id":78857}""";

    // A movie whose IMDb list holds an empty string beside its real id.
    private const string MaabouNoDaikyousou =
        """{"type":"MOVIE","anidb_id":6724,"anilist_id":6840,"animecountdown_id":38102,"anime-planet_id":"maabou-no-daikyousou","anisearch_id":5666,"imdb_id":["tt1092390",""],"kitsu_id":4742,"mal_id":6840,"simkl_id":38102,"themoviedb_id":{"movie":[380621]}}""";

    // A special filed under TMDB season 0 (the "Specials" bucket).
    private const string CrestOfTheStarsBirth =
        """{"type":"SPECIAL","anidb_id":6,"anilist_id":1124,"animecountdown_id":40816,"animenewsnetwork_id":787,"anime-planet_id":"crest-of-the-stars-birth","anisearch_id":3304,"imdb_id":["tt0286390"],"kitsu_id":1008,"livechart_id":6910,"mal_id":1124,"simkl_id":40816,"themoviedb_id":{"tv":26209},"tvdb_id":72025,"season":{"tvdb":0,"tmdb":0}}""";

    // A movie with two IMDb ids, for different releases.
    private const string NightOnTheGalacticRailroad =
        """{"type":"MOVIE","anidb_id":1250,"anilist_id":1441,"animecountdown_id":37340,"animenewsnetwork_id":51,"anime-planet_id":"night-on-the-galactic-railroad","anisearch_id":1102,"imdb_id":["tt1920940","tt0089206"],"kitsu_id":1294,"livechart_id":6241,"mal_id":1441,"simkl_id":37340,"themoviedb_id":{"movie":[37585]}}""";

    // A movie split across two TMDB movie ids.
    private const string RurouniKenshinNewKyotoArc =
        """{"type":"OVA","anidb_id":8595,"anilist_id":11441,"animecountdown_id":38395,"animenewsnetwork_id":13316,"anime-planet_id":"rurouni-kenshin-new-kyoto-arc","anisearch_id":7253,"imdb_id":["tt2361423"],"kitsu_id":6509,"livechart_id":5229,"mal_id":11441,"simkl_id":38395,"themoviedb_id":{"movie":[145675,210227]},"tvdb_id":70863,"season":{"tvdb":0},"episode_offset":{"tvdb":7}}""";

    // A real entry with a MAL id, an AniList id and other sites' ids, but no
    // TMDB id and no IMDb id.
    private const string HanaukyoMaidTeam =
        """{"type":"TV","anidb_id":54,"anilist_id":403,"animecountdown_id":36516,"animenewsnetwork_id":505,"anime-planet_id":"hanaukyo-maid-team","anisearch_id":2252,"kitsu_id":370,"livechart_id":4742,"mal_id":403,"simkl_id":36516,"tvdb_id":80654,"season":{"tvdb":1}}""";

    // A real entry with TMDB and IMDb ids but no MAL id.
    private const string NoMalId =
        """{"type":"OVA","anidb_id":71,"animecountdown_id":38805,"imdb_id":["tt0106076"],"simkl_id":38805,"themoviedb_id":{"tv":34189},"tvdb_id":71279,"season":{"tvdb":0,"tmdb":0}}""";

    // A real entry whose only id is AniList's (and a few other sites').
    private const string AniListOnly =
        """{"type":"TV","anidb_id":2267,"anilist_id":170427,"animecountdown_id":39833,"anisearch_id":363,"simkl_id":39833}""";

    private static async Task<Dictionary<int, AnimeIdMapping>> ParseAsync(params string[] entries)
    {
        var json = "[" + string.Join(",", entries) + "]";
        return await AnimeIdMappingParser.ParseAsync(new MemoryStream(Encoding.UTF8.GetBytes(json)));
    }

    [Fact]
    public async Task ATvSeasonKeepsItsTvIdSeasonAndImdbId()
    {
        var result = await ParseAsync(AttackOnTitan);

        var (malId, mapping) = Assert.Single(result);
        Assert.Equal(16498, malId);
        Assert.Equal(16498, mapping.AnimeId);
        Assert.Equal(1429, mapping.TmdbTvId);
        Assert.Equal(1, mapping.TmdbSeasonNumber);
        Assert.Empty(mapping.TmdbMovieIds);
        Assert.Equal(["tt2560140"], mapping.ImdbIds);
    }

    [Fact]
    public async Task AMovieKeepsItsMovieIdsAndNoSeasonWhenTheSeasonIsTheTvdbs()
    {
        var result = await ParseAsync(MugenTrain);

        var mapping = result[40456];
        Assert.Null(mapping.TmdbTvId);
        Assert.Null(mapping.TmdbSeasonNumber);
        Assert.Equal([635302], mapping.TmdbMovieIds);
        Assert.Equal(["tt11032374"], mapping.ImdbIds);
    }

    [Fact]
    public async Task AWholeShowWithNoSeasonKeepsTheTvIdAndANullSeason()
    {
        var result = await ParseAsync(Naruto);

        var mapping = result[20];
        Assert.Equal(46260, mapping.TmdbTvId);
        Assert.Null(mapping.TmdbSeasonNumber);
        Assert.Equal(["tt0409591"], mapping.ImdbIds);
    }

    [Fact]
    public async Task AnEmptyImdbValueIsDropped()
    {
        var result = await ParseAsync(MaabouNoDaikyousou);

        Assert.Equal(["tt1092390"], result[6840].ImdbIds);
        Assert.Equal([380621], result[6840].TmdbMovieIds);
    }

    [Fact]
    public async Task SeasonZeroIsStoredAsGiven()
    {
        var result = await ParseAsync(CrestOfTheStarsBirth);

        // The skip-season-0 rule is applied at read time (design.md D7), not here.
        Assert.Equal(26209, result[1124].TmdbTvId);
        Assert.Equal(0, result[1124].TmdbSeasonNumber);
    }

    [Fact]
    public async Task ASeasonIsKeptOnlyAlongsideATvId()
    {
        // Synthetic: the real file has no entry with a TMDB season and no TV id.
        var movieWithATmdbSeason = MugenTrain.Replace("\"season\":{\"tvdb\":0}", "\"season\":{\"tvdb\":0,\"tmdb\":2}");
        Assert.Contains("\"tmdb\":2", movieWithATmdbSeason);

        var result = await ParseAsync(movieWithATmdbSeason);

        Assert.Null(result[40456].TmdbSeasonNumber);
        Assert.Equal([635302], result[40456].TmdbMovieIds);
    }

    [Fact]
    public async Task SeveralImdbIdsAreKeptInTheFilesOrder()
    {
        var result = await ParseAsync(NightOnTheGalacticRailroad);

        Assert.Equal(["tt1920940", "tt0089206"], result[1441].ImdbIds);
    }

    [Fact]
    public async Task SeveralMovieIdsAreKeptInTheFilesOrder()
    {
        var result = await ParseAsync(RurouniKenshinNewKyotoArc);

        Assert.Equal([145675, 210227], result[11441].TmdbMovieIds);
    }

    [Fact]
    public async Task ImdbIdsAreValidatedAndDeduplicatedInOrder()
    {
        // Synthetic: anything that is not "tt" + digits goes, and a repeat
        // keeps its first place.
        var entry = Naruto.Replace(
            "\"imdb_id\":[\"tt0409591\"]",
            "\"imdb_id\":[\"tt0000002\",\"nm0000001\",\"tt\",\"tt12a\",\" tt123\",\"tt0000001\",\"tt0000002\",\"\"]");
        Assert.Contains("nm0000001", entry);

        var result = await ParseAsync(entry);

        Assert.Equal(["tt0000002", "tt0000001"], result[20].ImdbIds);
    }

    [Fact]
    public async Task TheFirstEntryForAMalIdWins()
    {
        // Synthetic: the real file has no duplicate MAL ids today.
        var laterDuplicate = AttackOnTitan.Replace("\"tv\":1429", "\"tv\":99999").Replace("tt2560140", "tt9999999");
        Assert.Contains("99999", laterDuplicate);

        var result = await ParseAsync(AttackOnTitan, laterDuplicate);

        var mapping = Assert.Single(result).Value;
        Assert.Equal(1429, mapping.TmdbTvId);
        Assert.Equal(["tt2560140"], mapping.ImdbIds);
    }

    [Fact]
    public async Task AFirstEntryThatKeepsNothingStillBlocksALaterDuplicate()
    {
        var laterDuplicate = HanaukyoMaidTeam.Replace("\"mal_id\":403", "\"mal_id\":403,\"themoviedb_id\":{\"tv\":77777}");
        Assert.Contains("77777", laterDuplicate);

        var result = await ParseAsync(HanaukyoMaidTeam, laterDuplicate);

        Assert.Empty(result);
    }

    [Fact]
    public async Task AnEntryWithoutAMalIdContributesNothing()
    {
        var result = await ParseAsync(NoMalId, AniListOnly, Naruto);

        Assert.Equal([20], result.Keys);
    }

    [Fact]
    public async Task AnEntryWithAMalIdButOnlyOtherSitesIdsLeavesNoMapping()
    {
        var result = await ParseAsync(HanaukyoMaidTeam);

        Assert.Empty(result);
    }

    [Fact]
    public async Task ABareStringImdbIdIsAcceptedAsAOneElementList()
    {
        // Synthetic: today's file always gives an array.
        var entry = Naruto.Replace("\"imdb_id\":[\"tt0409591\"]", "\"imdb_id\":\"tt0409591\"");
        Assert.Contains("\"imdb_id\":\"tt0409591\"", entry);

        var result = await ParseAsync(entry);

        Assert.Equal(["tt0409591"], result[20].ImdbIds);
    }

    [Fact]
    public async Task ABareNumberThemoviedbIdThrows()
    {
        // Synthetic: the shape the file used before it became an object.
        // Guessing what a bare number means would silently corrupt the mapping.
        var entry = Naruto.Replace("\"themoviedb_id\":{\"tv\":46260}", "\"themoviedb_id\":46260");
        Assert.Contains("\"themoviedb_id\":46260", entry);

        await Assert.ThrowsAsync<JsonException>(() => ParseAsync(entry));
    }

    [Theory]
    [InlineData("\"imdb_id\":[\"tt0409591\",7]")] // a non-string element
    [InlineData("\"imdb_id\":7")] // a bare number
    [InlineData("\"imdb_id\":{\"id\":\"tt0409591\"}")] // an object
    public async Task AnyOtherImdbShapeThrows(string imdbMember)
    {
        var entry = Naruto.Replace("\"imdb_id\":[\"tt0409591\"]", imdbMember);
        Assert.Contains(imdbMember, entry);

        await Assert.ThrowsAsync<JsonException>(() => ParseAsync(entry));
    }

    [Fact]
    public async Task ATruncatedFileThrowsRatherThanReturningAPartialMapping()
    {
        var truncated = "[" + AttackOnTitan + "," + Naruto[..(Naruto.Length / 2)];

        await Assert.ThrowsAsync<JsonException>(() =>
            AnimeIdMappingParser.ParseAsync(new MemoryStream(Encoding.UTF8.GetBytes(truncated))));
    }

    [Fact]
    public async Task EverythingElseInAnEntryIsDiscarded()
    {
        // MugenTrain carries a type, AniList/AniDB/Kitsu ids, a TheTVDB id and
        // an episode offset.
        var result = await ParseAsync(MugenTrain);

        // Case-sensitive: the mapping's own "TmdbMovieIds" is not the type "MOVIE".
        var json = JsonSerializer.Serialize(result);
        foreach (var discarded in new[] { "MOVIE", "112151", "15113", "42586", "348545", "mugen-train", "episode", "anilist", "tvdb" })
            Assert.DoesNotContain(discarded, json, StringComparison.Ordinal);
    }

    [Fact]
    public void TheMappingHoldsExactlyTheFourKeptValuesAndItsKey()
    {
        var properties = typeof(AnimeIdMapping).GetProperties().Select(p => p.Name).Order(StringComparer.Ordinal);

        Assert.Equal(
            ["AnimeId", "ImdbIds", "TmdbMovieIds", "TmdbSeasonNumber", "TmdbTvId"],
            properties);
    }
}
