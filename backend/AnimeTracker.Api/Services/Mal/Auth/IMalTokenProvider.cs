namespace AnimeTracker.Api.Services.Mal.Auth;

/// <summary>Supplies a valid (non-expired) MAL access token for the pacing
/// handler to attach as a bearer token, refreshing lazily if needed.</summary>
public interface IMalTokenProvider
{
    /// <summary>Returns a valid access token, or null if none is available
    /// (never authorized, or the connection is lost and re-authorization is
    /// required).</summary>
    Task<string?> GetValidAccessTokenAsync(CancellationToken ct = default);

    /// <summary>Called when a Bearer request comes back 401 despite a stored
    /// token that hadn't reached its expiry buffer — MAL has revoked the
    /// login early. Forces one refresh (or reuses a concurrent caller's) and
    /// reports what happened, so the pacing handler can retry, refuse, or
    /// pass the original response through (design.md D12).</summary>
    Task<MalRefreshResult> RefreshAfterRejectionAsync(string rejectedAccessToken, CancellationToken ct = default);
}
