namespace AnimeTracker.Api.Services.Library;

/// <summary>The MAL `ranking_type` tokens this app exposes on the Top Anime
/// page selector. `ona` and `music` are deliberately absent: MAL API v2's
/// `/anime/ranking` answers both with `400 invalid ranking_type` (verified
/// against the live API), and this app does not introduce a second data
/// provider to cover them.</summary>
public static class TopAnimeRankingType
{
    public const string All = "all";
    public const string Tv = "tv";
    public const string Movie = "movie";
    public const string Ova = "ova";
    public const string Special = "special";
    public const string ByPopularity = "bypopularity";
    public const string Favorite = "favorite";

    public const string Default = All;

    public static readonly IReadOnlyList<string> SupportedValues =
        [All, Tv, Movie, Ova, Special, ByPopularity, Favorite];

    public static bool IsSupported(string rankingType) => SupportedValues.Contains(rankingType);

    /// <summary>Parses a ranking type from user input (e.g. a query
    /// parameter). A missing value (<c>null</c>) defaults to <see cref="Default"/>;
    /// a present-but-unrecognised value returns false rather than silently
    /// falling back to the default.</summary>
    public static bool TryParse(string? value, out string rankingType)
    {
        if (value is null)
        {
            rankingType = Default;
            return true;
        }

        if (IsSupported(value))
        {
            rankingType = value;
            return true;
        }

        rankingType = Default;
        return false;
    }
}
