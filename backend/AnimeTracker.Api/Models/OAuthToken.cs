namespace AnimeTracker.Api.Models;

/// <summary>Single-row table holding the current MAL OAuth token set for the one
/// user of this app. Persisted in Postgres (not memory/filesystem) so it survives
/// container restarts.</summary>
public class OAuthToken
{
    public int Id { get; set; }
    public required string AccessToken { get; set; }
    public required string RefreshToken { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
