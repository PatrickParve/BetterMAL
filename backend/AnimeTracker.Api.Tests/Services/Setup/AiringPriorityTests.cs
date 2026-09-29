using AnimeTracker.Api.Services.Setup;

namespace AnimeTracker.Api.Tests.Services.Setup;

// first-run-setup, "Airing dates are fetched for every list anime, most useful first",
// and design D10. Seasons are the app's viewing seasons: winter Jan-Mar, spring
// Apr-Jun, summer Jul-Sep, fall Oct-Dec.
public class AiringPriorityTests
{
    private static readonly DateOnly SummerDay = new(2026, 9, 29); // summer 2026: last season is spring, next is fall

    [Theory]
    [InlineData("currently_airing", "2026-07-05", 1)]
    [InlineData("currently_airing", "2024-01-10", 1)] // a long runner still airing
    [InlineData("currently_airing", null, 1)]
    [InlineData("not_yet_aired", "2026-08-01", 2)]    // this season
    [InlineData("not_yet_aired", "2026-10-05", 2)]    // next season
    [InlineData("not_yet_aired", "2027-01-10", 4)]    // two seasons ahead
    [InlineData("not_yet_aired", null, 4)]            // no start date: can't be said to start this or next season
    [InlineData("finished_airing", "2026-04-10", 3)]  // started last season
    [InlineData("finished_airing", "2026-07-01", 4)]  // started this season, but not airing and not upcoming
    [InlineData("finished_airing", "2025-04-10", 4)]  // a year ago
    [InlineData("finished_airing", null, 4)]
    [InlineData(null, "2026-04-10", 3)]
    [InlineData(null, null, 4)]
    public void AnAnimeFallsInTheTierItsStatusAndStartSeasonPick(string? status, string? airedFrom, int tier)
    {
        var start = airedFrom is null ? (DateOnly?)null : DateOnly.Parse(airedFrom);

        Assert.Equal(tier, AiringPriority.TierOf(status, start, SummerDay));
    }

    [Theory]
    [InlineData("2025-11-20", 3)] // winter's last season is fall of the year before
    [InlineData("2026-04-02", 2)] // ...and its next is spring
    public void SeasonsWrapAcrossTheYearBoundary(string airedFrom, int tier)
    {
        var winterDay = new DateOnly(2026, 1, 15);

        // The upcoming show is spring 2026; the finished one started in fall 2025.
        var status = airedFrom == "2025-11-20" ? "finished_airing" : "not_yet_aired";
        Assert.Equal(tier, AiringPriority.TierOf(status, DateOnly.Parse(airedFrom), winterDay));
    }

    [Fact]
    public void TiersOneToThreeAreThePrioritySet()
    {
        Assert.True(AiringPriority.IsPriority(1));
        Assert.True(AiringPriority.IsPriority(2));
        Assert.True(AiringPriority.IsPriority(3));
        Assert.False(AiringPriority.IsPriority(4));
    }
}
