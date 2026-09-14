namespace AnimeTracker.Api.Services.Mal.Auth;

/// <summary>Supplies a valid (non-expired) MAL access token for the pacing
/// handler to attach as a bearer token, refreshing it on the request path
/// when needed as well as in the background.</summary>
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

    /// <summary>Used by the background refresh. Refreshes the stored login
    /// only when a login is stored, the connection isn't lost, and no more
    /// than <paramref name="window"/> is left before it expires — holding the
    /// same lock as the other two methods, and reading the stored login
    /// inside it. Returns null when no refresh was attempted; otherwise
    /// returns whatever <c>RefreshAsync</c> returned.</summary>
    Task<MalRefreshResult?> RefreshIfExpiringWithinAsync(TimeSpan window, CancellationToken ct = default);
}
