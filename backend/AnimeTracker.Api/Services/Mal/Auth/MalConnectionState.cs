using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Services.Mal.Auth;

/// <summary>The state of the MyAnimeList login as the frontend reads it: no login
/// stored, one MyAnimeList has since refused, or a healthy one (design.md D14). One
/// definition, shared by <c>api/mal-auth/status</c> and the setup status.</summary>
public static class MalConnectionState
{
    public const string NotConnected = "NotConnected";
    public const string Lost = "Lost";
    public const string Connected = "Connected";

    public static string Of(OAuthToken? token) =>
        token is null ? NotConnected : token.ConnectionLostAt is not null ? Lost : Connected;
}
