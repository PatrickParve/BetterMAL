using System.Globalization;
using System.Text;

namespace AnimeTracker.Api.Services.Search;

/// <summary>Title-matching and popularity-ranking helpers shared by anime and
/// series search, so both match a query the same way (design.md decision
/// 1).</summary>
internal static class SearchTextMatch
{
    /// <summary>Lower-cases, folds accents to their base letters, and drops
    /// every character that is not a letter or digit — whitespace,
    /// punctuation, and symbols alike — so "Full-Metal", "full metal", and
    /// "FullMetal" all normalize the same. Letters of every script survive
    /// (kana, kanji, Cyrillic, ...), so a Japanese title matches exactly as
    /// it did before this normalization existed (design.md D1).</summary>
    public static string Normalize(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;
            if (!char.IsLetterOrDigit(c))
                continue;

            builder.Append(char.ToLowerInvariant(c));
        }

        return builder.ToString();
    }

    public static bool EqualsNormalized(string? value, string term) =>
        value is not null && string.Equals(Normalize(value), Normalize(term), StringComparison.Ordinal);

    public static bool ContainsNormalized(string? value, string term) =>
        value is not null && Normalize(value).Contains(Normalize(term), StringComparison.Ordinal);

    public static bool StartsWithNormalized(string? value, string term) =>
        value is not null && Normalize(value).StartsWith(Normalize(term), StringComparison.Ordinal);

    // The same three comparisons for text that has already been through Normalize on both sides.
    // The openings-and-endings search holds every catalogue row's texts normalized once, so a
    // keystroke compares 14.6k rows without normalizing any of them again (improve-search-page
    // design.md D7). They compare exactly what they are given: normalizing is the caller's job,
    // and given normalized input they agree with the three above for every value and term.
    public static bool EqualsPrenormalized(string? normalizedValue, string normalizedTerm) =>
        normalizedValue is not null && string.Equals(normalizedValue, normalizedTerm, StringComparison.Ordinal);

    public static bool StartsWithPrenormalized(string? normalizedValue, string normalizedTerm) =>
        normalizedValue is not null && normalizedValue.StartsWith(normalizedTerm, StringComparison.Ordinal);

    public static bool ContainsPrenormalized(string? normalizedValue, string normalizedTerm) =>
        normalizedValue is not null && normalizedValue.Contains(normalizedTerm, StringComparison.Ordinal);

    /// <summary>Word-anchored contains: true when every word of
    /// <paramref name="term"/> lines up, in order, with a run of whole words
    /// inside <paramref name="value"/>. Unlike <see cref="ContainsNormalized"/>,
    /// a query word must match an entire title word rather than a letter run
    /// buried inside one — "miss" must not match inside "Mission" the way it
    /// would as a raw substring. Used for series matching, where a stray
    /// mid-word hit on one member's title would otherwise surface an entire
    /// unrelated franchise.</summary>
    public static bool ContainsWholeWord(string? value, string term)
    {
        if (value is null)
            return false;

        var valueWords = SplitWords(value);
        var termWords = SplitWords(term);
        if (termWords.Count == 0)
            return false;

        for (var start = 0; start <= valueWords.Count - termWords.Count; start++)
        {
            var matched = true;
            for (var i = 0; i < termWords.Count; i++)
            {
                if (valueWords[start + i] != termWords[i])
                {
                    matched = false;
                    break;
                }
            }
            if (matched)
                return true;
        }

        return false;
    }

    private static List<string> SplitWords(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var words = new List<string>();
        var current = new StringBuilder();
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;
            if (char.IsLetterOrDigit(c))
            {
                current.Append(char.ToLowerInvariant(c));
            }
            else if (current.Length > 0)
            {
                words.Add(current.ToString());
                current.Clear();
            }
        }
        if (current.Length > 0)
            words.Add(current.ToString());

        return words;
    }

    // MAL popularity is a rank (1 = most popular); 0 or null means "unranked"
    // and must sort last rather than ahead of rank 1.
    public static int PopularityKey(int? rank) => rank is null or 0 ? int.MaxValue : rank.Value;
}
