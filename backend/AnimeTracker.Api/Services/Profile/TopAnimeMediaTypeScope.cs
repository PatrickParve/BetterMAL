using System.Diagnostics.CodeAnalysis;

namespace AnimeTracker.Api.Services.Profile;

/// <summary>The six "My top anime" filter scopes (case-insensitive). "special"
/// also matches MAL's "tv_special" media type, since MAL emits both for what
/// the UI treats as one option.</summary>
public static class TopAnimeMediaTypeScope
{
    public const string All = "all";

    private static readonly HashSet<string> ValidScopes = new(StringComparer.OrdinalIgnoreCase)
    {
        "all", "tv", "movie", "ova", "ona", "special",
    };

    public static bool IsValid([NotNullWhen(true)] string? scope) => scope is not null && ValidScopes.Contains(scope);

    public static bool Matches(string scope, string? mediaType)
    {
        if (string.Equals(scope, All, StringComparison.OrdinalIgnoreCase))
            return true;

        if (mediaType is null)
            return false;

        if (string.Equals(scope, "special", StringComparison.OrdinalIgnoreCase))
            return string.Equals(mediaType, "special", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mediaType, "tv_special", StringComparison.OrdinalIgnoreCase);

        return string.Equals(scope, mediaType, StringComparison.OrdinalIgnoreCase);
    }
}
