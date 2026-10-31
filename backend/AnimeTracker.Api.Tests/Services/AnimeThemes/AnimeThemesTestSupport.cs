using System.Net;
using System.Net.Http.Headers;
using System.Text;
using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.AnimeThemes;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimeTracker.Api.Tests.Services.AnimeThemes;

// Shared by the AnimeThemes tests: the recorded answer, a fake HTTP handler and
// small builders for the parsed models.

/// <summary>The recorded answer. <c>Fixtures/anime-pagination.json</c> is a trimmed copy of
/// a real <c>animePagination</c> answer from graphql.animethemes.moe, recorded on
/// 2026-10-31 with <see cref="AnimeThemesClient.Query"/>: page 1 of the whole list
/// (anime 2, 5, 6, 18, 20, 22, 25, 33 and 69) and the one anime with insert songs
/// (4514), left as AnimeThemes sent them. Between them they hold a clean and a credited
/// video, versions, a spoiler, NSFW entries, an insert song, no artist, no episodes, a
/// theme with no number, a theme with no song, and a group credited once per member.
/// <para>The recording has no alias, no shared song, no anime with several MAL ids and none
/// with no MAL id, because those are rare: the tests that need them build the parsed models
/// by hand instead.</para></summary>
internal static class AnimeThemesFixture
{
    public static string PageJson() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Services", "AnimeThemes", "Fixtures", "anime-pagination.json"));

    /// <summary>The recorded answer, parsed by the real client.</summary>
    public static async Task<AnimeThemesPage> ParsedPageAsync()
    {
        var handler = new FakeAnimeThemesHandler();
        handler.Enqueue(() => FakeAnimeThemesHandler.Json(PageJson()));
        var client = new InstantClient(handler);
        return await client.GetPageAsync(1);
    }
}

/// <summary>A request as the handler saw it.</summary>
internal sealed record RecordedRequest(HttpMethod Method, Uri Uri, string Body, string UserAgent);

/// <summary>Answers each request with the next queued response, or the fallback, and
/// remembers what was asked.</summary>
internal sealed class FakeAnimeThemesHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpResponseMessage>> _responses = new();

    public List<RecordedRequest> Requests { get; } = [];

    /// <summary>What answers once the queue is empty.</summary>
    public Func<HttpResponseMessage> Fallback { get; set; } = () => new HttpResponseMessage(HttpStatusCode.InternalServerError);

    // A delegate rather than a response: each request needs a fresh message,
    // since a response body can be read only once.
    public void Enqueue(Func<HttpResponseMessage> response) => _responses.Enqueue(response);

    public static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    public static HttpResponseMessage RateLimited(TimeSpan? retryAfter) =>
        new(HttpStatusCode.TooManyRequests)
        {
            Headers = { RetryAfter = retryAfter is { } wait ? new RetryConditionHeaderValue(wait) : null },
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add(new RecordedRequest(request.Method, request.RequestUri!, body, request.Headers.UserAgent.ToString()));
        return (_responses.Count > 0 ? _responses.Dequeue() : Fallback)();
    }
}

/// <summary>The real client with its waits recorded rather than sat through, over a
/// client configured as Program.cs configures it.</summary>
internal sealed class InstantClient : AnimeThemesClient
{
    public InstantClient(FakeAnimeThemesHandler handler)
        : base(Configured(handler), NullLogger<AnimeThemesClient>.Instance)
    {
    }

    public List<TimeSpan> Delays { get; } = [];

    protected override Task DelayAsync(TimeSpan wait, CancellationToken ct)
    {
        Delays.Add(wait);
        return Task.CompletedTask;
    }

    private static HttpClient Configured(HttpMessageHandler handler)
    {
        var http = new HttpClient(handler, disposeHandler: false);
        Configure(http);
        return http;
    }
}

/// <summary>Builders for parsed pages, for the tests that need a shape the recording
/// doesn't hold. Every link is on an allowed host unless a test says otherwise.</summary>
internal static class Parsed
{
    public static AnimeThemesVideo Video(
        string file = "Show-OP1", bool clean = false, int priority = 20, bool audio = true) =>
        new(clean, priority, $"https://v.animethemes.moe/{file}.webm", audio ? $"https://a.animethemes.moe/{file}.ogg" : null);

    public static AnimeThemesVersion Version(
        int number, AnimeThemesVideo[]? videos = null, string? episodes = null, bool nsfw = false, bool spoiler = false) =>
        new(number, episodes, nsfw, spoiler, videos ?? [Video($"Show-OP1v{number}")]);

    public static AnimeThemesSong Song(int id = 100, string? title = "A song", params AnimeThemesCredit[] credits) =>
        new(id, title, credits);

    public static AnimeThemesCredit Credit(string artist, string? alias = null) => new(alias, artist);

    public static AnimeThemesTheme Theme(
        int id,
        string type = "OP",
        string slug = "OP1",
        int? sequence = 1,
        AnimeThemesSong? song = null,
        AnimeThemesVersion[]? versions = null) =>
        new(id, type, sequence, slug, song, versions ?? [Version(1)]);

    public static AnimeThemesAnime Anime(int id, int[] malIds, params AnimeThemesTheme[] themes) =>
        new(id, malIds, themes);

    public static AnimeThemesPage Page(params AnimeThemesAnime[] anime) => new(false, anime);
}

/// <summary>The reader every fixture that constructs a page-read service needs: the real
/// one over the same context, so a fixture that stores no catalogue sees every page report
/// no themes.</summary>
internal static class AnimeThemesReadsTestKit
{
    public static AnimeThemesReads Create(AnimeTrackerDbContext db) => new(db);
}

/// <summary>Rows of the stored catalogue, for the tests of what reads it.</summary>
internal static class StoredThemes
{
    /// <summary>One stored opening or ending, labelled OP1 or ED2 from its kind and number unless
    /// told otherwise.</summary>
    public static AnimeTheme Row(
        int animeId,
        int themeId,
        AnimeThemeKind kind = AnimeThemeKind.Opening,
        int sequence = 1,
        string? label = null,
        string? songTitle = "A song",
        string[]? artists = null,
        string? episodes = null,
        bool nsfw = false,
        bool spoiler = false,
        bool clean = false) =>
        new()
        {
            AnimeId = animeId,
            ThemeId = themeId,
            Kind = kind,
            Sequence = sequence,
            Label = label ?? $"{(kind == AnimeThemeKind.Opening ? "OP" : "ED")}{sequence}",
            SongId = themeId * 10,
            SongTitle = songTitle,
            Artists = [.. artists ?? []],
            Episodes = episodes,
            Nsfw = nsfw,
            Spoiler = spoiler,
            Clean = clean,
            VideoUrl = $"https://v.animethemes.moe/T{themeId}.webm",
            AudioUrl = $"https://a.animethemes.moe/T{themeId}.ogg",
        };

    /// <summary>Records that a refresh has finished, which is what makes the catalogue ready.</summary>
    public static void MarkSynced(AnimeTrackerDbContext db) =>
        db.AnimeThemesSyncStates.Add(new AnimeThemesSyncState
        {
            LastSyncedAt = DateTimeOffset.UtcNow,
            LastAttemptAt = DateTimeOffset.UtcNow,
        });
}
