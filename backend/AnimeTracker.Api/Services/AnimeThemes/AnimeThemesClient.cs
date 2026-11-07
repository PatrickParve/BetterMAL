using System.Net;
using System.Text;
using System.Text.Json;

namespace AnimeTracker.Api.Services.AnimeThemes;

/// <summary>The AnimeThemes GraphQL client (add-anime-themes-and-guess-the-theme
/// design.md D2): one query, read a page at a time by the weekly sync, and the
/// only place that reads AnimeThemes' API. It never reads a video or an audio
/// file, only the links to them.
/// <para>The schema doesn't always match its own documentation, and may keep
/// moving, so anything the query didn't get is a failure rather than a guess: a
/// body with `errors`, a status other than 200, a body that doesn't parse, or one
/// without the list the query asked for. The sync keeps what it stored when this
/// throws.</para></summary>
public class AnimeThemesClient(HttpClient http, ILogger<AnimeThemesClient> logger) : IAnimeThemesClient
{
    public const string Endpoint = "https://graphql.animethemes.moe/";

    /// <summary>Names the app and where to find it, so AnimeThemes can see who is
    /// reading and reach whoever runs it.</summary>
    public const string UserAgent = "BetterMAL (https://github.com/PatrickParve/BetterMAL)";

    /// <summary>A page of 100 anime with everything below took 5 to 20 seconds to
    /// answer, and 14.7 seconds at the slowest of a whole read.</summary>
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(60);

    /// <summary>A 429's <c>Retry-After</c> is waited out up to this long; asked to
    /// wait longer, the read fails.</summary>
    public static readonly TimeSpan MaxRetryAfter = TimeSpan.FromMinutes(2);

    /// <summary>When a 429 names no wait: AnimeThemes' limit is per minute.</summary>
    private static readonly TimeSpan DefaultRetryAfter = TimeSpan.FromMinutes(1);

    /// <summary>The query asks for the fields the catalogue stores (design.md D1)
    /// and nothing else: no synopsis, picture, studio or year. The MAL ids come
    /// from `resources(site: MAL)`, a song's artists in AnimeThemes' order of
    /// relevance, and `first` is the most a page may hold. The anime's own name is
    /// its romaji title, `title { romaji }`: GraphQL's `Anime` has no `name` field,
    /// and asking for one fails every page (improve-search-page design.md D8, checked
    /// against the schema in the server's `graphql/Models/Wiki/anime.graphql`).</summary>
    public const string Query = """
        query ($page: Int!) {
          animePagination(first: 100, page: $page, sort: [ID]) {
            paginatorInfo { hasMorePages }
            data {
              id
              title { romaji }
              resources(site: MAL) { nodes { externalId } }
              animethemes {
                id
                type
                sequence
                slug
                song {
                  id
                  title { romaji }
                  performances(sort: [RELEVANCE]) { alias artist { name { main } } }
                }
                animethemeentries {
                  version
                  episodes
                  nsfw
                  spoiler
                  videos { nodes { nc priority link audio { link } } }
                }
              }
            }
          }
        }
        """;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>What Program.cs's registration applies to the client: the timeout
    /// and the name it reads under. The address is the request's own.</summary>
    public static void Configure(HttpClient client)
    {
        client.Timeout = RequestTimeout;
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
    }

    /// <summary>The wait before the one retry after a rate-limit response.
    /// Virtual so a test can observe the wait instead of sitting through
    /// it.</summary>
    protected virtual Task DelayAsync(TimeSpan wait, CancellationToken ct) => Task.Delay(wait, ct);

    public async Task<AnimeThemesPage> GetPageAsync(int page, CancellationToken ct = default)
    {
        var payload = JsonSerializer.Serialize(new { query = Query, variables = new { page } });

        for (var attempt = 0; ; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
            };
            using var response = await http.SendAsync(request, ct);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var wait = RetryAfterOf(response);
                if (attempt > 0)
                    throw new AnimeThemesException($"AnimeThemes was still rate limiting page {page} after waiting {wait.TotalSeconds:0} seconds.");
                if (wait > MaxRetryAfter)
                    throw new AnimeThemesException($"AnimeThemes asked for a {wait.TotalSeconds:0}-second wait on page {page}, longer than the {MaxRetryAfter.TotalSeconds:0} allowed.");

                logger.LogWarning("AnimeThemes rate-limited page {Page}; retrying once in {Seconds}s.", page, wait.TotalSeconds);
                await DelayAsync(wait, ct);
                continue;
            }

            if (response.StatusCode != HttpStatusCode.OK)
                throw new AnimeThemesException($"AnimeThemes answered page {page} with HTTP {(int)response.StatusCode}.");

            var body = await response.Content.ReadAsStringAsync(ct);
            return Parse(body, page);
        }
    }

    /// <summary>How long AnimeThemes asked to wait, or a minute when it said
    /// nothing usable. Not clamped here: a wait over the cap fails the read.</summary>
    private static TimeSpan RetryAfterOf(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        var asked = retryAfter?.Delta ?? (retryAfter?.Date - DateTimeOffset.UtcNow) ?? DefaultRetryAfter;
        return asked < TimeSpan.Zero ? TimeSpan.Zero : asked;
    }

    private static AnimeThemesPage Parse(string body, int page)
    {
        GraphQlResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<GraphQlResponse>(body, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new AnimeThemesException($"AnimeThemes' answer for page {page} could not be read.", ex);
        }

        if (parsed is null)
            throw new AnimeThemesException($"AnimeThemes' answer for page {page} was empty.");

        // Any error fails the page, even beside data: a field the query asks for
        // may have gone, and the rest of the answer can't be trusted to be whole.
        if (parsed.Errors.ValueKind == JsonValueKind.Array && parsed.Errors.GetArrayLength() > 0)
            throw new AnimeThemesException($"AnimeThemes reported an error on page {page}: {FirstErrorMessage(parsed.Errors)}");

        if (parsed.Data?.AnimePagination is not { PaginatorInfo: not null, Data: not null } list)
            throw new AnimeThemesException($"AnimeThemes' answer for page {page} had no list of anime.");

        return new AnimeThemesPage(list.PaginatorInfo.HasMorePages, list.Data.Where(a => a is not null).Select(ToAnime).ToList());
    }

    private static string FirstErrorMessage(JsonElement errors)
    {
        var first = errors[0];
        var message = first.ValueKind == JsonValueKind.Object
            && first.TryGetProperty("message", out var m)
            && m.ValueKind == JsonValueKind.String
                ? m.GetString()
                : null;
        return message is { Length: > 200 } ? message[..200] : message ?? "no message";
    }

    private static AnimeThemesAnime ToAnime(AnimeDto anime) =>
        new(
            anime.Id ?? throw MissingId("an anime"),
            anime.Title?.Romaji,
            (anime.Resources?.Nodes ?? [])
                .Select(r => r?.ExternalId)
                .OfType<int>()
                .Distinct()
                .ToList(),
            (anime.AnimeThemes ?? []).Where(t => t is not null).Select(ToTheme).ToList());

    private static AnimeThemesTheme ToTheme(ThemeDto theme) =>
        new(
            theme.Id ?? throw MissingId("a theme"),
            theme.Type ?? "",
            theme.Sequence,
            theme.Slug ?? "",
            theme.Song is null ? null : ToSong(theme.Song),
            (theme.AnimeThemeEntries ?? []).Where(e => e is not null).Select(ToVersion).ToList());

    private static AnimeThemesSong ToSong(SongDto song) =>
        new(
            song.Id ?? throw MissingId("a song"),
            song.Title?.Romaji,
            (song.Performances ?? [])
                .Where(p => p is not null)
                .Select(p => new AnimeThemesCredit(p.Alias, p.Artist?.Name?.Main))
                .ToList());

    private static AnimeThemesVersion ToVersion(EntryDto entry) =>
        new(
            entry.Version ?? 1,
            entry.Episodes,
            entry.Nsfw,
            entry.Spoiler,
            (entry.Videos?.Nodes ?? [])
                .Where(v => v is not null)
                .Select(v => new AnimeThemesVideo(v.Nc, v.Priority, v.Link, v.Audio?.Link))
                .ToList());

    private static AnimeThemesException MissingId(string what) =>
        new($"AnimeThemes sent {what} with no id.");

    // Only the fields the query asks for are declared; anything else AnimeThemes
    // adds is skipped by System.Text.Json and never read.
    private sealed class GraphQlResponse
    {
        public JsonElement Errors { get; set; }
        public DataDto? Data { get; set; }
    }

    private sealed class DataDto
    {
        public PaginationDto? AnimePagination { get; set; }
    }

    private sealed class PaginationDto
    {
        public PaginatorInfoDto? PaginatorInfo { get; set; }
        public List<AnimeDto>? Data { get; set; }
    }

    private sealed class PaginatorInfoDto
    {
        public bool HasMorePages { get; set; }
    }

    private sealed class NodesDto<T>
    {
        public List<T>? Nodes { get; set; }
    }

    private sealed class AnimeDto
    {
        public int? Id { get; set; }
        public AnimeTitleDto? Title { get; set; }
        public NodesDto<ResourceDto>? Resources { get; set; }
        public List<ThemeDto>? AnimeThemes { get; set; }
    }

    private sealed class AnimeTitleDto
    {
        public string? Romaji { get; set; }
    }

    private sealed class ResourceDto
    {
        public int? ExternalId { get; set; }
    }

    private sealed class ThemeDto
    {
        public int? Id { get; set; }
        public string? Type { get; set; }
        public int? Sequence { get; set; }
        public string? Slug { get; set; }
        public SongDto? Song { get; set; }
        public List<EntryDto>? AnimeThemeEntries { get; set; }
    }

    private sealed class SongDto
    {
        public int? Id { get; set; }
        public SongTitleDto? Title { get; set; }
        public List<PerformanceDto>? Performances { get; set; }
    }

    private sealed class SongTitleDto
    {
        public string? Romaji { get; set; }
    }

    private sealed class PerformanceDto
    {
        public string? Alias { get; set; }
        public ArtistDto? Artist { get; set; }
    }

    private sealed class ArtistDto
    {
        public ArtistNameDto? Name { get; set; }
    }

    private sealed class ArtistNameDto
    {
        public string? Main { get; set; }
    }

    private sealed class EntryDto
    {
        public int? Version { get; set; }
        public string? Episodes { get; set; }
        public bool Nsfw { get; set; }
        public bool Spoiler { get; set; }
        public NodesDto<VideoDto>? Videos { get; set; }
    }

    private sealed class VideoDto
    {
        public bool Nc { get; set; }
        public int Priority { get; set; }
        public string? Link { get; set; }
        public AudioDto? Audio { get; set; }
    }

    private sealed class AudioDto
    {
        public string? Link { get; set; }
    }
}
