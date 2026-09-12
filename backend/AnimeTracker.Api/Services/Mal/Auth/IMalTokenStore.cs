using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Services.Mal.Auth;

/// <summary>Persists the single MAL OAuth token set (this app has exactly one
/// user/account) in Postgres, so it survives container restarts.</summary>
public interface IMalTokenStore
{
    Task<OAuthToken?> GetAsync(CancellationToken ct = default);
    Task SaveAsync(string accessToken, string refreshToken, DateTimeOffset expiresAt, CancellationToken ct = default);

    /// <summary>Records that MyAnimeList has refused to refresh the stored
    /// login, keeping the first time this was noticed — a later refusal
    /// doesn't overwrite an earlier <c>ConnectionLostAt</c> (design.md D13).
    /// Does nothing when no login is stored.</summary>
    Task MarkConnectionLostAsync(DateTimeOffset at, CancellationToken ct = default);
}
