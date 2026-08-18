using AnimeTracker.Api.Services.Season;

namespace AnimeTracker.Api.Tests.Services.Season;

// SeasonHorizon.Resolve (design.md decision 3, tasks.md 3.2): a pure
// function, so these tests drive it directly with hand-built
// "notListedToday" predicates rather than a DB.
public class SeasonHorizonTests
{
    private static readonly (int Year, string Season) Current = (2026, "summer");

    private static bool Never((int Year, string Season) _) => false;

    [Fact]
    public void DefaultCeilingIsCurrentPlusTwo()
    {
        var ceiling = SeasonHorizon.Resolve(Current, latestCachedSeason: null, Never);

        Assert.Equal((2027, "winter"), ceiling);
    }

    [Fact]
    public void CeilingExtendsToACachedSeasonPastTheWindow()
    {
        var latestCached = (2027, "fall"); // current + 5, well past the +2 default

        var ceiling = SeasonHorizon.Resolve(Current, latestCached, Never);

        Assert.Equal(latestCached, ceiling);
    }

    [Fact]
    public void CeilingRetreatsPastASeason404dToday()
    {
        // current+2 (2027 winter) was 404'd today; current+1 (2026 fall) was not.
        bool NotListedToday((int Year, string Season) point) => point == (2027, "winter");

        var ceiling = SeasonHorizon.Resolve(Current, latestCachedSeason: null, NotListedToday);

        Assert.Equal((2026, "fall"), ceiling);
    }

    [Fact]
    public void ARunOfConsecutive404sRetreatsPastAllOfThem()
    {
        // Both current+1 and current+2 were 404'd today — the ceiling falls
        // all the way back to the current season.
        bool NotListedToday((int Year, string Season) point) => point == (2026, "fall") || point == (2027, "winter");

        var ceiling = SeasonHorizon.Resolve(Current, latestCachedSeason: null, NotListedToday);

        Assert.Equal(Current, ceiling);
    }

    [Fact]
    public void A404DatedToAnEarlierLocalDayDoesNotConstrainIt()
    {
        // The predicate a caller passes is already scoped to "today" (see
        // SeasonBrowseService.GetBoundsAsync's NotListedToday, which checks
        // the fetch log's date against the local date before returning
        // true) — from the resolver's own point of view, a stale mark never
        // reports true, so it behaves exactly like "nothing is known".
        var ceiling = SeasonHorizon.Resolve(Current, latestCachedSeason: null, Never);

        Assert.Equal((2027, "winter"), ceiling);
    }

    [Fact]
    public void CeilingNeverFallsBelowTheCurrentSeason()
    {
        bool AlwaysNotListedToday((int Year, string Season) _) => true;

        var ceiling = SeasonHorizon.Resolve(Current, latestCachedSeason: null, AlwaysNotListedToday);

        Assert.Equal(Current, ceiling);
    }
}
