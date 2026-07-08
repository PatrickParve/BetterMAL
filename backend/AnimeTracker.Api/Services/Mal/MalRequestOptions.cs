namespace AnimeTracker.Api.Services.Mal;

public enum MalAuthMode
{
    /// <summary>Public/read endpoints — attach X-MAL-Client-ID only.</summary>
    ClientId,

    /// <summary>User-specific endpoints — attach an OAuth2 bearer token.</summary>
    Bearer,
}

/// <summary>Per-request signal read by <see cref="MalAuthPacingHandler"/> to decide
/// which auth header a request needs. Set by IMalClient when building each request.</summary>
public static class MalRequestOptions
{
    public static readonly HttpRequestOptionsKey<MalAuthMode> AuthModeKey = new("MalAuthMode");
}
