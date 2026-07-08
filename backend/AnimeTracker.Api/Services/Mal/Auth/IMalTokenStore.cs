using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Services.Mal.Auth;

/// <summary>Persists the single MAL OAuth token set (this app has exactly one
/// user/account) in Postgres, so it survives container restarts.</summary>
public interface IMalTokenStore
{
    Task<OAuthToken?> GetAsync(CancellationToken ct = default);
    Task SaveAsync(string accessToken, string refreshToken, DateTimeOffset expiresAt, CancellationToken ct = default);
}
