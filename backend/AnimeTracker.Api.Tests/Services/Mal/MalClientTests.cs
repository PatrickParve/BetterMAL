using System.Globalization;
using System.Net;
using System.Text;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Mal.Dto;
using Microsoft.AspNetCore.WebUtilities;

namespace AnimeTracker.Api.Tests.Services.Mal;

// MalClient's paging loops (GetFullUserAnimeListAsync, GetFullSeasonAsync) take
// the next page's offset from MAL's own `paging.next` link, rather than
// assuming every page is full (design.md D1). Exercised over a real HttpClient
// and a stub handler scripted by offset, so a request for the wrong page fails
// loudly instead of quietly returning the wrong data. Also covers the
// `my_list_status` PATCH, including that `is_rewatching` is no longer sent or
// modelled (design.md D3).
public class MalClientTests
{
    private static MalClient CreateClient(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://api.myanimelist.net/v2/") });

    private static string? Query(Uri uri, string key) =>
        QueryHelpers.ParseQuery(uri.Query).TryGetValue(key, out var values) ? values.ToString() : null;

    private static List<int> RequestedOffsets(OffsetScriptedHandler stub) =>
        stub.Requests.Select(uri => int.Parse(Query(uri, "offset")!, CultureInfo.InvariantCulture)).ToList();

    private static string AnimeListPage(int startId, int count, string? next = null) =>
        BuildPage(Enumerable.Range(startId, count).Select(id => $$$"""{"node":{"id":{{{id}}},"title":"A{{{id}}}"}}"""), next);

    private static string UserListPage(int startId, int count, string? next = null) =>
        BuildPage(Enumerable.Range(startId, count).Select(id =>
            $$$"""{"node":{"id":{{{id}}},"title":"A{{{id}}}"},"list_status":{"status":"watching"}}"""), next);

    private static string BuildPage(IEnumerable<string> items, string? next)
    {
        var paging = next is null ? "{}" : $$"""{"next":"{{next}}"}""";
        return $$"""{"data":[{{string.Join(",", items)}}],"paging":{{paging}}}""";
    }

    private static async Task<(List<int> Ids, List<int> Offsets, List<int> PageCounts)> RunUserListAsync(
        Dictionary<int, (HttpStatusCode Status, string Json)> script)
    {
        var stub = new OffsetScriptedHandler(script);
        var client = CreateClient(stub);
        var pageCounts = new List<int>();

        var result = await client.GetFullUserAnimeListAsync(onPageRead: pageCounts.Add);

        return (result.Select(e => e.Node.Id).ToList(), RequestedOffsets(stub), pageCounts);
    }

    private static async Task<(List<int>? Ids, List<int> Offsets)> RunSeasonAsync(
        Dictionary<int, (HttpStatusCode Status, string Json)> script)
    {
        var stub = new OffsetScriptedHandler(script);
        var client = CreateClient(stub);

        var result = await client.GetFullSeasonAsync(2024, "spring");

        Assert.All(stub.Requests, uri => Assert.Equal("/v2/anime/season/2024/spring", uri.AbsolutePath));
        return (result?.Select(e => e.Node.Id).ToList(), RequestedOffsets(stub));
    }

    // --- GetFullUserAnimeListAsync ---

    [Fact]
    public async Task UserListAShortPageStartsTheNextRequestAtMalsOffset()
    {
        var script = new Dictionary<int, (HttpStatusCode, string)>
        {
            [0] = (HttpStatusCode.OK, UserListPage(1, 98, next: "https://api.myanimelist.net/v2/users/@me/animelist?offset=100&limit=100")),
            [100] = (HttpStatusCode.OK, UserListPage(101, 50)),
        };

        var (ids, offsets, pageCounts) = await RunUserListAsync(script);

        Assert.Equal(new[] { 0, 100 }, offsets);
        Assert.Equal(Enumerable.Range(1, 98).Concat(Enumerable.Range(101, 50)), ids);
        Assert.Equal(new[] { 98, 148 }, pageCounts);
    }

    [Fact]
    public async Task UserListANextLinkWithoutAnOffsetFallsBackOnePageSize()
    {
        var script = new Dictionary<int, (HttpStatusCode, string)>
        {
            [0] = (HttpStatusCode.OK, UserListPage(1, 100, next: "https://api.myanimelist.net/v2/users/@me/animelist?limit=100")),
            [100] = (HttpStatusCode.OK, UserListPage(101, 10)),
        };

        var (_, offsets, _) = await RunUserListAsync(script);

        Assert.Equal(new[] { 0, 100 }, offsets);
    }

    [Theory]
    [InlineData("offset=0")]
    [InlineData("offset=abc")]
    public async Task UserListANextLinkOffsetThatDoesNotAdvanceFallsBackAndKeepsGoing(string offsetParam)
    {
        var next = $"https://api.myanimelist.net/v2/users/@me/animelist?{offsetParam}&limit=100";
        var script = new Dictionary<int, (HttpStatusCode, string)>
        {
            [0] = (HttpStatusCode.OK, UserListPage(1, 100, next)),
            [100] = (HttpStatusCode.OK, UserListPage(101, 100, next)),
            [200] = (HttpStatusCode.OK, UserListPage(201, 5)),
        };

        var (_, offsets, _) = await RunUserListAsync(script);

        Assert.Equal(new[] { 0, 100, 200 }, offsets);
    }

    [Fact]
    public async Task UserListNoNextLinkMakesExactlyOneRequest()
    {
        var script = new Dictionary<int, (HttpStatusCode, string)>
        {
            [0] = (HttpStatusCode.OK, UserListPage(1, 30)),
        };

        var (ids, offsets, _) = await RunUserListAsync(script);

        Assert.Equal(new[] { 0 }, offsets);
        Assert.Equal(30, ids.Count);
    }

    [Fact]
    public async Task UserListAnEmptyPageEndsTheReadEvenWithANextLink()
    {
        var script = new Dictionary<int, (HttpStatusCode, string)>
        {
            [0] = (HttpStatusCode.OK, UserListPage(1, 100, next: "https://api.myanimelist.net/v2/users/@me/animelist?offset=100&limit=100")),
            [100] = (HttpStatusCode.OK, UserListPage(101, 0, next: "https://api.myanimelist.net/v2/users/@me/animelist?offset=200&limit=100")),
        };

        var (ids, offsets, _) = await RunUserListAsync(script);

        Assert.Equal(new[] { 0, 100 }, offsets);
        Assert.Equal(100, ids.Count);
    }

    [Fact]
    public async Task UserListRequestsKeepTheAppsOwnFieldsAndExcludeIsRewatching()
    {
        var script = new Dictionary<int, (HttpStatusCode, string)>
        {
            [0] = (HttpStatusCode.OK, UserListPage(1, 100, next: "https://api.myanimelist.net/v2/users/@me/animelist?offset=100&fields=id&nsfw=1")),
            [100] = (HttpStatusCode.OK, UserListPage(101, 5)),
        };
        var stub = new OffsetScriptedHandler(script);
        var client = CreateClient(stub);

        await client.GetFullUserAnimeListAsync();

        var firstFields = Query(stub.Requests[0], "fields");
        Assert.Equal(firstFields, Query(stub.Requests[1], "fields"));
        Assert.Equal("true", Query(stub.Requests[1], "nsfw"));
        Assert.Contains("list_status{status,score,num_episodes_watched,start_date,finish_date,num_times_rewatched}", firstFields);
        Assert.DoesNotContain("is_rewatching", firstFields);
    }

    // --- GetFullSeasonAsync ---

    [Fact]
    public async Task SeasonAShortPageStartsTheNextRequestAtMalsOffset()
    {
        var script = new Dictionary<int, (HttpStatusCode, string)>
        {
            [0] = (HttpStatusCode.OK, AnimeListPage(1, 98, next: "https://api.myanimelist.net/v2/anime/season/2024/spring?offset=100&limit=100")),
            [100] = (HttpStatusCode.OK, AnimeListPage(101, 50)),
        };

        var (ids, offsets) = await RunSeasonAsync(script);

        Assert.Equal(new[] { 0, 100 }, offsets);
        Assert.Equal(Enumerable.Range(1, 98).Concat(Enumerable.Range(101, 50)), ids);
    }

    [Fact]
    public async Task SeasonANextLinkWithoutAnOffsetFallsBackOnePageSize()
    {
        var script = new Dictionary<int, (HttpStatusCode, string)>
        {
            [0] = (HttpStatusCode.OK, AnimeListPage(1, 100, next: "https://api.myanimelist.net/v2/anime/season/2024/spring?limit=100")),
            [100] = (HttpStatusCode.OK, AnimeListPage(101, 10)),
        };

        var (_, offsets) = await RunSeasonAsync(script);

        Assert.Equal(new[] { 0, 100 }, offsets);
    }

    [Theory]
    [InlineData("offset=0")]
    [InlineData("offset=abc")]
    public async Task SeasonANextLinkOffsetThatDoesNotAdvanceFallsBackAndKeepsGoing(string offsetParam)
    {
        var next = $"https://api.myanimelist.net/v2/anime/season/2024/spring?{offsetParam}&limit=100";
        var script = new Dictionary<int, (HttpStatusCode, string)>
        {
            [0] = (HttpStatusCode.OK, AnimeListPage(1, 100, next)),
            [100] = (HttpStatusCode.OK, AnimeListPage(101, 100, next)),
            [200] = (HttpStatusCode.OK, AnimeListPage(201, 5)),
        };

        var (_, offsets) = await RunSeasonAsync(script);

        Assert.Equal(new[] { 0, 100, 200 }, offsets);
    }

    [Fact]
    public async Task SeasonNoNextLinkMakesExactlyOneRequest()
    {
        var script = new Dictionary<int, (HttpStatusCode, string)>
        {
            [0] = (HttpStatusCode.OK, AnimeListPage(1, 30)),
        };

        var (ids, offsets) = await RunSeasonAsync(script);

        Assert.Equal(new[] { 0 }, offsets);
        Assert.Equal(30, ids!.Count);
    }

    [Fact]
    public async Task SeasonAnEmptyPageEndsTheReadEvenWithANextLink()
    {
        var script = new Dictionary<int, (HttpStatusCode, string)>
        {
            [0] = (HttpStatusCode.OK, AnimeListPage(1, 100, next: "https://api.myanimelist.net/v2/anime/season/2024/spring?offset=100&limit=100")),
            [100] = (HttpStatusCode.OK, AnimeListPage(101, 0, next: "https://api.myanimelist.net/v2/anime/season/2024/spring?offset=200&limit=100")),
        };

        var (ids, offsets) = await RunSeasonAsync(script);

        Assert.Equal(new[] { 0, 100 }, offsets);
        Assert.Equal(100, ids!.Count);
    }

    [Fact]
    public async Task SeasonRequestsKeepTheAppsOwnFieldsAndNsfw()
    {
        var script = new Dictionary<int, (HttpStatusCode, string)>
        {
            [0] = (HttpStatusCode.OK, AnimeListPage(1, 100, next: "https://api.myanimelist.net/v2/anime/season/2024/spring?offset=100&fields=id&nsfw=1")),
            [100] = (HttpStatusCode.OK, AnimeListPage(101, 5)),
        };
        var stub = new OffsetScriptedHandler(script);
        var client = CreateClient(stub);

        await client.GetFullSeasonAsync(2024, "spring");

        Assert.Equal(Query(stub.Requests[0], "fields"), Query(stub.Requests[1], "fields"));
        Assert.Equal("true", Query(stub.Requests[1], "nsfw"));
    }

    [Fact]
    public async Task SeasonFirstPage404ReturnsNullAfterOneRequest()
    {
        var script = new Dictionary<int, (HttpStatusCode, string)>
        {
            [0] = (HttpStatusCode.NotFound, ""),
        };

        var (ids, offsets) = await RunSeasonAsync(script);

        Assert.Null(ids);
        Assert.Equal(new[] { 0 }, offsets);
    }

    // --- UpdateMyListStatusAsync ---

    [Fact]
    public async Task UpdateMyListStatusSendsExactFormFieldsAndDeserializesTheResponse()
    {
        var stub = new RecordingHandler(HttpStatusCode.OK,
            """{"status":"watching","score":8,"num_episodes_watched":3,"is_rewatching":false,"num_times_rewatched":1,"updated_at":"2026-09-14T00:00:00+00:00"}""");
        var client = CreateClient(stub);
        var update = new MalListStatusUpdate
        {
            Status = "watching",
            NumWatchedEpisodes = 3,
            Score = 8,
            NumTimesRewatched = 1,
            StartDate = new DateOnly(2026, 1, 1),
            FinishDate = new DateOnly(2026, 2, 1),
        };

        var result = await client.UpdateMyListStatusAsync(42, update);

        Assert.Equal(HttpMethod.Patch, stub.Request!.Method);
        Assert.Equal("/v2/anime/42/my_list_status", stub.Request.RequestUri!.AbsolutePath);

        var form = QueryHelpers.ParseQuery(stub.RequestBody);
        var expectedKeys = new[] { "status", "num_watched_episodes", "score", "num_times_rewatched", "start_date", "finish_date" };
        Assert.Equal(expectedKeys.OrderBy(k => k), form.Keys.OrderBy(k => k));

        Assert.Equal("watching", result.Status);
        Assert.Equal(8, result.Score);
        Assert.Equal(3, result.NumEpisodesWatched);
    }

    private sealed class OffsetScriptedHandler(Dictionary<int, (HttpStatusCode Status, string Json)> responses) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;
            Requests.Add(uri);

            var offsetText = Query(uri, "offset") ?? "0";
            var offset = int.Parse(offsetText, CultureInfo.InvariantCulture);
            if (!responses.TryGetValue(offset, out var response))
                throw new InvalidOperationException($"No scripted response for offset {offset}.");

            return Task.FromResult(new HttpResponseMessage(response.Status)
            {
                Content = new StringContent(response.Json, Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class RecordingHandler(HttpStatusCode status, string json) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string RequestBody { get; private set; } = "";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            RequestBody = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
        }
    }
}
