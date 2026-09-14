using AnimeTracker.Api.Services.Season;

namespace AnimeTracker.Api.Tests.Services.Season;

// SeasonRequestRange (design D7, tasks.md 8.2): a pure record, so these
// tests drive ContainsYear/ContainsSeason directly rather than through a DB
// or a controller.
public class SeasonRequestRangeTests
{
    private static readonly SeasonRequestRange Range = new(1917, 2027, "winter");

    [Theory]
    [InlineData(1917, "winter")]
    [InlineData(2027, "winter")]
    [InlineData(2026, "fall")]
    [InlineData(1988, "summer")]
    public void ContainsSeason_AcceptsBothEndsAndEverythingBetween(int year, string season)
    {
        Assert.True(Range.ContainsSeason(year, season));
    }

    [Theory]
    [InlineData(1916, "fall")]
    [InlineData(2027, "spring")]
    [InlineData(2028, "winter")]
    [InlineData(9999, "winter")]
    [InlineData(1800, "winter")]
    [InlineData(0, "winter")]
    [InlineData(-1, "fall")]
    [InlineData(int.MaxValue, "winter")]
    // The overflow comment's own example: GetSeasonPointIndex is year * 4,
    // which wraps in unchecked arithmetic so that 1073743741 * 4 lands on
    // winter 1917's index. The year-first check in ContainsSeason has to
    // refuse this on the year alone, before any index comparison runs.
    [InlineData(1073743741, "winter")]
    public void ContainsSeason_RefusesOutsideTheRange(int year, string season)
    {
        Assert.False(Range.ContainsSeason(year, season));
    }

    [Theory]
    [InlineData(1917)]
    [InlineData(2027)]
    public void ContainsYear_AcceptsBothEnds(int year)
    {
        Assert.True(Range.ContainsYear(year));
    }

    [Theory]
    [InlineData(1916)]
    [InlineData(2028)]
    [InlineData(int.MaxValue)]
    [InlineData(1073743741)]
    public void ContainsYear_RefusesOutsideTheRange(int year)
    {
        Assert.False(Range.ContainsYear(year));
    }

    // The earliest end is always winter (design D7), so a range whose ceiling
    // isn't winter still accepts every season of an earlier year — the year
    // check alone decides once the year is below LatestYear.
    [Theory]
    [InlineData("winter")]
    [InlineData("spring")]
    [InlineData("summer")]
    [InlineData("fall")]
    public void ContainsSeason_ARangeEndingAtFallAcceptsEverySeasonOfItsLastYear(string season)
    {
        var range = new SeasonRequestRange(1917, 2027, "fall");

        Assert.True(range.ContainsSeason(2027, season));
    }
}
