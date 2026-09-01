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

    // MAL popularity is a rank (1 = most popular); 0 or null means "unranked"
    // and must sort last rather than ahead of rank 1.
    public static int PopularityKey(int? rank) => rank is null or 0 ? int.MaxValue : rank.Value;
}
