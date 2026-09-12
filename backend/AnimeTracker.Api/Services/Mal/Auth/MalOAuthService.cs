using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AnimeTracker.Api.Models;
using Microsoft.Extensions.Options;

namespace AnimeTracker.Api.Services.Mal.Auth;

public class MalOAuthService(
    IHttpClientFactory httpClientFactory,
    IMalTokenStore tokenStore,
    MalOAuthStateStore stateStore,
    IOptions<MalOptions> options,
    ILogger<MalOAuthService> logger) : IMalOAuthService
{
    private const string AuthorizeEndpoint = "https://myanimelist.net/v1/oauth2/authorize";
    private const string TokenEndpoint = "https://myanimelist.net/v1/oauth2/token";

    public string BuildAuthorizeUrl()
    {
        var (state, verifier) = stateStore.CreatePending();
        var opts = options.Value;

        var query = new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = opts.ClientId,
            // MAL requires the literal "plain" challenge method — most OAuth
            // libraries default to S256, which fails against MAL silently.
            ["code_challenge"] = verifier,
            ["code_challenge_method"] = "plain",
            ["state"] = state,
            ["redirect_uri"] = RedirectUri(opts),
        };

        var queryString = string.Join("&", query.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
        return $"{AuthorizeEndpoint}?{queryString}";
    }

    public async Task<OAuthToken> HandleCallbackAsync(string code, string state, CancellationToken ct = default)
    {
        var verifier = stateStore.ConsumeVerifier(state)
            ?? throw new InvalidOperationException(
                "MAL OAuth callback state is missing, expired, or does not match the pending authorization request.");

        var opts = options.Value;
        var form = new Dictionary<string, string>
        {
            ["client_id"] = opts.ClientId,
            ["client_secret"] = opts.ClientSecret ?? "",
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["code_verifier"] = verifier,
            ["redirect_uri"] = RedirectUri(opts),
        };

        return await ExchangeAsync(form, ct);
    }

    public async Task<MalRefreshResult> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var opts = options.Value;
        var form = new Dictionary<string, string>
        {
            ["client_id"] = opts.ClientId,
            ["client_secret"] = opts.ClientSecret ?? "",
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
        };

        HttpResponseMessage response;
        try
        {
            response = await SendTokenRequestAsync(form, ct);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "MAL token refresh couldn't be reached; treating this as an outage.");
            return new MalRefreshResult.Unavailable("MyAnimeList couldn't be reached.");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("MAL token refresh timed out; treating this as an outage.");
            return new MalRefreshResult.Unavailable("MyAnimeList couldn't be reached.");
        }

        using (response)
        {
            // 400/401 is the token endpoint refusing this login outright
            // (RFC 6749 §5.2) — everything else is an outage (design.md D11).
            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
            {
                var error = await TryReadErrorAsync(response, ct);
                logger.LogWarning(
                    "MyAnimeList refused to refresh the login ({StatusCode}): {Error}",
                    (int)response.StatusCode, error ?? "(no error field)");
                await tokenStore.MarkConnectionLostAsync(DateTimeOffset.UtcNow, ct);
                return new MalRefreshResult.Refused((int)response.StatusCode, error);
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("MAL token refresh answered {StatusCode}; treating this as an outage.", (int)response.StatusCode);
                return new MalRefreshResult.Unavailable($"MyAnimeList answered {(int)response.StatusCode}.");
            }

            var payload = await response.Content.ReadFromJsonAsync<MalTokenResponse>(cancellationToken: ct)
                ?? throw new InvalidOperationException("MAL token endpoint returned an empty response.");

            var expiresAt = DateTimeOffset.UtcNow.AddSeconds(payload.ExpiresIn);
            await tokenStore.SaveAsync(payload.AccessToken, payload.RefreshToken, expiresAt, ct);

            return new MalRefreshResult.Refreshed(new OAuthToken
            {
                AccessToken = payload.AccessToken,
                RefreshToken = payload.RefreshToken,
                ExpiresAt = expiresAt,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
        }
    }

    private async Task<OAuthToken> ExchangeAsync(Dictionary<string, string> form, CancellationToken ct)
    {
        using var response = await SendTokenRequestAsync(form, ct);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<MalTokenResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("MAL token endpoint returned an empty response.");

        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(payload.ExpiresIn);
        await tokenStore.SaveAsync(payload.AccessToken, payload.RefreshToken, expiresAt, ct);

        return new OAuthToken
        {
            AccessToken = payload.AccessToken,
            RefreshToken = payload.RefreshToken,
            ExpiresAt = expiresAt,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
    }

    private async Task<HttpResponseMessage> SendTokenRequestAsync(Dictionary<string, string> form, CancellationToken ct)
    {
        using var client = httpClientFactory.CreateClient();
        return await client.PostAsync(TokenEndpoint, new FormUrlEncodedContent(form), ct);
    }

    // Best-effort: MAL doesn't document its refusal error bodies, and an
    // unparsable one still means Refused — it just logs without the detail.
    private static async Task<string?> TryReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync<MalTokenErrorResponse>(cancellationToken: ct);
            return body?.Error;
        }
        catch
        {
            return null;
        }
    }

    private static string RedirectUri(MalOptions opts) => $"http://localhost:{opts.CallbackPort}/callback";

    private class MalTokenResponse
    {
        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = "";

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = "";

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; } = "";
    }

    private class MalTokenErrorResponse
    {
        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }
}
