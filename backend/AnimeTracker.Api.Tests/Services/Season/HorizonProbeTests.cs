using AnimeTracker.Api.Services.Season;

namespace AnimeTracker.Api.Tests.Services.Season;

// HorizonProbe (design.md D10/D10a, tasks.md 2.1/2.2): a pure function, so
// these tests drive it directly with hand-built dates rather than a clock or
// a DB.
public class HorizonProbeTests
{
    // Summer 2026 (Jul-Sep); final month is September.
    private static readonly (int Year, string Season) Current = (2026, "summer");
    private static readonly (int Year, string Season) DefaultOuterCeiling = (2027, "winter"); // current + 2
    private static readonly DateOnly InLastMonth = new(2026, 9, 15);

    [Fact]
    public void TargetIsCurrentPlusThree()
    {
        Assert.Equal((2027, "spring"), HorizonProbe.Target(Current));
    }

    [Fact]
    public void FiresOnFirstVisitOfADayInTheLastMonth()
    {
        var shouldProbe = HorizonProbe.ShouldProbe(Current, InLastMonth, DefaultOuterCeiling, targetLastFetchedLocalDate: null);

        Assert.True(shouldProbe);
    }

    [Fact]
    public void NotTwiceTheSameDay()
    {
        var shouldProbe = HorizonProbe.ShouldProbe(Current, InLastMonth, DefaultOuterCeiling, targetLastFetchedLocalDate: InLastMonth);

        Assert.False(shouldProbe);
    }

    [Fact]
    public void FiresAgainOnALaterDayTheSameMonth()
    {
        var askedYesterday = InLastMonth.AddDays(-1);

        var shouldProbe = HorizonProbe.ShouldProbe(Current, InLastMonth, DefaultOuterCeiling, targetLastFetchedLocalDate: askedYesterday);

        Assert.True(shouldProbe);
    }

    [Theory]
    [InlineData(2026, 7, 1)] // first month of the season
    [InlineData(2026, 8, 15)] // second month of the season
    public void NotAtAllInTheFirstTwoMonths(int year, int month, int day)
    {
        var today = new DateOnly(year, month, day);

        var shouldProbe = HorizonProbe.ShouldProbe(Current, today, DefaultOuterCeiling, targetLastFetchedLocalDate: null);

        Assert.False(shouldProbe);
    }

    [Fact]
    public void NotOnceTheTargetIsAtTheCeiling()
    {
        var ceilingAtTarget = HorizonProbe.Target(Current); // spring 2027

        var shouldProbe = HorizonProbe.ShouldProbe(Current, InLastMonth, ceilingAtTarget, targetLastFetchedLocalDate: null);

        Assert.False(shouldProbe);
    }

    [Fact]
    public void NotOnceTheTargetIsBelowTheCeiling()
    {
        var ceilingPastTarget = (2027, "summer"); // current + 4, past the target

        var shouldProbe = HorizonProbe.ShouldProbe(Current, InLastMonth, ceilingPastTarget, targetLastFetchedLocalDate: null);

        Assert.False(shouldProbe);
    }

    [Fact]
    public void TheTargetDoesNotMoveWhenTheCeilingRises()
    {
        var raisedCeiling = (2027, "spring"); // ceiling risen to exactly the old target

        Assert.Equal(HorizonProbe.Target(Current), HorizonProbe.Target(Current));
        Assert.Equal((2027, "spring"), HorizonProbe.Target(Current));
        // A ceiling risen to the target itself stops further probing (the
        // case above), rather than the target moving one further out.
        Assert.False(HorizonProbe.ShouldProbe(Current, InLastMonth, raisedCeiling, targetLastFetchedLocalDate: null));
    }

    [Fact]
    public void OnDemandIgnoresTheLastMonthWindow()
    {
        var firstMonth = new DateOnly(2026, 7, 1);

        var shouldProbe = HorizonProbe.ShouldProbe(Current, firstMonth, DefaultOuterCeiling, targetLastFetchedLocalDate: null, onDemand: true);

        Assert.True(shouldProbe);
    }

    [Fact]
    public void OnDemandStillHonoursTheOncePerDayGate()
    {
        var firstMonth = new DateOnly(2026, 7, 1);

        var shouldProbe = HorizonProbe.ShouldProbe(Current, firstMonth, DefaultOuterCeiling, targetLastFetchedLocalDate: firstMonth, onDemand: true);

        Assert.False(shouldProbe);
    }

    [Fact]
    public void OnDemandStillRequiresTheTargetToBePastTheCeiling()
    {
        var ceilingAtTarget = HorizonProbe.Target(Current);
        var firstMonth = new DateOnly(2026, 7, 1);

        var shouldProbe = HorizonProbe.ShouldProbe(Current, firstMonth, ceilingAtTarget, targetLastFetchedLocalDate: null, onDemand: true);

        Assert.False(shouldProbe);
    }
}
