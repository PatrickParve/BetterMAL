using System.Globalization;
using System.Text;
using AnimeTracker.Api.Services.Airing.AniList;

namespace AnimeTracker.Api.Services.Studios;

/// <summary>Pairs the studios MyAnimeList credits an anime with to the same anime's
/// studios on AniList (studio-links design.md D7). The two sites spell a name
/// differently often enough ("J.C.Staff" and "J.C. Staff", "Studio Deen" and
/// "Studio DEEN") that names are compared after normalising, and only inside one
/// anime's own credits, which makes a wrong pairing nearly impossible even where
/// AniList lists producers beside the animation studio.</summary>
public static class StudioNameMatcher
{
    private const string StudioWord = "studio";

    /// <summary>The form two spellings of one name are compared in: decomposed
    /// (NFKD) with accents stripped, lowercase, everything that is not a letter or a
    /// digit dropped, and a leading "studio" dropped when something remains after it.</summary>
    public static string Normalise(string name)
    {
        var decomposed = name.Normalize(NormalizationForm.FormKD);

        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsLetterOrDigit(character))
                builder.Append(char.ToLowerInvariant(character));
        }

        var normalised = builder.ToString();
        return normalised.StartsWith(StudioWord, StringComparison.Ordinal) && normalised.Length > StudioWord.Length
            ? normalised[StudioWord.Length..]
            : normalised;
    }

    /// <summary>For each MyAnimeList name of one anime, the AniList id of that same
    /// anime's studio whose name is the same once both are normalised. A name with no
    /// such studio is absent from the result. Should AniList list two studios that
    /// normalise alike, the first listed is taken.</summary>
    public static IReadOnlyDictionary<string, int> Match(
        IEnumerable<string> malNames, IReadOnlyList<AniListStudio> aniListStudios)
    {
        var matches = new Dictionary<string, int>(StringComparer.Ordinal);
        if (aniListStudios.Count == 0)
            return matches;

        var aniListByNormalName = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var studio in aniListStudios)
        {
            var normalName = Normalise(studio.Name);
            if (normalName.Length > 0)
                aniListByNormalName.TryAdd(normalName, studio.Id);
        }

        foreach (var malName in malNames)
        {
            // A name with nothing left to compare once normalised has nothing to match.
            var normalName = Normalise(malName);
            if (normalName.Length > 0 && aniListByNormalName.TryGetValue(normalName, out var aniListId))
                matches[malName] = aniListId;
        }

        return matches;
    }
}
