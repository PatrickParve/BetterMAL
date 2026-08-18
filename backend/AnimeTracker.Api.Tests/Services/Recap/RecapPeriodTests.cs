using AnimeTracker.Api.Services.Recap;

namespace AnimeTracker.Api.Tests.Services.Recap;

// RecapPeriod (tasks.md 2.1): pure resolution from mode + parameters to a
// date range and to the years/(year, season) points a period covers.
public class RecapPeriodTests
{
    [Fact]
    public void MultiYearResolvesToFullInclusiveRange()
    {
        var period = RecapPeriod.MultiYear(2011, 2020);

        Assert.Equal((new DateOnly(2011, 1, 1), new DateOnly(2020, 12, 31)), period.DateRange);
    }

    [Fact]
    public void YearlyResolvesToOneCalendarYear()
    {
        var period = RecapPeriod.Yearly(2022);

        Assert.Equal((new DateOnly(2022, 1, 1), new DateOnly(2022, 12, 31)), period.DateRange);
    }

    [Fact]
    public void SeasonResolvesToItsQuarter()
    {
        var period = RecapPeriod.OfSeason(2019, "fall");

        Assert.Equal((new DateOnly(2019, 10, 1), new DateOnly(2019, 12, 31)), period.DateRange);
    }

    [Fact]
    public void MultiYearOfOneYearCoversJustThatYear()
    {
        var period = RecapPeriod.MultiYear(2022, 2022);

        Assert.Equal((new DateOnly(2022, 1, 1), new DateOnly(2022, 12, 31)), period.DateRange);
    }

    // Spec scenario "Reversed multi-year bounds": the two bounds are treated
    // as an inclusive range in ascending order rather than producing an
    // empty period.
    [Fact]
    public void ReversedMultiYearBoundsNormaliseToAscending()
    {
        var reversed = RecapPeriod.MultiYear(2020, 2011);
        var ascending = RecapPeriod.MultiYear(2011, 2020);

        Assert.Equal(ascending.DateRange, reversed.DateRange);
        Assert.Equal(2011, reversed.StartYear);
        Assert.Equal(2020, reversed.EndYear);
    }

    [Fact]
    public void YearsSpansEveryCalendarYearInclusive()
    {
        var period = RecapPeriod.MultiYear(2018, 2020);

        Assert.Equal([2018, 2019, 2020], period.Years);
    }

    [Fact]
    public void SeasonPointsForAYearlyPeriodIsItsFourSeasons()
    {
        var period = RecapPeriod.Yearly(2022);

        Assert.Equal(
            [(2022, "winter"), (2022, "spring"), (2022, "summer"), (2022, "fall")],
            period.SeasonPoints);
    }

    [Fact]
    public void SeasonPointsForASeasonPeriodIsJustItself()
    {
        var period = RecapPeriod.OfSeason(2019, "fall");

        Assert.Equal([(2019, "fall")], period.SeasonPoints);
    }
}
