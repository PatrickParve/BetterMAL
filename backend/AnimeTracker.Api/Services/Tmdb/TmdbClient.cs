using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace AnimeTracker.Api.Services.Tmdb;

/// <summary>Typed client for TMDB's v3 images endpoints (design.md D4). The v3
/// key has to travel in the query string, so key hygiene is a rule of this
/// class: nothing here logs a request URI, only the endpoint path and the
/// status. (<c>IHttpClientFactory</c>'s own request logging does print URIs;
/// since .NET 9 it redacts query strings, which the app inherits and must not
/// switch off — task 12.3 confirms it with a captured log.)</summary>
public class TmdbClient(HttpClient http, IOptions<TmdbOptions> options, ILogger<TmdbClient> logger) : ITmdbClient
{
    // Exactly these three values, "null" being TMDB's spelling of "no language".
    private const string LanguageQuery = "include_image_language=ja,en,null";

    private static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(10);

    public Task<TmdbImagesResult> GetTvImagesAsync(int tvId, CancellationToken ct = default) =>
        GetImagesAsync($"tv/{tvId}/images", keepBackdrops: true, ct);

    // The season endpoint returns posters only; dropping backdrops here keeps
    // "a season is posters only" true whatever the response carries.
    public Task<TmdbImagesResult> GetSeasonImagesAsync(int tvId, int seasonNumber, CancellationToken ct = default) =>
        GetImagesAsync($"tv/{tvId}/season/{seasonNumber}/images", keepBackdrops: false, ct);

    public Task<TmdbImagesResult> GetMovieImagesAsync(int movieId, CancellationToken ct = default) =>
        GetImagesAsync($"movie/{movieId}/images", keepBackdrops: true, ct);

    /// <summary>The wait before the one retry after a rate-limit response.
    /// Virtual so a test can observe the wait instead of sitting through
    /// it.</summary>
    protected virtual Task DelayAsync(TimeSpan wait, CancellationToken ct) => Task.Delay(wait, ct);

    private async Task<TmdbImagesResult> GetImagesAsync(string path, bool keepBackdrops, CancellationToken ct)
    {
        if (!options.Value.IsConfigured)
            return new TmdbImagesResult.Failed();

        var uri = $"{path}?{LanguageQuery}&api_key={Uri.EscapeDataString(options.Value.ApiKey)}";

        try
        {
            for (var attempt = 0; ; attempt++)
            {
                using var response = await http.GetAsync(uri, ct);

                // One retry, then a second refusal falls through to the
                // non-success branch below and counts as a failure.
                if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt == 0)
                {
                    var wait = RetryDelay(response);
                    logger.LogWarning("TMDB rate-limited {Path}; retrying once in {Seconds}s.", path, wait.TotalSeconds);
                    await DelayAsync(wait, ct);
                    continue;
                }

                if (response.StatusCode == HttpStatusCode.NotFound)
                    return new TmdbImagesResult.NotFound();

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    logger.LogError("TMDB rejected the API key (HTTP 401). No TMDB images can be fetched until the key is corrected.");
                    return new TmdbImagesResult.Failed();
                }

                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning("TMDB answered {Path} with HTTP {Status}.", path, (int)response.StatusCode);
                    return new TmdbImagesResult.Failed();
                }

                var body = await response.Content.ReadFromJsonAsync<ImagesResponse>(ct);
                if (body is null)
                {
                    logger.LogWarning("TMDB answered {Path} with an empty body.", path);
                    return new TmdbImagesResult.Failed();
                }

                return new TmdbImagesResult.Images(
                    ToImages(body.Posters),
                    keepBackdrops ? ToImages(body.Backdrops) : []);
            }
        }
        // An HttpClient timeout also surfaces as an OperationCanceledException,
        // but with this token still live, so only a caller's own cancellation
        // is let through; a timeout is a failed request like any other.
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "TMDB request for {Path} failed.", path);
            return new TmdbImagesResult.Failed();
        }
    }

    /// <summary>How long TMDB asked to wait, clamped to at most 10 seconds
    /// and defaulting to 2 when the response says nothing usable.</summary>
    private static TimeSpan RetryDelay(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        var asked = retryAfter?.Delta ?? (retryAfter?.Date - DateTimeOffset.UtcNow) ?? DefaultRetryDelay;
        return asked < TimeSpan.Zero ? TimeSpan.Zero : asked > MaxRetryDelay ? MaxRetryDelay : asked;
    }

    // The request's filter should already guarantee ja/en/none; anything else
    // is dropped as a guard, and an entry with no path is unusable.
    private static List<TmdbImage> ToImages(List<ImageDto>? images) =>
        (images ?? [])
            .Where(i => !string.IsNullOrEmpty(i.FilePath) && i.Iso6391 is null or "ja" or "en")
            .Select(i => new TmdbImage(i.FilePath!, i.Iso6391, i.Width, i.Height))
            .ToList();

    // Only these two arrays, and per image only these four fields, are
    // declared. `logos` and the vote fields are skipped by System.Text.Json
    // and never deserialised. A season response has no `backdrops` key, which
    // is simply a null list here.
    private sealed record ImagesResponse(
        [property: JsonPropertyName("posters")] List<ImageDto>? Posters,
        [property: JsonPropertyName("backdrops")] List<ImageDto>? Backdrops);

    private sealed record ImageDto(
        [property: JsonPropertyName("file_path")] string? FilePath,
        [property: JsonPropertyName("iso_639_1")] string? Iso6391,
        [property: JsonPropertyName("width")] int Width,
        [property: JsonPropertyName("height")] int Height);
}
