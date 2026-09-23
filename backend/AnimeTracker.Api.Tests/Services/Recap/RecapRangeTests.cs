using AnimeTracker.Api.Services.Recap;

namespace AnimeTracker.Api.Tests.Services.Recap;

// RecapRange.For (design D6, tasks.md 1.5): the returned range's ceiling is
// one local day ahead of the date passed in, so a browser already past a
// season or year boundary is still inside what the API accepts.
public class RecapRangeTests
{
    [Theory]
    [InlineData(2026, 9, 23, 2026, "summer")]
    [InlineData(2026, 9, 30, 2026, "fall")]
    [InlineData(2026, 12, 31, 2027, "winter")]
    [InlineData(2026, 6, 15, 2026, "spring")]
    public void For_TheCeilingIsOneDayAheadOfLocalToday(int year, int month, int day, int expectedYear, string expectedSeason)
    {
        var range = RecapRange.For(new DateOnly(year, month, day));

        Assert.Equal(expectedYear, range.LatestYear);
        Assert.Equal(expectedSeason, range.LatestSeason);
    }

    [Theory]
    [InlineData(2026, 9, 23)]
    [InlineData(1917, 1, 1)]
    [InlineData(2099, 12, 31)]
    public void For_TheEarliestYearIsAlways1917(int year, int month, int day)
    {
        var range = RecapRange.For(new DateOnly(year, month, day));

        Assert.Equal(1917, range.EarliestYear);
    }
}
