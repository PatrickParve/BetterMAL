using AnimeTracker.Api.Services.Search;

namespace AnimeTracker.Api.Tests.Services.Search;

// improve-search-page task 3.2: the matchers that take text already normalized, which let the
// OP/ED search compare 14.6k rows without renormalizing them on every keystroke. They must agree
// with the matchers every other search uses, on every input.
public class SearchTextMatchPrenormalizedTests
{
    // A spread of titles: plain, case, spacing, punctuation, accents, kana with voiced marks,
    // kanji, a title of nothing but punctuation, and the empty one.
    private static readonly string[] Titles =
    [
        "Blue Bird", "blue bird", "BLUE-BIRD", "Blue Bird Days", "Beyond Blue Bird",
        "Clémentine", "Clementine", "Fate/Zero", "Fate / Zero 2nd Season", "Full-Metal Alchemist",
        "ゲゲゲの鬼太郎", "ゲゲゲ", "鋼の錬金術師", "Ōkami-san", "Okami san",
        "K-ON!!", "Re:Zero", "86 -Eighty Six-", "!!!", "", "   ",
    ];

    private static readonly string[] Terms =
    [
        "blue bird", "bluebird", "Blue-Bird", "BLUE", "bird", "days", "clementine", "Clémentine",
        "fate", "fatezero", "zero", "full metal", "ゲゲゲ", "げげげ", "鋼", "okami", "kon", "re zero",
        "86", "!!!", "x", "blue bird days beyond",
    ];

    [Fact]
    public void TheyAgreeWithTheNormalizingMatchersOverASpreadOfTitlesAndTerms()
    {
        var disagreements = new List<string>();
        foreach (var title in Titles)
        {
            foreach (var term in Terms)
            {
                var value = SearchTextMatch.Normalize(title);
                var key = SearchTextMatch.Normalize(term);

                Check(disagreements, "equals", title, term,
                    SearchTextMatch.EqualsNormalized(title, term), SearchTextMatch.EqualsPrenormalized(value, key));
                Check(disagreements, "starts with", title, term,
                    SearchTextMatch.StartsWithNormalized(title, term), SearchTextMatch.StartsWithPrenormalized(value, key));
                Check(disagreements, "contains", title, term,
                    SearchTextMatch.ContainsNormalized(title, term), SearchTextMatch.ContainsPrenormalized(value, key));
            }
        }

        Assert.True(disagreements.Count == 0, string.Join(Environment.NewLine, disagreements.Take(10)));
        Assert.True(Titles.Length * Terms.Length >= 400); // a spread, not a spot check
    }

    private static void Check(List<string> disagreements, string matcher, string title, string term, bool existing, bool prenormalized)
    {
        if (existing != prenormalized)
            disagreements.Add($"{matcher}: \"{title}\" vs \"{term}\": existing {existing}, prenormalized {prenormalized}");
    }

    [Fact]
    public void ATitleThatIsNothingButPunctuationEqualsATermThatIsToo_AsTheExistingMatcherSays()
    {
        // Both normalize to "". The searches refuse such a query before matching; the matchers
        // themselves must still say what the existing ones say.
        Assert.True(SearchTextMatch.EqualsNormalized("!!!", "???"));
        Assert.True(SearchTextMatch.EqualsPrenormalized("", ""));
    }

    [Fact]
    public void ANullValueMatchesNothing()
    {
        Assert.False(SearchTextMatch.EqualsPrenormalized(null, "blue"));
        Assert.False(SearchTextMatch.StartsWithPrenormalized(null, "blue"));
        Assert.False(SearchTextMatch.ContainsPrenormalized(null, "blue"));
    }

    [Fact]
    public void TheyCompareTheirInputsAsTheyAre_SoNormalizingIsTheCallersJob()
    {
        // Nothing here lower-cases or strips: that is what makes them cheap, and what the
        // index does once per row instead of the search doing it per keystroke.
        Assert.True(SearchTextMatch.ContainsPrenormalized("bluebird", "uebi"));
        Assert.False(SearchTextMatch.ContainsPrenormalized("bluebird", "Blue"));
        Assert.False(SearchTextMatch.StartsWithPrenormalized("blue bird", "bluebird"));
    }
}
