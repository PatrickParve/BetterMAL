using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Services.Mal.Auth;

public interface IMalOAuthService
{
    /// <summary>Builds the MAL authorize URL for a fresh PKCE attempt
    /// (code_challenge_method=plain), recording the pending verifier.</summary>
    string BuildAuthorizeUrl();

    /// <summary>Completes the flow: validates state, exchanges the code for
    /// tokens, and persists them.</summary>
    Task<OAuthToken> HandleCallbackAsync(string code, string state, CancellationToken ct = default);

    /// <summary>Exchanges a refresh token for a new token set. Tells MyAnimeList
    /// refusing this login apart from an outage, and records a refusal as a
    /// lost connection (design.md D11).</summary>
    Task<MalRefreshResult> RefreshAsync(string refreshToken, CancellationToken ct = default);
}
