using AnimeTracker.Api.Services.Mal;

namespace AnimeTracker.Api.Services.Setup;

/// <summary>Which of the two MyAnimeList credentials is missing. Both the
/// client id and the client secret are required: the authorize URL carries the
/// id, and the code exchange and every token refresh send both. The backend
/// still starts without them, so the setup screen can name what is missing
/// (design D5).</summary>
public static class MalCredentials
{
    public const string ClientIdName = "MAL_CLIENT_ID";
    public const string ClientSecretName = "MAL_CLIENT_SECRET";

    /// <summary>The <c>.env</c> names, client id first, of the credentials whose
    /// value is null, empty or whitespace. Empty when both are set. Never
    /// includes a value.</summary>
    public static IReadOnlyList<string> Missing(MalOptions options)
    {
        var missing = new List<string>(2);
        if (string.IsNullOrWhiteSpace(options.ClientId))
            missing.Add(ClientIdName);
        if (string.IsNullOrWhiteSpace(options.ClientSecret))
            missing.Add(ClientSecretName);
        return missing;
    }
}
