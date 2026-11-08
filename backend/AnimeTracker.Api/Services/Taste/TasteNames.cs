namespace AnimeTracker.Api.Services.Taste;

/// <summary>Which of the names MyAnimeList stores under an anime's one list of
/// genres, themes and demographics are demographics (taste-stats "Demographics
/// are not genres", design.md D2). Every name that is not one of the five is a
/// genre, theme and genre alike. Names are matched exactly as MyAnimeList
/// stores them.</summary>
public static class TasteNames
{
    public static readonly IReadOnlyList<string> Demographics = ["Shounen", "Seinen", "Shoujo", "Josei", "Kids"];

    private static readonly HashSet<string> DemographicSet = new(Demographics, StringComparer.Ordinal);

    public static bool IsDemographic(string name) => DemographicSet.Contains(name);
}
