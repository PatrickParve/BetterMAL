using AnimeTracker.Api.Services.Search;

namespace AnimeTracker.Api.Tests.Services.Search;

// improve-search-page task 3.1: the one reading of a typed query, shared by the anime search
// and the OP/ED search so they can't come to disagree about what a query means. The anime
// search's own behaviour is held by AnimeSearchServiceTests, unchanged.
public class SearchQueryTests
{
    [Theory]
    [InlineData("naruto", "naruto", false)]
    [InlineData("  naruto  ", "naruto", false)]
    [InlineData("\tnaruto\n", "naruto", false)]
    [InlineData("\"naruto\"", "naruto", true)]
    [InlineData("  \"naruto\"  ", "naruto", true)]
    [InlineData("\"blue bird\"", "blue bird", true)]
    [InlineData("\"  naruto  \"", "  naruto  ", true)] // only the outside is trimmed
    [InlineData("\"naruto", "\"naruto", false)]        // half a pair is just a character
    [InlineData("naruto\"", "naruto\"", false)]
    [InlineData("\"", "\"", false)]                    // one quote is not a pair
    [InlineData("\"\"", "", true)]                     // an empty exact term, which matches nothing
    [InlineData("", "", false)]
    [InlineData("   ", "", false)]
    public void Parse_TrimsAndReadsASurroundingPairOfQuotesAsExact(string query, string term, bool exact)
    {
        Assert.Equal((term, exact), SearchQuery.Parse(query));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!!!")]
    [InlineData("\"")]
    [InlineData("\"\"")]
    [InlineData("\"!!\"")]
    public void ATermThatNormalizesToNothingIsLeftToTheCallersToRefuse(string query)
    {
        // Parse itself keeps the term raw; both searches return early when it normalizes
        // to nothing, so "!!!" matches nothing instead of everything.
        var (term, _) = SearchQuery.Parse(query);

        Assert.Equal(0, SearchTextMatch.Normalize(term).Length);
    }
}
