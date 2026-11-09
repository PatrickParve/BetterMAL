using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Ranking;
using AnimeTracker.Api.Services.Ranking.ThisOrThat;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Services.Ranking;

// add-this-or-that-ranking task 3.4: the service on InMemory, over the real ranking
// service and repositories. In these tests a LOWER id is the better anime, so a
// truthful pick takes the lower id. What InMemory cannot prove (the arrays, the token
// on a real database, the atomic save) is task 3.6, on Postgres.
public class ThisOrThatServiceTests
{
    // ---- fixtures ----

    /// <summary>One InMemory database with a context and a service over it. A second
    /// context on the same name stands in for another request, tab or restart.</summary>
    private sealed class World : IDisposable
    {
        public World(int randomSeed = 1)
        {
            Db = Open();
            Ranking = RankingOver(Db);
            Service = new ThisOrThatService(Ranking, Db, new Random(randomSeed));
        }

        public string Name { get; } = Guid.NewGuid().ToString();
        public AnimeTrackerDbContext Db { get; }
        public AnimeRankingService Ranking { get; }
        public ThisOrThatService Service { get; }

        public AnimeTrackerDbContext Open() =>
            new(new DbContextOptionsBuilder<AnimeTrackerDbContext>().UseInMemoryDatabase(Name).Options);

        public ThisOrThatService ServiceOver(AnimeTrackerDbContext db, int randomSeed = 1) =>
            new(RankingOver(db), db, new Random(randomSeed));

        /// <summary>The row as the database holds it, read through a context of its own.</summary>
        public async Task<ThisOrThatRun?> StoredAsync(int score)
        {
            using var other = Open();
            return await other.ThisOrThatRuns.AsNoTracking().SingleOrDefaultAsync(r => r.Score == score);
        }

        public async Task<List<int>> OrderOfAsync(int score) =>
            (await Ranking.GetTierAsync(score, "all"))!.Members.Select(m => m.AnimeId).ToList();

        public async Task SeedAsync(int score, params int[] ids)
        {
            foreach (var id in ids)
                AddAnime(Db, id, score);
            await Db.SaveChangesAsync();
        }

        public async Task SetEntryAsync(int animeId, int? score = null, WatchStatus? status = null)
        {
            var entry = await Db.UserAnimeEntries.SingleAsync(e => e.AnimeId == animeId);
            if (score is { } s)
                entry.MyScore = s;
            if (status is { } st)
                entry.Status = st;
            await Db.SaveChangesAsync();
        }

        public void Dispose() => Db.Dispose();

        private static AnimeRankingService RankingOver(AnimeTrackerDbContext db) =>
            new(new UserAnimeEntryRepository(db), new TopAnimeSelectionRepository(db));
    }

    private static void AddAnime(
        AnimeTrackerDbContext db, int id, int? score, WatchStatus status = WatchStatus.Completed,
        string mediaType = "tv", string airing = "finished_airing")
    {
        var anime = new AnimeMetadata
        {
            Id = id,
            Title = $"Anime {id}",
            EnglishTitle = $"English {id}",
            PictureUrl = $"https://img/{id}.jpg",
            MediaType = mediaType,
            AiringStatus = airing,
            AiredFrom = new DateOnly(2000 + id % 20, 4, 1),
        };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = id, Anime = anime, Status = status, MyScore = score });
    }

    private static int Better(ThisOrThatPairDto pair) => Math.Min(pair.Left.AnimeId, pair.Right.AnimeId);

    private static Task<ThisOrThatScoreState> PickAsync(
        ThisOrThatService service, int score, ThisOrThatScoreState state, Func<ThisOrThatPairDto, int?>? decide = null)
    {
        var pair = state.Run!.Pair!;
        return service.PickAsync(score, pair.Left.AnimeId, pair.Right.AnimeId, decide is null ? Better(pair) : decide(pair));
    }

    private static async Task<ThisOrThatScoreState> PicksAsync(
        ThisOrThatService service, int score, ThisOrThatScoreState state, int count)
    {
        for (var i = 0; i < count && state.Run!.Result is null; i++)
            state = await PickAsync(service, score, state);
        return state;
    }

    private static async Task<ThisOrThatScoreState> PlayOutAsync(
        ThisOrThatService service, int score, ThisOrThatScoreState state)
    {
        while (state.Run!.Result is null)
            state = await PickAsync(service, score, state);
        return state;
    }

    private static List<int> ResultIds(ThisOrThatScoreState state) =>
        state.Run!.Result!.Select(r => r.AnimeId).ToList();

    private static List<int> PairIds(ThisOrThatScoreState state) =>
        [state.Run!.Pair!.Left.AnimeId, state.Run.Pair.Right.AnimeId];

    // ---- which anime are ranked ----

    [Fact]
    public async Task OnlyTheScoresHandOrderableAnimeAreRankedAndScoresWithFewerThanTwoAreNotOffered()
    {
        using var world = new World();
        await world.SeedAsync(8, 1, 2, 3);
        AddAnime(world.Db, 4, 8, status: WatchStatus.Dropped);
        AddAnime(world.Db, 5, 8, mediaType: "music");
        AddAnime(world.Db, 6, 8, status: WatchStatus.PlanToWatch);
        AddAnime(world.Db, 7, 8, airing: "not_yet_aired");
        AddAnime(world.Db, 8, 9);
        AddAnime(world.Db, 20, 7);
        AddAnime(world.Db, 21, 7);
        await world.Db.SaveChangesAsync();

        var scores = await world.Service.GetScoresAsync();
        var eights = await world.Service.GetScoreAsync(8);

        Assert.Equal([8, 7], scores.Scores.Select(s => s.Score));
        Assert.Equal([3, 2], scores.Scores.Select(s => s.Count));
        Assert.Equal([3, 1], scores.Scores.Select(s => s.MaxPicks));
        Assert.Equal(3, eights!.Count);
        Assert.Equal(3, eights.MaxPicks);
        Assert.Null(await world.Service.GetScoreAsync(9));
        Assert.Null(await world.Service.GetScoreAsync(3));
        Assert.Null(await world.Service.GetScoreAsync(11));
    }

    [Fact]
    public async Task StartRefusesAScoreWithFewerThanTwoAnime()
    {
        using var world = new World();
        await world.SeedAsync(9, 1);

        await Assert.ThrowsAsync<ThisOrThatNotEnoughAnimeException>(() => world.Service.StartAsync(9, null));
        await Assert.ThrowsAsync<ThisOrThatNotEnoughAnimeException>(() => world.Service.StartAsync(3, null));
        Assert.Null(await world.StoredAsync(9));
    }

    [Fact]
    public async Task AScoreWithNoRankingReadsWithNoRunAndTheMostPicksItCanTake()
    {
        using var world = new World();
        await world.SeedAsync(8, Enumerable.Range(1, 12).ToArray());

        var state = await world.Service.GetScoreAsync(8);

        Assert.Equal(new[] { 8, 12, 33 }, new[] { state!.Score, state.Count, state.MaxPicks });
        Assert.Null(state.Run);
    }

    // ---- start, pick, undo, save, discard ----

    [Fact]
    public async Task StartShowsTheFirstPairWithTheFirstAnimePlacedAndNoPickMade()
    {
        using var world = new World();
        await world.SeedAsync(8, 1, 2, 3, 4);

        var state = await world.Service.StartAsync(8, null);

        var run = state.Run!;
        Assert.Equal(1, run.Version);
        Assert.Equal(1, run.Placed);
        Assert.Equal(4, run.Total);
        Assert.Equal(0, run.PicksMade);
        Assert.Equal(ThisOrThatReplay.MaxPicks(4), run.PicksLeftAtMost);
        Assert.False(run.CanUndo);
        Assert.Null(run.Result);
        Assert.NotEqual(run.Pair!.Left.AnimeId, run.Pair.Right.AnimeId);
        Assert.Equal([1, 2, 3, 4], (await world.StoredAsync(8))!.QueueIds.Order());
    }

    [Fact]
    public async Task ACardCarriesTheFieldsThePageShows()
    {
        using var world = new World();
        await world.SeedAsync(8, 1, 2);

        var pair = (await world.Service.StartAsync(8, null)).Run!.Pair!;

        var card = pair.Left;
        Assert.Equal($"Anime {card.AnimeId}", card.Title);
        Assert.Equal($"English {card.AnimeId}", card.EnglishTitle);
        Assert.Equal($"https://img/{card.AnimeId}.jpg", card.PictureUrl);
        Assert.Equal("tv", card.MediaType);
        Assert.Equal(2000 + card.AnimeId % 20, card.Year);
    }

    [Fact]
    public async Task EveryPickBumpsTheVersionAndTheFinishedRankingListsEveryAnimeInTheOrderPicked()
    {
        using var world = new World();
        await world.SeedAsync(8, 1, 2, 3, 4, 5, 6);
        var state = await world.Service.StartAsync(8, null);

        var picks = 0;
        while (state.Run!.Result is null)
        {
            var before = state.Run;
            state = await PickAsync(world.Service, 8, state);
            picks++;

            Assert.Equal(before.Version + 1, state.Run!.Version);
            Assert.Equal(picks, state.Run.PicksMade);
            Assert.True(state.Run.CanUndo);
        }

        var run = state.Run!;
        Assert.Equal([1, 2, 3, 4, 5, 6], ResultIds(state));
        Assert.Equal([1, 2, 3, 4, 5, 6], run.Result!.Select(r => r.Position));
        Assert.Equal(6, run.Placed);
        Assert.Equal(6, run.Total);
        Assert.Equal(0, run.PicksLeftAtMost);
        Assert.Null(run.Pair);
        Assert.True(picks <= ThisOrThatReplay.MaxPicks(6));
    }

    [Fact]
    public async Task ThePicksLeftNeverRiseAndStartAtTheMostPicksTheScoreCanTake()
    {
        using var world = new World();
        await world.SeedAsync(8, Enumerable.Range(1, 12).ToArray());
        var state = await world.Service.StartAsync(8, null);
        Assert.Equal(33, state.Run!.PicksLeftAtMost);

        var previous = state.Run.PicksLeftAtMost;
        while (state.Run!.Result is null)
        {
            state = await PickAsync(world.Service, 8, state);
            Assert.True(state.Run!.PicksLeftAtMost <= previous);
            previous = state.Run.PicksLeftAtMost;
        }
    }

    [Fact]
    public async Task CantDecideIsStoredAsAZeroAndPlacesTheAnimeDirectlyBelowTheOneItWasShownWith()
    {
        using var world = new World();
        await world.SeedAsync(8, 1, 2, 3);
        var state = await world.Service.StartAsync(8, null);

        // The first pair is the second queued anime against the first.
        state = await PickAsync(world.Service, 8, state, _ => null);

        var stored = await world.StoredAsync(8);
        Assert.Equal([0], stored!.AnswerOutcomes);
        var queue = stored.QueueIds;
        // The third anime then loses every pair, so it ends last and the first two stay
        // as Can't decide left them: the second directly below the first.
        while (state.Run!.Result is null)
            state = await PickAsync(world.Service, 8, state, p => p.Left.AnimeId == queue[2] ? p.Right.AnimeId : p.Left.AnimeId);
        Assert.Equal([queue[0], queue[1], queue[2]], ResultIds(state));
    }

    [Fact]
    public async Task APickStoresTheOutcomeFromTheSideOfTheAnimeBeingPlaced()
    {
        using var world = new World();
        await world.SeedAsync(8, 1, 2, 3);
        var state = await world.Service.StartAsync(8, null);
        var pair = state.Run!.Pair!;

        await world.Service.PickAsync(8, pair.Left.AnimeId, pair.Right.AnimeId, pair.Left.AnimeId);

        var stored = await world.StoredAsync(8);
        var challenger = stored!.AnswerChallengerIds.Single();
        Assert.Equal(pair.Left.AnimeId == challenger ? 1 : -1, stored.AnswerOutcomes.Single());
        Assert.Equal(pair.Left.AnimeId == challenger ? pair.Right.AnimeId : pair.Left.AnimeId, stored.AnswerOtherIds.Single());
    }

    [Fact]
    public async Task AWinnerThatIsNeitherCardIsRefusedAndChangesNothing()
    {
        using var world = new World();
        await world.SeedAsync(8, 1, 2, 3);
        var pair = (await world.Service.StartAsync(8, null)).Run!.Pair!;

        await Assert.ThrowsAsync<ThisOrThatWinnerNotInPairException>(
            () => world.Service.PickAsync(8, pair.Left.AnimeId, pair.Right.AnimeId, 999));

        var stored = await world.StoredAsync(8);
        Assert.Equal(1, stored!.Version);
        Assert.Empty(stored.AnswerOutcomes);
    }

    [Fact]
    public async Task UndoTakesBackTheLastPickAndShowsThePairItWasMadeOn()
    {
        using var world = new World();
        await world.SeedAsync(8, 1, 2, 3, 4, 5);
        var state = await world.Service.StartAsync(8, null);

        for (var picks = 1; picks <= 6; picks++)
        {
            var before = state;
            state = await PickAsync(world.Service, 8, state);
            var undone = await world.Service.UndoAsync(8, state.Run!.Version);

            Assert.Equal(before.Run!.PicksMade, undone.Run!.PicksMade);
            Assert.Equal(PairIds(before), PairIds(undone));
            Assert.Equal(before.Run.CanUndo, undone.Run.CanUndo);
            Assert.Equal(state.Run.Version + 1, undone.Run.Version);

            state = await PickAsync(world.Service, 8, undone);
            if (state.Run!.Result is not null)
                break;
        }
    }

    [Fact]
    public async Task UndoOnTheResultBringsBackTheLastPair()
    {
        using var world = new World();
        await world.SeedAsync(8, 1, 2, 3);
        var finished = await PlayOutAsync(world.Service, 8, await world.Service.StartAsync(8, null));
        Assert.NotNull(finished.Run!.Result);

        var undone = await world.Service.UndoAsync(8, finished.Run.Version);

        Assert.Null(undone.Run!.Result);
        Assert.NotNull(undone.Run.Pair);
        Assert.Equal(finished.Run.PicksMade - 1, undone.Run.PicksMade);
    }

    [Fact]
    public async Task UndoWithNothingToTakeBackOrAStaleVersionIsRefused()
    {
        using var world = new World();
        await world.SeedAsync(8, 1, 2, 3);
        var started = await world.Service.StartAsync(8, null);
        await Assert.ThrowsAsync<ThisOrThatStaleException>(() => world.Service.UndoAsync(8, started.Run!.Version));

        var picked = await PickAsync(world.Service, 8, started);
        await Assert.ThrowsAsync<ThisOrThatStaleException>(() => world.Service.UndoAsync(8, started.Run!.Version));

        Assert.Single((await world.StoredAsync(8))!.AnswerOutcomes);
        Assert.Equal(picked.Run!.Version, (await world.StoredAsync(8))!.Version);
    }

    [Fact]
    public async Task StartOverReplacesTheRunAndTheOldPicksNoLongerCount()
    {
        using var world = new World();
        await world.SeedAsync(8, 1, 2, 3, 4, 5);
        var state = await PicksAsync(world.Service, 8, await world.Service.StartAsync(8, null), 3);
        Assert.Equal(3, state.Run!.PicksMade);

        var again = await world.Service.StartAsync(8, state.Run.Version);

        Assert.Equal(state.Run.Version + 1, again.Run!.Version);
        Assert.Equal(0, again.Run.PicksMade);
        Assert.Equal(1, again.Run.Placed);
        var stored = await world.StoredAsync(8);
        Assert.Empty(stored!.AnswerChallengerIds);
        Assert.Empty(stored.AnswerOtherIds);
        Assert.Equal([1, 2, 3, 4, 5], stored.QueueIds.Order());
        Assert.Equal(1, await world.Db.ThisOrThatRuns.CountAsync());
    }

    [Fact]
    public async Task ASecondStartWithNoVersionOrAWrongOneIsRefusedAndChangesNothing()
    {
        using var world = new World();
        await world.SeedAsync(8, 1, 2, 3);
        var state = await PicksAsync(world.Service, 8, await world.Service.StartAsync(8, null), 1);
        var before = await world.StoredAsync(8);

        await Assert.ThrowsAsync<ThisOrThatStaleException>(() => world.Service.StartAsync(8, null));
        await Assert.ThrowsAsync<ThisOrThatStaleException>(() => world.Service.StartAsync(8, state.Run!.Version + 5));
        await Assert.ThrowsAsync<ThisOrThatStaleException>(() => world.Service.StartAsync(8, state.Run!.Version - 1));

        var after = await world.StoredAsync(8);
        Assert.Equal(before!.Version, after!.Version);
        Assert.Equal(before.AnswerOutcomes, after.AnswerOutcomes);
        Assert.Equal(before.QueueIds, after.QueueIds);
        Assert.Equal(before.Seed, after.Seed);
    }

    [Fact]
    public async Task ARowWhoseAnswerListsDifferInLengthReadsAsNoRankingAndStartReplacesIt()
    {
        using var world = new World();
        await world.SeedAsync(8, 1, 2, 3);
        await world.Service.StartAsync(8, null);
        var row = await world.Db.ThisOrThatRuns.SingleAsync();
        row.AnswerChallengerIds = [2];
        await world.Db.SaveChangesAsync();

        Assert.Null((await world.Service.GetScoreAsync(8))!.Run);
        Assert.False((await world.Service.GetScoresAsync()).Scores.Single().InProgress);
        await Assert.ThrowsAsync<ThisOrThatRunNotFoundException>(() => world.Service.UndoAsync(8, row.Version));

        var started = await world.Service.StartAsync(8, null);

        Assert.NotNull(started.Run!.Pair);
        Assert.Equal(0, started.Run.PicksMade);
    }

    // ---- stale and double requests ----

    [Fact]
    public async Task ADoubleSubmitCountsOnce()
    {
        using var world = new World();
        await world.SeedAsync(8, 1, 2, 3, 4);
        var pair = (await world.Service.StartAsync(8, null)).Run!.Pair!;

        await world.Service.PickAsync(8, pair.Left.AnimeId, pair.Right.AnimeId, Better(pair));
        await Assert.ThrowsAsync<ThisOrThatStaleException>(
            () => world.Service.PickAsync(8, pair.Left.AnimeId, pair.Right.AnimeId, Better(pair)));

        Assert.Single((await world.StoredAsync(8))!.AnswerOutcomes);
    }

    [Fact]
    public async Task APickOnAPairThatIsNotTheCurrentOneIsRefusedWhicheverCardsItNames()
    {
        using var world = new World();
        await world.SeedAsync(8, 1, 2, 3, 4);
        var pair = (await world.Service.StartAsync(8, null)).Run!.Pair!;

        // Swapped sides, one real card and one not, and two cards nobody is showing.
        await Assert.ThrowsAsync<ThisOrThatStaleException>(
            () => world.Service.PickAsync(8, pair.Right.AnimeId, pair.Left.AnimeId, pair.Left.AnimeId));
        await Assert.ThrowsAsync<ThisOrThatStaleException>(
            () => world.Service.PickAsync(8, pair.Left.AnimeId, 999, pair.Left.AnimeId));
        await Assert.ThrowsAsync<ThisOrThatStaleException>(() => world.Service.PickAsync(8, 3, 4, 3));

        Assert.Empty((await world.StoredAsync(8))!.AnswerOutcomes);
    }

    [Fact]
    public async Task APickOnAFinishedRankingIsRefused()
    {
        using var world = new World();
        await world.SeedAsync(8, 1, 2);
        var started = await world.Service.StartAsync(8, null);
        var pair = started.Run!.Pair!;
        await world.Service.PickAsync(8, pair.Left.AnimeId, pair.Right.AnimeId, Better(pair));

        await Assert.ThrowsAsync<ThisOrThatStaleException>(
            () => world.Service.PickAsync(8, pair.Left.AnimeId, pair.Right.AnimeId, Better(pair)));
    }

    [Fact]
    public async Task APickAfterAnotherTabPickedAndUndidBackToTheSamePairIsAccepted()
    {
        using var world = new World();
        await world.SeedAsync(8, 1, 2, 3, 4);
        var started = await world.Service.StartAsync(8, null);
        var pair = started.Run!.Pair!;

        var otherTabPicked = await world.Service.PickAsync(8, pair.Left.AnimeId, pair.Right.AnimeId, Better(pair));
        await world.Service.UndoAsync(8, otherTabPicked.Run!.Version);
        var thisTab = await world.Service.PickAsync(8, pair.Left.AnimeId, pair.Right.AnimeId, Better(pair));

        Assert.Equal(1, thisTab.Run!.PicksMade);
    }

    [Fact]
    public async Task APickWithNoRankingInProgressIsNotFound()
    {
        using var world = new World();
        await world.SeedAsync(8, 1, 2, 3);

        await Assert.ThrowsAsync<ThisOrThatRunNotFoundException>(() => world.Service.PickAsync(8, 1, 2, 1));
        await Assert.ThrowsAsync<ThisOrThatRunNotFoundException>(() => world.Service.UndoAsync(8, 1));
        await Assert.ThrowsAsync<ThisOrThatRunNotFoundException>(() => world.Service.SaveAsync(8, 1));
        await Assert.ThrowsAsync<ThisOrThatRunNotFoundException>(() => world.Service.DiscardAsync(8, 1));
    }

    [Fact]
    public async Task TwoPicksThatReadTheSameRunAtOnceCountOnceAndTheLoserFailsOnTheToken()
    {
        using var world = new World();
        await world.SeedAsync(8, 1, 2, 3, 4);
        // Tab one started the ranking, so its context still holds the run at version 1.
        var started = await world.Service.StartAsync(8, null);
        var pair = started.Run!.Pair!;
        using var tabTwo = world.Open();
        var tabTwoService = world.ServiceOver(tabTwo);

        // Tab two's pick lands first. Tab one then reads its own stale copy, finds the
        // right pair, and is stopped only by the version on the row.
        await tabTwoService.PickAsync(8, pair.Left.AnimeId, pair.Right.AnimeId, Better(pair));
        await Assert.ThrowsAsync<ThisOrThatStaleException>(
            () => world.Service.PickAsync(8, pair.Left.AnimeId, pair.Right.AnimeId, Better(pair)));

        var stored = await world.StoredAsync(8);
        Assert.Single(stored!.AnswerOutcomes);
        Assert.Equal(2, stored.Version);
    }

    // ---- save and discard ----

    [Fact]
    public async Task SaveWritesExactlyTheResultAsTheScoresHandOrderedOrderAndLeavesNoRun()
    {
        using var world = new World();
        await world.SeedAsync(8, 1, 2, 3, 4, 5, 6);
        await world.Ranking.ApplyTierOrderAsync([new AnimeRankingTierOrderRequest(8, [6, 5, 4, 3, 2, 1])]);
        var finished = await PlayOutAsync(world.Service, 8, await world.Service.StartAsync(8, null));
        Assert.Equal([6, 5, 4, 3, 2, 1], await world.OrderOfAsync(8));

        await world.Service.SaveAsync(8, finished.Run!.Version);

        Assert.Equal(ResultIds(finished), await world.OrderOfAsync(8));
        Assert.Equal([1, 2, 3, 4, 5, 6], await world.OrderOfAsync(8));
        Assert.Equal(6, await world.Db.TopAnimeSelections.CountAsync());
        Assert.Empty(await world.Db.ThisOrThatRuns.ToListAsync());
        Assert.Null((await world.Service.GetScoreAsync(8))!.Run);
        Assert.False((await world.Service.GetScoresAsync()).Scores.Single().InProgress);
    }

    [Fact]
    public async Task SaveLeavesTheOtherScoresOrderAlone()
    {
        using var world = new World();
        await world.SeedAsync(8, 1, 2, 3);
        await world.SeedAsync(7, 11, 12, 13);
        await world.Ranking.ApplyTierOrderAsync([new AnimeRankingTierOrderRequest(7, [13, 11, 12])]);
        var finished = await PlayOutAsync(world.Service, 8, await world.Service.StartAsync(8, null));

        await world.Service.SaveAsync(8, finished.Run!.Version);

        Assert.Equal([13, 11, 12], await world.OrderOfAsync(7));
    }

    [Fact]
    public async Task SaveBeforeTheRankingIsFinishedOrWithAStaleVersionIsRefusedAndWritesNothing()
    {
        using var world = new World();
        await world.SeedAsync(8, 1, 2, 3, 4);
        await world.Ranking.ApplyTierOrderAsync([new AnimeRankingTierOrderRequest(8, [4, 3, 2, 1])]);
        var state = await PicksAsync(world.Service, 8, await world.Service.StartAsync(8, null), 2);
        Assert.Null(state.Run!.Result);

        await Assert.ThrowsAsync<ThisOrThatNotFinishedException>(() => world.Service.SaveAsync(8, state.Run.Version));
        await Assert.ThrowsAsync<ThisOrThatStaleException>(() => world.Service.SaveAsync(8, state.Run.Version - 1));

        Assert.Equal([4, 3, 2, 1], await world.OrderOfAsync(8));
        Assert.NotNull(await world.StoredAsync(8));
    }

    [Fact]
    public async Task ASaveWithAStaleVersionOnAFinishedRankingWritesNothing()
    {
        using var world = new World();
        await world.SeedAsync(8, 1, 2, 3);
        await world.Ranking.ApplyTierOrderAsync([new AnimeRankingTierOrderRequest(8, [3, 2, 1])]);
        var finished = await PlayOutAsync(world.Service, 8, await world.Service.StartAsync(8, null));

        await Assert.ThrowsAsync<ThisOrThatStaleException>(() => world.Service.SaveAsync(8, finished.Run!.Version - 1));

        Assert.Equal([3, 2, 1], await world.OrderOfAsync(8));
        Assert.NotNull(await world.StoredAsync(8));
    }

    [Fact]
    public async Task SavingTwiceIsRefusedTheSecondTimeBecauseTheRunIsGone()
    {
        using var world = new World();
        await world.SeedAsync(8, 1, 2, 3);
        var finished = await PlayOutAsync(world.Service, 8, await world.Service.StartAsync(8, null));
        await world.Service.SaveAsync(8, finished.Run!.Version);

        await Assert.ThrowsAsync<ThisOrThatRunNotFoundException>(() => world.Service.SaveAsync(8, finished.Run.Version));
    }

    [Fact]
    public async Task DiscardEndsTheRankingAndLeavesTheOrderAsItWas()
    {
        using var world = new World();
        await world.SeedAsync(8, 1, 2, 3, 4);
        await world.Ranking.ApplyTierOrderAsync([new AnimeRankingTierOrderRequest(8, [4, 3, 2, 1])]);
        var finished = await PlayOutAsync(world.Service, 8, await world.Service.StartAsync(8, null));

        await Assert.ThrowsAsync<ThisOrThatStaleException>(() => world.Service.DiscardAsync(8, finished.Run!.Version + 1));
        await world.Service.DiscardAsync(8, finished.Run!.Version);

        Assert.Equal([4, 3, 2, 1], await world.OrderOfAsync(8));
        Assert.Empty(await world.Db.ThisOrThatRuns.ToListAsync());
        Assert.Null((await world.Service.GetScoreAsync(8))!.Run);
    }

    [Fact]
    public async Task PickingNeverChangesTheStoredRankingUntilSave()
    {
        using var world = new World();
        await world.SeedAsync(8, 1, 2, 3, 4, 5);
        await world.Ranking.ApplyTierOrderAsync([new AnimeRankingTierOrderRequest(8, [5, 4, 3, 2, 1])]);
        var modifiedAt = await world.Db.RankingStates.Select(s => s.ModifiedAt).SingleAsync();

        await PlayOutAsync(world.Service, 8, await world.Service.StartAsync(8, null));

        Assert.Equal([5, 4, 3, 2, 1], await world.OrderOfAsync(8));
        using var other = world.Open();
        Assert.Equal(modifiedAt, await other.RankingStates.Select(s => s.ModifiedAt).SingleAsync());
    }

    [Fact]
    public async Task AFinishedResultShowsHowFarEachAnimeMovedFromTheScoresCurrentOrder()
    {
        using var world = new World();
        await world.SeedAsync(8, 1, 2, 3, 4);
        await world.Ranking.ApplyTierOrderAsync([new AnimeRankingTierOrderRequest(8, [4, 3, 2, 1])]);

        var finished = await PlayOutAsync(world.Service, 8, await world.Service.StartAsync(8, null));

        Assert.Equal([1, 2, 3, 4], finished.Run!.Result!.Select(r => r.Position));
        Assert.Equal([4, 3, 2, 1], finished.Run.Result!.Select(r => r.PreviousPosition));
        Assert.Equal("English 1", finished.Run.Result![0].EnglishTitle);
        Assert.Equal("https://img/1.jpg", finished.Run.Result[0].PictureUrl);
    }

    [Fact]
    public async Task PreviousPositionFollowsAReorderMadeThroughTheRankingEditorDuringTheRanking()
    {
        using var world = new World();
        await world.SeedAsync(8, 1, 2, 3, 4);
        var started = await world.Service.StartAsync(8, null);

        // The order is [1, 2, 3, 4] when the ranking starts and [2, 4, 1, 3] by its end.
        await world.Ranking.ApplyTierOrderAsync([new AnimeRankingTierOrderRequest(8, [2, 4, 1, 3])]);
        var finished = await PlayOutAsync(world.Service, 8, started);

        Assert.Equal([1, 2, 3, 4], ResultIds(finished));
        Assert.Equal([3, 1, 4, 2], finished.Run!.Result!.Select(r => r.PreviousPosition));
    }

    // ---- the list changing under a ranking ----

    [Fact]
    public async Task AnAnimeReScoredAwayLeavesAtTheNextReadAndStaysInTheStoredQueue()
    {
        using var world = new World();
        await world.SeedAsync(8, 1, 2, 3, 4, 5, 6);
        await PicksAsync(world.Service, 8, await world.Service.StartAsync(8, null), 3);
        // The first anime in the queue is placed with no pick, so it is a placed one.
        var leaver = (await world.StoredAsync(8))!.QueueIds[0];
        await world.SetEntryAsync(leaver, score: 9);

        var read = await world.Service.GetScoreAsync(8);

        Assert.Equal(5, read!.Count);
        Assert.Equal(5, read.Run!.Total);
        Assert.DoesNotContain(leaver, PairIds(read));
        var next = await PickAsync(world.Service, 8, read);
        Assert.Contains(leaver, (await world.StoredAsync(8))!.QueueIds);
        var finished = await PlayOutAsync(world.Service, 8, next);
        Assert.DoesNotContain(leaver, ResultIds(finished));
        Assert.Equal(5, finished.Run!.Result!.Count);
    }

    [Fact]
    public async Task ADroppedAnimeAndAnAnimeRemovedFromMyListLeaveToo()
    {
        using var world = new World();
        await world.SeedAsync(8, 1, 2, 3, 4, 5, 6);
        await PicksAsync(world.Service, 8, await world.Service.StartAsync(8, null), 2);

        await world.SetEntryAsync(2, status: WatchStatus.Dropped);
        world.Db.UserAnimeEntries.Remove(await world.Db.UserAnimeEntries.SingleAsync(e => e.AnimeId == 5));
        await world.Db.SaveChangesAsync();

        var read = await world.Service.GetScoreAsync(8);
        var finished = await PlayOutAsync(world.Service, 8, read!);

        Assert.Equal(4, read!.Count);
        Assert.Equal(4, read.Run!.Total);
        Assert.Equal([1, 3, 4, 6], ResultIds(finished));
    }

    [Fact]
    public async Task ANewlyScoredAnimeJoinsAfterEveryAnimeStillWaitingAndIsFoldedInByTheNextPick()
    {
        using var world = new World();
        await world.SeedAsync(8, 1, 2, 3, 4, 5, 6);
        var state = await PicksAsync(world.Service, 8, await world.Service.StartAsync(8, null), 2);
        AddAnime(world.Db, 99, 8);
        await world.Db.SaveChangesAsync();

        var read = await world.Service.GetScoreAsync(8);

        Assert.Equal(7, read!.Count);
        Assert.Equal(7, read.Run!.Total);
        Assert.Equal(state.Run!.Placed, read.Run.Placed);
        Assert.DoesNotContain(99, (await world.StoredAsync(8))!.QueueIds);

        var next = await PickAsync(world.Service, 8, read);

        Assert.Equal(99, (await world.StoredAsync(8))!.QueueIds[^1]);
        var finished = await PlayOutAsync(world.Service, 8, next);
        Assert.Equal([1, 2, 3, 4, 5, 6, 99], ResultIds(finished));
        var challengers = (await world.StoredAsync(8))!.AnswerChallengerIds.Distinct().ToList();
        Assert.Equal(99, challengers[^1]);
    }

    [Fact]
    public async Task AnAnimeJoiningAFinishedRankingShowsItsFirstPairInsteadOfTheResult()
    {
        using var world = new World();
        await world.SeedAsync(8, 1, 2, 3);
        var finished = await PlayOutAsync(world.Service, 8, await world.Service.StartAsync(8, null));
        Assert.NotNull(finished.Run!.Result);
        AddAnime(world.Db, 99, 8);
        await world.Db.SaveChangesAsync();

        var read = await world.Service.GetScoreAsync(8);

        Assert.Null(read!.Run!.Result);
        Assert.Contains(99, PairIds(read));
        Assert.Equal(3, read.Run.Placed);
        Assert.Equal(4, read.Run.Total);
    }

    [Fact]
    public async Task AnAnimeThatLeftAndCameBackTakesBackThePlaceItsPicksGaveIt()
    {
        using var world = new World();
        await world.SeedAsync(8, 1, 2, 3, 4, 5);
        var finished = await PlayOutAsync(world.Service, 8, await world.Service.StartAsync(8, null));
        Assert.Equal([1, 2, 3, 4, 5], ResultIds(finished));

        await world.SetEntryAsync(3, score: 9);
        Assert.Equal([1, 2, 4, 5], ResultIds((await world.Service.GetScoreAsync(8))!));
        await world.SetEntryAsync(3, score: 8);

        var back = await world.Service.GetScoreAsync(8);
        Assert.Equal([1, 2, 3, 4, 5], ResultIds(back!));
    }

    [Fact]
    public async Task AScoreLeftWithOneAnimeIsNotOfferedAndItsRankingCarriesOnWhenItRefills()
    {
        using var world = new World();
        await world.SeedAsync(8, 1, 2, 3);
        var state = await PicksAsync(world.Service, 8, await world.Service.StartAsync(8, null), 1);

        await world.SetEntryAsync(2, status: WatchStatus.Dropped);
        await world.SetEntryAsync(3, status: WatchStatus.Dropped);

        Assert.Null(await world.Service.GetScoreAsync(8));
        Assert.Empty((await world.Service.GetScoresAsync()).Scores);
        await Assert.ThrowsAsync<ThisOrThatRunNotFoundException>(() => PickAsync(world.Service, 8, state));
        Assert.NotNull(await world.StoredAsync(8));

        await world.SetEntryAsync(2, status: WatchStatus.Completed);
        await world.SetEntryAsync(3, status: WatchStatus.Completed);

        var back = await world.Service.GetScoreAsync(8);
        Assert.Equal(3, back!.Count);
        Assert.Equal(1, back.Run!.PicksMade);
    }

    [Fact]
    public async Task ReadsWriteNothingEvenWhenTheListHasChanged()
    {
        using var world = new World();
        await world.SeedAsync(8, 1, 2, 3, 4, 5, 6);
        await PicksAsync(world.Service, 8, await world.Service.StartAsync(8, null), 3);
        await world.SetEntryAsync(1, score: 9);
        AddAnime(world.Db, 99, 8);
        await world.Db.SaveChangesAsync();
        var before = await world.StoredAsync(8);

        await world.Service.GetScoresAsync();
        await world.Service.GetScoreAsync(8);
        await world.Service.GetScoreAsync(8);

        var after = await world.StoredAsync(8);
        Assert.Equal(before!.Version, after!.Version);
        Assert.Equal(before.UpdatedAt, after.UpdatedAt);
        Assert.Equal(before.QueueIds, after.QueueIds);
        Assert.Equal(before.AnswerChallengerIds, after.AnswerChallengerIds);
        Assert.Equal(before.AnswerOtherIds, after.AnswerOtherIds);
        Assert.Equal(before.AnswerOutcomes, after.AnswerOutcomes);
    }

    [Fact]
    public async Task TwoScoresAreRankedIndependentlyAndSavingOneLeavesTheOtherInProgress()
    {
        using var world = new World();
        await world.SeedAsync(8, 1, 2, 3);
        await world.SeedAsync(9, 11, 12, 13);
        var eights = await world.Service.StartAsync(8, null);
        var nines = await PicksAsync(world.Service, 9, await world.Service.StartAsync(9, null), 1);

        var finished = await PlayOutAsync(world.Service, 8, eights);
        await world.Service.SaveAsync(8, finished.Run!.Version);

        var scores = (await world.Service.GetScoresAsync()).Scores;
        Assert.Equal([9, 8], scores.Select(s => s.Score));
        Assert.Equal([true, false], scores.Select(s => s.InProgress));
        Assert.Equal(nines.Run!.PicksMade, (await world.Service.GetScoreAsync(9))!.Run!.PicksMade);
    }

    // ---- the side an anime is placed from ----

    [Theory]
    [InlineData(12345, "00010001010110000100101010110011")]
    [InlineData(987654321, "00110001001110010110000001011100")]
    [InlineData(-7, "01100100001100110100100101101011")]
    public void SideIsAFixedIntegerMixOfTheSeedAndTheAnimeId(int seed, string expected)
    {
        var sides = string.Concat(Enumerable.Range(1, 32).Select(id => ThisOrThatService.Side(seed, id)));

        Assert.Equal(expected, sides);
    }

    [Fact]
    public void SidesSplitAboutEvenlyAndChangeWithTheSeed()
    {
        var left = Enumerable.Range(1, 1000).Count(id => ThisOrThatService.Side(12345, id) == 0);
        var differ = Enumerable.Range(1, 1000).Count(id => ThisOrThatService.Side(1, id) != ThisOrThatService.Side(2, id));

        Assert.InRange(left, 400, 600);
        Assert.InRange(differ, 300, 700);
    }

    [Fact]
    public async Task TheAnimeBeingPlacedKeepsItsSideForEveryPickOfItsPlacement()
    {
        using var world = new World();
        await world.SeedAsync(8, Enumerable.Range(1, 12).ToArray());
        var state = await world.Service.StartAsync(8, null);
        var seed = (await world.StoredAsync(8))!.Seed;
        var picksOfTheSameAnimeInARow = 0;
        int? previousChallenger = null;

        while (state.Run!.Result is null)
        {
            var pair = state.Run.Pair!;
            state = await PickAsync(world.Service, 8, state);
            var challenger = (await world.StoredAsync(8))!.AnswerChallengerIds[^1];

            var onTheLeft = pair.Left.AnimeId == challenger;
            Assert.Equal(ThisOrThatService.Side(seed, challenger) == 0, onTheLeft);
            if (challenger == previousChallenger)
                picksOfTheSameAnimeInARow++;
            previousChallenger = challenger;
        }

        Assert.True(picksOfTheSameAnimeInARow > 0, "a placement should have taken more than one pick");
    }

    [Fact]
    public async Task TheOtherCardChangesFromPickToPickWithinAPlacement()
    {
        using var world = new World();
        await world.SeedAsync(8, Enumerable.Range(1, 12).ToArray());
        var state = await world.Service.StartAsync(8, null);

        while (state.Run!.Result is null)
        {
            state = await PickAsync(world.Service, 8, state);
            var stored = (await world.StoredAsync(8))!;
            var count = stored.AnswerChallengerIds.Count;
            if (count >= 2 && stored.AnswerChallengerIds[count - 1] == stored.AnswerChallengerIds[count - 2])
                Assert.NotEqual(stored.AnswerOtherIds[count - 1], stored.AnswerOtherIds[count - 2]);
        }
    }

    [Fact]
    public async Task AReloadAndASecondServiceInstanceShowTheSameSides()
    {
        using var world = new World();
        await world.SeedAsync(8, 1, 2, 3, 4, 5);
        var state = await PicksAsync(world.Service, 8, await world.Service.StartAsync(8, null), 2);
        using var second = world.Open();

        var reload = await world.ServiceOver(second, randomSeed: 99).GetScoreAsync(8);

        Assert.Equal(PairIds(state), PairIds(reload!));
        Assert.Equal(state.Run!.Pair!.Left.AnimeId, reload!.Run!.Pair!.Left.AnimeId);
    }

    // ---- restart ----

    [Fact]
    public async Task TheRankingSurvivesAFreshContextAndKeepsEveryPick()
    {
        using var world = new World();
        await world.SeedAsync(8, 1, 2, 3, 4, 5, 6);
        var state = await PicksAsync(world.Service, 8, await world.Service.StartAsync(8, null), 5);

        using var fresh = world.Open();
        var after = await world.ServiceOver(fresh, randomSeed: 7).GetScoreAsync(8);

        var run = after!.Run!;
        Assert.Equal(state.Run!.Version, run.Version);
        Assert.Equal(state.Run.PicksMade, run.PicksMade);
        Assert.Equal(state.Run.Placed, run.Placed);
        Assert.Equal(state.Run.Total, run.Total);
        Assert.Equal(state.Run.PicksLeftAtMost, run.PicksLeftAtMost);
        Assert.Equal(PairIds(state), PairIds(after));
        Assert.Equal(state.Run.Pair!.Left.AnimeId, run.Pair!.Left.AnimeId);
    }

    // ---- what the next pair's pictures are ----

    [Fact]
    public async Task TheUpcomingPicturesAreTheAnimeTheNextPairCouldHoldMinusTheTwoOnScreen()
    {
        using var world = new World();
        await world.SeedAsync(8, Enumerable.Range(1, 20).ToArray());
        var state = await PicksAsync(world.Service, 8, await world.Service.StartAsync(8, null), 9);
        var onScreen = PairIds(state);

        var upcoming = state.Run!.UpcomingPictureUrls;

        Assert.InRange(upcoming.Count, 1, 4);
        Assert.Equal(upcoming.Count, upcoming.Distinct().Count());
        Assert.All(upcoming, url => Assert.StartsWith("https://img/", url));
        foreach (var id in onScreen)
            Assert.DoesNotContain($"https://img/{id}.jpg", upcoming);
    }

    [Fact]
    public async Task AFinishedRankingHasNoUpcomingPictures()
    {
        using var world = new World();
        await world.SeedAsync(8, 1, 2, 3);

        var finished = await PlayOutAsync(world.Service, 8, await world.Service.StartAsync(8, null));

        Assert.Empty(finished.Run!.UpcomingPictureUrls);
    }

    [Fact]
    public async Task AnAnimeRatedRxIsRankedLikeAnyOtherSinceTheServiceHasNoNsfwSetting()
    {
        using var world = new World();
        await world.SeedAsync(8, 1, 2);
        var rx = await world.Db.AnimeMetadata.SingleAsync(a => a.Id == 2);
        rx.Rating = "rx";
        await world.Db.SaveChangesAsync();

        var state = await world.Service.StartAsync(8, null);

        Assert.Equal(2, state.Count);
        Assert.Equal([1, 2], PairIds(state).Order());
    }
}
