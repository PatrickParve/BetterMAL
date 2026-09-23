using AnimeTracker.Api.Services.Airing;

namespace AnimeTracker.Api.Tests.Services.Airing;

// AiringWeekRange (design D8, tasks.md 2.3): the airing schedule's own
// accepted date range, with the same one-local-day lead as RecapRange.
public class AiringWeekRangeTests
{
    [Fact]
    public void Earliest_Is1917January1()
    {
        Assert.Equal(new DateOnly(1917, 1, 1), AiringWeekRange.Earliest);
    }

    [Fact]
    public void Latest_IsDecember31OfTheYearAfterLocalToday()
    {
        Assert.Equal(new DateOnly(2027, 12, 31), AiringWeekRange.Latest(new DateOnly(2026, 9, 23)));
    }

    [Fact]
    public void Latest_TheOneDayLeadCanPushTheYearAheadAtAYearBoundary()
    {
        Assert.Equal(new DateOnly(2028, 12, 31), AiringWeekRange.Latest(new DateOnly(2026, 12, 31)));
    }

    [Theory]
    [InlineData(1917, 1, 1)]
    [InlineData(2027, 12, 31)]
    public void Contains_AcceptsBothEnds(int year, int month, int day)
    {
        var today = new DateOnly(2026, 9, 23);

        Assert.True(AiringWeekRange.Contains(new DateOnly(year, month, day), today));
    }

    [Theory]
    [InlineData(1916, 12, 31)]
    [InlineData(2028, 1, 1)]
    public void Contains_RefusesOneDayBeyondEachEnd(int year, int month, int day)
    {
        var today = new DateOnly(2026, 9, 23);

        Assert.False(AiringWeekRange.Contains(new DateOnly(year, month, day), today));
    }
}
