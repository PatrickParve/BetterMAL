using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Services.Series;

/// <summary>The contiguous-trim rule for a custom series title (design.md
/// D8): a candidate is accepted only when it appears, after normalizing, as
/// an unbroken run of text inside at least one offered title. `Beyblade:
/// Metal Fusion` therefore yields `Beyblade`, `Metal Fusion`, `Beyblade:
/// Metal`, and the whole string; it does not yield `Beyblade Fusion` (a
/// mid-string cut), `Beyblade Metal Fusion Remastered` (added text), or
/// `bladusin` (invented text).</summary>
public static class SeriesTitleRule
{
    // Colon, semicolon, comma, hyphen, en dash, em dash, full stop,
    // exclamation/question marks, and straight/curly quotes — trimmed only
    // from the candidate's boundary, so cutting "Beyblade: Metal Fusion" at
    // the colon yields "Beyblade" rather than failing on a dangling mark.
    private static readonly char[] BoundaryPunctuation =
        [':', ';', ',', '-', '–', '—', '.', '!', '?', '"', '\'', '“', '”', '‘', '’'];

    /// <summary>Collapses surrounding and internal whitespace runs to single
    /// spaces, then trims boundary punctuation. This is both the acceptance
    /// check's normalized candidate and what gets stored — capitalisation is
    /// left untouched since comparison is case-insensitive and the user's
    /// casing is part of their choice.</summary>
    public static string Normalize(string candidate) =>
        CollapseWhitespace(candidate).Trim(BoundaryPunctuation);

    private static string CollapseWhitespace(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>True when <paramref name="candidate"/>, once normalized,
    /// appears as an unbroken, case-insensitive run of text inside at least
    /// one of <paramref name="offeredTitles"/> (whitespace-normalized only —
    /// offered titles keep their own punctuation).</summary>
    public static bool IsAcceptable(string candidate, IEnumerable<string> offeredTitles)
    {
        var normalized = Normalize(candidate);
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        return offeredTitles.Any(offered =>
            CollapseWhitespace(offered).Contains(normalized, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The titles a series may take: every main-line member's MAL
    /// title, then its English title where MAL provides one, in the order
    /// <paramref name="mainLineMembers"/> is given (main-line watch order),
    /// deduplicated. Extras are never passed in, so their titles are never
    /// offered — a series' title describes the franchise's main line.</summary>
    public static List<string> OfferedTitles(IEnumerable<SeriesMember> mainLineMembers)
    {
        var titles = new List<string>();
        foreach (var member in mainLineMembers)
        {
            if (!titles.Contains(member.Anime.Title))
                titles.Add(member.Anime.Title);
            if (member.Anime.EnglishTitle is { } englishTitle && !titles.Contains(englishTitle))
                titles.Add(englishTitle);
        }

        return titles;
    }
}
