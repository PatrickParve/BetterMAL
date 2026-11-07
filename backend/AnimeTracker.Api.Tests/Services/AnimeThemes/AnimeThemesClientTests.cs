using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using AnimeTracker.Api.Services.AnimeThemes;

namespace AnimeTracker.Api.Tests.Services.AnimeThemes;

// anime-themes spec: AnimeThemesClient over a fake HttpMessageHandler. Waits are
// recorded, not sat through.
public class AnimeThemesClientTests
{
    private const string EmptyPage = """{"data":{"animePagination":{"paginatorInfo":{"hasMorePages":false},"data":[]}}}""";

    private static (InstantClient Client, FakeAnimeThemesHandler Handler) Create()
    {
        var handler = new FakeAnimeThemesHandler();
        return (new InstantClient(handler), handler);
    }

    // --- Reading the recorded answer ---

    [Fact]
    public async Task ParsesARealRecordedPage()
    {
        var page = await AnimeThemesFixture.ParsedPageAsync();

        Assert.True(page.HasMorePages);
        Assert.Equal([2, 5, 6, 18, 20, 22, 25, 33, 69, 4514], page.Anime.Select(a => a.Id));

        var ald = page.Anime.Single(a => a.Id == 6);
        Assert.Equal([41380], ald.MalIds);
        Assert.Equal(["OP1", "OP2", "ED1"], ald.Themes.Select(t => t.Slug));

        var ed = ald.Themes.Single(t => t.Slug == "ED1");
        Assert.Equal(9399, ed.Id);
        Assert.Equal("ED", ed.Type);
        Assert.Null(ed.Sequence);
        Assert.Equal(9400, ed.Song!.Id);
        Assert.Equal("Carpe Diem", ed.Song.Title);
        Assert.Equal("Liyuu", Assert.Single(ed.Song.Credits).ArtistName);
        Assert.Equal([1, 2, 3, 4], ed.Versions.Select(v => v.Number));
        Assert.Equal("3-9, 11", ed.Versions[1].Episodes);

        var video = ed.Versions[0].Videos.Single(v => v.Clean);
        Assert.Equal(60, video.Priority);
        Assert.Equal("https://v.animethemes.moe/100Man-ED1-NCBD1080.webm", video.VideoUrl);
        Assert.Equal("https://a.animethemes.moe/100Man-ED1-NCBD1080.ogg", video.AudioUrl);
    }

    [Fact]
    public async Task AThemeWithNoSongHasNoSong()
    {
        var page = await AnimeThemesFixture.ParsedPageAsync();

        var theme = page.Anime.Single(a => a.Id == 69).Themes[0];

        Assert.Null(theme.Song);
    }

    [Fact]
    public async Task FlagsAndEveryVersionsEpisodesAreRead()
    {
        var page = await AnimeThemesFixture.ParsedPageAsync();

        var ed = page.Anime.Single(a => a.Id == 20).Themes.Single(t => t.Slug == "ED1");

        Assert.False(ed.Versions[0].Spoiler);
        Assert.True(ed.Versions[1].Spoiler);
        Assert.True(page.Anime.Single(a => a.Id == 18).Themes[0].Versions[0].Nsfw);
    }

    [Fact]
    public async Task TheRecordingPredatesTheNameSoNoAnimeInItHasOne()
    {
        // Recorded before `title { romaji }` joined the query (see AnimeThemesFixture): an answer
        // that sends no title parses, with no name, rather than failing the page.
        var page = await AnimeThemesFixture.ParsedPageAsync();

        Assert.NotEmpty(page.Anime);
        Assert.All(page.Anime, anime => Assert.Null(anime.Name));
    }

    [Fact]
    public async Task AnAnimesNameIsItsRomajiTitle()
    {
        var (client, handler) = Create();
        handler.Enqueue(() => FakeAnimeThemesHandler.Json("""
            {"data":{"animePagination":{"paginatorInfo":{"hasMorePages":false},"data":[
              {"id":1,"title":{"romaji":"Cowboy Bebop"},"resources":{"nodes":[{"externalId":1}]},"animethemes":[]},
              {"id":2,"title":{"romaji":"Kimi no Na wa."},"resources":{"nodes":[]},"animethemes":[]}
            ]}}}
            """));

        var page = await client.GetPageAsync(1);

        Assert.Equal(["Cowboy Bebop", "Kimi no Na wa."], page.Anime.Select(a => a.Name));
        Assert.Equal([1], page.Anime[0].MalIds); // reading the name leaves the rest as it was
    }

    [Theory]
    [InlineData("""{"id":1,"animethemes":[]}""", null)]                                 // no title at all
    [InlineData("""{"id":1,"title":null,"animethemes":[]}""", null)]                    // a null title
    [InlineData("""{"id":1,"title":{},"animethemes":[]}""", null)]                      // a title with no romaji
    [InlineData("""{"id":1,"title":{"romaji":null},"animethemes":[]}""", null)]         // a null romaji
    [InlineData("""{"id":1,"title":{"romaji":""},"animethemes":[]}""", "")]             // blank: passed on as sent
    [InlineData("""{"id":1,"title":{"romaji":"   "},"animethemes":[]}""", "   ")]       // the reader makes it null
    [InlineData("""{"id":1,"title":{"romaji":"Real","english":"Other","native":"X"},"animethemes":[]}""", "Real")]
    public async Task AnAnimeWithNoUsableNameStillParses_AndDoesNotFailThePage(string anime, string? expectedName)
    {
        var (client, handler) = Create();
        handler.Enqueue(() => FakeAnimeThemesHandler.Json(
            """{"data":{"animePagination":{"paginatorInfo":{"hasMorePages":false},"data":[""" + anime
            + """,{"id":2,"title":{"romaji":"Next"},"animethemes":[]}]}}}"""));

        var page = await client.GetPageAsync(1);

        Assert.Equal(2, page.Anime.Count); // the other anime on the page is read too
        Assert.Equal(expectedName, page.Anime[0].Name);
        Assert.Equal("Next", page.Anime[1].Name);
    }

    [Fact]
    public async Task OnlyDistinctMalIdsAreKept_AndAMissingOneIsDropped()
    {
        var (client, handler) = Create();
        handler.Enqueue(() => FakeAnimeThemesHandler.Json("""
            {"data":{"animePagination":{"paginatorInfo":{"hasMorePages":false},"data":[
              {"id":1,"resources":{"nodes":[{"externalId":10},{"externalId":10},{"externalId":null},{"externalId":11}]},"animethemes":[]},
              {"id":2,"resources":{"nodes":[]},"animethemes":[]}
            ]}}}
            """));

        var page = await client.GetPageAsync(1);

        Assert.Equal([10, 11], page.Anime[0].MalIds);
        Assert.Empty(page.Anime[1].MalIds);
    }

    // --- The request ---

    [Fact]
    public async Task SendsOneNamedPostForTheRequestedPage()
    {
        var (client, handler) = Create();
        handler.Enqueue(() => FakeAnimeThemesHandler.Json(EmptyPage));

        await client.GetPageAsync(7);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(AnimeThemesClient.Endpoint, request.Uri.AbsoluteUri);

        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal(AnimeThemesClient.Query, body.RootElement.GetProperty("query").GetString());
        Assert.Equal(7, body.RootElement.GetProperty("variables").GetProperty("page").GetInt32());
        Assert.Equal(2, body.RootElement.EnumerateObject().Count());
    }

    [Fact]
    public void TheQueryReadsAHundredAnimeAtATimeInIdOrder()
    {
        Assert.Contains("first: 100", AnimeThemesClient.Query);
        Assert.Contains("sort: [ID]", AnimeThemesClient.Query);
        Assert.Contains("resources(site: MAL)", AnimeThemesClient.Query);
    }

    [Fact]
    public async Task EveryRequestNamesTheAppAndItsRepository()
    {
        var (client, handler) = Create();
        handler.Enqueue(() => FakeAnimeThemesHandler.Json(EmptyPage));

        await client.GetPageAsync(1);

        var userAgent = Assert.Single(handler.Requests).UserAgent;
        Assert.Contains("BetterMAL", userAgent);
        Assert.Contains("https://github.com/PatrickParve/BetterMAL", userAgent);
    }

    [Fact]
    public void TheClientIsConfiguredWithASixtySecondTimeout()
    {
        using var http = new HttpClient();

        AnimeThemesClient.Configure(http);

        Assert.Equal(TimeSpan.FromSeconds(60), http.Timeout);
    }

    [Fact]
    public void TheQueryAsksForTheAnimesRomajiTitle_AndNoFieldNamedNameOfItsOwn()
    {
        // `Anime` has no `name` in AnimeThemes' GraphQL schema, only `title { romaji }`
        // (improve-search-page task 2.1): asking for `name` there fails every page. The
        // other `name` in the query is the artist's, a different level.
        var query = Regex.Replace(AnimeThemesClient.Query, @"\s+", " ");

        Assert.Contains("data { id title { romaji } resources(site: MAL)", query);
        Assert.DoesNotContain("data { id name", query);
        Assert.Equal(2, Regex.Matches(query, @"\bromaji\b").Count); // the anime's, and the song's
    }

    [Fact]
    public void TheQueryAsksForNoOtherField()
    {
        // The fields the catalogue stores (design D1) and the structure that
        // reaches them: no synopsis, picture, studio or year, and no title but
        // the anime's own romaji one and the song's. The arguments and the
        // variable are not fields.
        string[] allowed =
        [
            "animePagination", "paginatorInfo", "hasMorePages", "data", "id", "resources", "nodes", "externalId",
            "animethemes", "type", "sequence", "slug", "song", "title", "romaji", "performances", "alias", "artist",
            "name", "main", "animethemeentries", "version", "episodes", "nsfw", "spoiler", "videos", "nc",
            "priority", "link", "audio",
        ];
        string[] notFields = ["query", "Int", "page", "first", "sort", "site", "ID", "RELEVANCE", "MAL"];

        var asked = Regex.Matches(AnimeThemesClient.Query, "[A-Za-z_][A-Za-z0-9_]*")
            .Select(m => m.Value)
            .Where(word => !notFields.Contains(word))
            .Distinct()
            .Order()
            .ToList();

        Assert.Equal(allowed.Order(), asked);
    }

    // --- Rate limiting ---

    [Fact]
    public async Task ARateLimitIsWaitedOutAndThePageRequestedOnceMore()
    {
        var (client, handler) = Create();
        handler.Enqueue(() => FakeAnimeThemesHandler.RateLimited(TimeSpan.FromSeconds(30)));
        handler.Enqueue(() => FakeAnimeThemesHandler.Json(EmptyPage));

        var page = await client.GetPageAsync(3);

        Assert.NotNull(page);
        Assert.Equal([TimeSpan.FromSeconds(30)], client.Delays);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(handler.Requests[0].Body, handler.Requests[1].Body);
    }

    [Fact]
    public async Task AWaitOfExactlyTwoMinutesIsHonoured()
    {
        var (client, handler) = Create();
        handler.Enqueue(() => FakeAnimeThemesHandler.RateLimited(TimeSpan.FromMinutes(2)));
        handler.Enqueue(() => FakeAnimeThemesHandler.Json(EmptyPage));

        await client.GetPageAsync(1);

        Assert.Equal([TimeSpan.FromMinutes(2)], client.Delays);
    }

    [Fact]
    public async Task AskedToWaitTenMinutesItGivesUpWithoutWaiting()
    {
        var (client, handler) = Create();
        handler.Enqueue(() => FakeAnimeThemesHandler.RateLimited(TimeSpan.FromMinutes(10)));

        await Assert.ThrowsAsync<AnimeThemesException>(() => client.GetPageAsync(1));

        Assert.Empty(client.Delays);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task RefusedAgainAfterTheWaitItGivesUp()
    {
        var (client, handler) = Create();
        handler.Enqueue(() => FakeAnimeThemesHandler.RateLimited(TimeSpan.FromSeconds(5)));
        handler.Enqueue(() => FakeAnimeThemesHandler.RateLimited(TimeSpan.FromSeconds(5)));
        handler.Enqueue(() => FakeAnimeThemesHandler.Json(EmptyPage));

        await Assert.ThrowsAsync<AnimeThemesException>(() => client.GetPageAsync(1));

        Assert.Equal(2, handler.Requests.Count);
        Assert.Single(client.Delays);
    }

    [Fact]
    public async Task ARateLimitThatNamesNoWaitWaitsAMinute()
    {
        var (client, handler) = Create();
        handler.Enqueue(() => FakeAnimeThemesHandler.RateLimited(null));
        handler.Enqueue(() => FakeAnimeThemesHandler.Json(EmptyPage));

        await client.GetPageAsync(1);

        Assert.Equal([TimeSpan.FromMinutes(1)], client.Delays);
    }

    // --- Answers it can't use ---

    [Fact]
    public async Task ABodyWithErrorsIsAFailure_AndSaysWhy()
    {
        var (client, handler) = Create();
        handler.Enqueue(() => FakeAnimeThemesHandler.Json("""{"errors":[{"message":"Cannot query field \"synopsis\" on type \"Anime\"."}],"data":null}"""));

        var failure = await Assert.ThrowsAsync<AnimeThemesException>(() => client.GetPageAsync(1));

        Assert.Contains("Cannot query field", failure.Message);
    }

    [Fact]
    public async Task ErrorsBesideDataAreStillAFailure()
    {
        var (client, handler) = Create();
        handler.Enqueue(() => FakeAnimeThemesHandler.Json(
            """{"errors":[{"message":"one field failed"}],"data":{"animePagination":{"paginatorInfo":{"hasMorePages":false},"data":[]}}}"""));

        await Assert.ThrowsAsync<AnimeThemesException>(() => client.GetPageAsync(1));
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Created)]
    public async Task AnyStatusButOkIsAFailure(HttpStatusCode status)
    {
        var (client, handler) = Create();
        handler.Enqueue(() => FakeAnimeThemesHandler.Json(EmptyPage, status));

        var failure = await Assert.ThrowsAsync<AnimeThemesException>(() => client.GetPageAsync(1));

        Assert.Contains(((int)status).ToString(), failure.Message);
    }

    [Theory]
    [InlineData("[ this is not json")]
    [InlineData("")]
    [InlineData("null")]
    public async Task AnUnparsableBodyIsAFailure(string body)
    {
        var (client, handler) = Create();
        handler.Enqueue(() => FakeAnimeThemesHandler.Json(body));

        await Assert.ThrowsAsync<AnimeThemesException>(() => client.GetPageAsync(1));
    }

    [Theory]
    [InlineData("""{"data":null}""")]
    [InlineData("""{"data":{}}""")]
    [InlineData("""{"data":{"animePagination":{"data":[]}}}""")]
    [InlineData("""{"data":{"animePagination":{"paginatorInfo":{"hasMorePages":true}}}}""")]
    public async Task AnAnswerWithoutTheListAskedForIsAFailure(string body)
    {
        var (client, handler) = Create();
        handler.Enqueue(() => FakeAnimeThemesHandler.Json(body));

        await Assert.ThrowsAsync<AnimeThemesException>(() => client.GetPageAsync(1));
    }

    [Theory]
    [InlineData("""{"resources":{"nodes":[]},"animethemes":[]}""")]
    [InlineData("""{"id":1,"animethemes":[{"type":"OP","slug":"OP1"}]}""")]
    [InlineData("""{"id":1,"animethemes":[{"id":5,"type":"OP","slug":"OP1","song":{"title":{"romaji":"x"}}}]}""")]
    public async Task AKeyThatIsMissingIsAFailure(string anime)
    {
        var (client, handler) = Create();
        handler.Enqueue(() => FakeAnimeThemesHandler.Json(
            """{"data":{"animePagination":{"paginatorInfo":{"hasMorePages":false},"data":[""" + anime + "]}}}"));

        await Assert.ThrowsAsync<AnimeThemesException>(() => client.GetPageAsync(1));
    }

    [Fact]
    public async Task ATransportFailureIsNotHiddenAsAnotherType()
    {
        var (client, handler) = Create();
        handler.Fallback = () => throw new HttpRequestException("connection refused");

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetPageAsync(1));
    }

    [Fact]
    public async Task CancellationPassesThrough()
    {
        var (client, _) = Create();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetPageAsync(1, cts.Token));
    }
}
