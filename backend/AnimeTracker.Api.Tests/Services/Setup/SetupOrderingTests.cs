using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Setup;

namespace AnimeTracker.Api.Tests.Services.Setup;

// add-first-run-setup design D8 and D10 (spec "Fetching anime details fills in every list
// anime" and "Airing dates are fetched for every list anime, most useful first"): the order
// each queue takes its anime in.
public class SetupOrderingTests
{
    private static readonly DateOnly Today = new(2026, 9, 29); // summer 2026 (Jul-Sep): next is fall, last is spring

    private static LibraryAnime Anime(
        int id, WatchStatus status = WatchStatus.Completed, string? airing = "finished_airing", DateOnly? airedFrom = null) =>
        new(id, $"Anime {id}", status, default, null, airing, airedFrom);

    private static Dictionary<int, int> InListOrder(params int[] ids) =>
        ids.Select((id, index) => (id, index)).ToDictionary(x => x.id, x => x.index);

    // --- the details order ---

    [Fact]
    public void WatchingAndRewatchingComeFirstThenAiringThenTheListsOwnOrder()
    {
        var anime = new[]
        {
            Anime(1),                                                          // other
            Anime(2, airing: "currently_airing", status: WatchStatus.PlanToWatch),  // airing
            Anime(3, status: WatchStatus.Watching),                            // watching
            Anime(4),                                                          // other
            Anime(5, status: WatchStatus.Rewatching),                          // rewatching
            Anime(6, airing: "currently_airing", status: WatchStatus.Completed),    // airing
        };

        var order = SetupOrdering.DetailsOrder(anime, InListOrder(1, 2, 3, 4, 5, 6));

        Assert.Equal([3, 5, 2, 6, 1, 4], order);
    }

    [Fact]
    public void AWatchingAnimeThatIsAlsoAiringIsInTheFirstGroupNotTheSecond()
    {
        var order = SetupOrdering.DetailsOrder(
            [Anime(1, airing: "currently_airing"), Anime(2, WatchStatus.Watching, airing: "currently_airing")],
            InListOrder(1, 2));

        Assert.Equal([2, 1], order);
    }

    [Fact]
    public void WithoutAListOrderTheAnimeIdBreaksTheTie()
    {
        // After a restart with the login refused there has been no read, so no list order.
        var order = SetupOrdering.DetailsOrder([Anime(9), Anime(3), Anime(5)], new Dictionary<int, int>());

        Assert.Equal([3, 5, 9], order);
    }

    [Fact]
    public void AnAnimeMissingFromTheListOrderComesAfterTheOnesInIt()
    {
        var order = SetupOrdering.DetailsOrder([Anime(1), Anime(2), Anime(3)], InListOrder(3, 2));

        Assert.Equal([3, 2, 1], order);
    }

    // --- the airing order ---

    [Fact]
    public void AiringGoesByTierAndTheWatchedAiringShowsLeadTheFirst()
    {
        var anime = new[]
        {
            Anime(1),                                                                       // tier 4: long finished
            Anime(2, airing: "currently_airing", status: WatchStatus.PlanToWatch),           // tier 1
            Anime(3, airing: "not_yet_aired", airedFrom: new DateOnly(2026, 10, 5)),         // tier 2: next season (fall)
            Anime(4, airing: "finished_airing", airedFrom: new DateOnly(2026, 5, 1)),        // tier 3: last season (spring)
            Anime(5, airing: "currently_airing", status: WatchStatus.Watching),              // tier 1, watched
            Anime(6, airing: "not_yet_aired", airedFrom: new DateOnly(2026, 8, 20)),         // tier 2: this season (summer)
            Anime(7, airing: "currently_airing", status: WatchStatus.Rewatching),            // tier 1, watched
        };

        var order = SetupOrdering.AiringOrder(anime, InListOrder(1, 2, 3, 4, 5, 6, 7), Today);

        // Tier 1: 5 and 7 (watched) before 2; then tier 2 in list order (3, 6); then tier 3 (4); then the rest (1).
        Assert.Equal([5, 7, 2, 3, 6, 4, 1], order);
    }

    [Fact]
    public void WithinATierTheListOrderDecides_AndTheWatchedBoostIsOnlyForTheFirstTier()
    {
        var anime = new[]
        {
            Anime(1, airing: "not_yet_aired", airedFrom: new DateOnly(2026, 10, 1), status: WatchStatus.PlanToWatch),
            Anime(2, airing: "not_yet_aired", airedFrom: new DateOnly(2026, 10, 2), status: WatchStatus.Watching),
        };

        Assert.Equal([1, 2], SetupOrdering.AiringOrder(anime, InListOrder(1, 2), Today));
        Assert.Equal([2, 1], SetupOrdering.AiringOrder(anime, InListOrder(2, 1), Today));
    }

    [Fact]
    public void AnUpcomingAnimeWithNoStartDateIsInTheLastTier()
    {
        var anime = new[]
        {
            Anime(1, airing: "not_yet_aired", airedFrom: null),
            Anime(2, airing: "not_yet_aired", airedFrom: new DateOnly(2026, 11, 1)), // next season
        };

        Assert.Equal([2, 1], SetupOrdering.AiringOrder(anime, InListOrder(1, 2), Today));
    }
}

public class SetupWakeTests
{
    [Fact]
    public async Task APulseCompletesEveryWaiterThatTookNextBeforeIt()
    {
        var wake = new SetupWake();
        var first = wake.Next();
        var second = wake.Next();
        Assert.False(first.IsCompleted);

        wake.Pulse();

        await first.WaitAsync(TimeSpan.FromSeconds(5));
        await second.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task APulseBeforeAWaiterTakesNextDoesNotWakeIt()
    {
        var wake = new SetupWake();
        wake.Pulse();

        var later = wake.Next();

        Assert.False(later.IsCompleted);
        wake.Pulse();
        await later.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ATaskTakenBeforeAPulseStaysCompleteSoNoWakeUpIsLostBetweenLookingAndWaiting()
    {
        var wake = new SetupWake();
        var taken = wake.Next();
        wake.Pulse(); // lands after the worker took its task, before it went to sleep on it

        await taken.WaitAsync(TimeSpan.FromSeconds(5));
    }
}
