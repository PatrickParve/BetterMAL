namespace AnimeTracker.Api.Services.Mal.Auth;

/// <summary>Supplies a valid (non-expired) MAL access token for the pacing
/// handler to attach as a bearer token, refreshing lazily if needed.</summary>
public interface IMalTokenProvider
{
    /// <summary>Returns a valid access token, or null if none is available
    /// (never authorized, or refresh failed and re-authorization is required).</summary>
    Task<string?> GetValidAccessTokenAsync(CancellationToken ct = default);
}
