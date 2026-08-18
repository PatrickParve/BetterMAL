using AnimeTracker.Api.Services.Season;

namespace AnimeTracker.Api.Tests.Services.Season;

public class SeasonCalendarTests
{
    [Theory]
    [InlineData(2026, "fall", 1, 2027, "winter")] // forward across a year boundary
    [InlineData(2027, "winter", -1, 2026, "fall")] // backward across a year boundary
    [InlineData(2026, "summer", 2, 2027, "winter")] // multi-step forward — the default horizon window
    [InlineData(2026, "summer", -6, 2025, "winter")] // multi-step backward, crossing more than one year
    public void ShiftCrossesYearBoundaries(int year, string season, int delta, int expectedYear, string expectedSeason)
    {
        var (resultYear, resultSeason) = SeasonCalendar.Shift(year, season, delta);

        Assert.Equal(expectedYear, resultYear);
        Assert.Equal(expectedSeason, resultSeason);
    }

    [Theory]
    [InlineData(2026, "winter")]
    [InlineData(2026, "spring")]
    [InlineData(2026, "summer")]
    [InlineData(2026, "fall")]
    [InlineData(1989, "winter")]
    [InlineData(2050, "fall")]
    public void SeasonPointIndexRoundTrips(int year, string season)
    {
        var index = SeasonCalendar.GetSeasonPointIndex(year, season);
        var (resultYear, resultSeason) = SeasonCalendar.FromSeasonPointIndex(index);

        Assert.Equal(year, resultYear);
        Assert.Equal(season, resultSeason);
    }
}
