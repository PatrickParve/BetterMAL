using AnimeTracker.Api.Services.Ranking.ThisOrThat;

namespace AnimeTracker.Api.Tests.Services.Ranking;

// add-this-or-that-ranking task 2.3: the halving replay, a fact per scenario of the
// `this-or-that` spec, then the properties of halving_sim.py over seeded random runs.
// In the scripted facts a LOWER id is the better anime, so a truthful pick puts the
// lower id above and a queue [1, 2, 3, ...] places each new anime last.
public class ThisOrThatReplayTests
{
    private const ThisOrThatOutcome Above = ThisOrThatOutcome.Above;
    private const ThisOrThatOutcome Below = ThisOrThatOutcome.Below;
    private const ThisOrThatOutcome CantDecide = ThisOrThatOutcome.CantDecide;

    private static List<int> Ids(int from, int to) => Enumerable.Range(from, to - from + 1).ToList();

    private static HashSet<int> LiveOf(IEnumerable<int> ids) => ids.ToHashSet();

    private static ThisOrThatOutcome Truthful(ThisOrThatPair pair) =>
        pair.ChallengerId < pair.OtherId ? Above : Below;

    private static ThisOrThatReplayResult Replay(
        IReadOnlyList<int> queue, IReadOnlyList<ThisOrThatAnswer> answers, IReadOnlySet<int> live) =>
        ThisOrThatReplay.Replay(queue, answers, live);

    /// <summary>Answers pairs with <paramref name="decide"/> until the ranking
    /// finishes, or until <paramref name="stopAt"/> holds for the pair about to be
    /// asked (that pair is not answered). Every pair asked is recorded.</summary>
    private static ThisOrThatReplayResult PlayOut(
        IReadOnlyList<int> queue,
        List<ThisOrThatAnswer> answers,
        IReadOnlySet<int> live,
        Func<ThisOrThatPair, ThisOrThatOutcome> decide,
        Func<ThisOrThatPair, bool>? stopAt = null,
        List<ThisOrThatPair>? asked = null)
    {
        for (var guard = 0; guard < 100_000; guard++)
        {
            var state = Replay(queue, answers, live);
            if (state.Pair is not { } pair || (stopAt?.Invoke(pair) ?? false))
                return state;

            asked?.Add(pair);
            answers.Add(new ThisOrThatAnswer(pair.ChallengerId, pair.OtherId, decide(pair)));
        }

        throw new InvalidOperationException("The ranking never finished.");
    }

    /// <summary>Makes <paramref name="count"/> truthful picks, or fewer if the
    /// ranking finishes first.</summary>
    private static void PlayPicks(
        IReadOnlyList<int> queue, List<ThisOrThatAnswer> answers, IReadOnlySet<int> live, int count)
    {
        for (var i = 0; i < count; i++)
        {
            if (Replay(queue, answers, live).Pair is not { } pair)
                return;
            answers.Add(new ThisOrThatAnswer(pair.ChallengerId, pair.OtherId, Truthful(pair)));
        }
    }

    private static bool Asked(IEnumerable<ThisOrThatAnswer> answers, int a, int b) =>
        answers.Any(x => (x.ChallengerId == a && x.OtherId == b) || (x.ChallengerId == b && x.OtherId == a));

    // ---- the halving ----

    [Fact]
    public void PlacingAmongSevenAboveTheFourthThenBelowTheSecondThenAboveTheThirdPutsItThirdAfterThreePicks()
    {
        var queue = Ids(1, 8);
        var live = LiveOf(queue);
        var answers = new List<ThisOrThatAnswer>();

        // 1 to 7 are placed in order; anime 8 is about to be placed among them.
        var state = PlayOut(queue, answers, live, Truthful, stopAt: p => p.ChallengerId == 8);
        Assert.Equal(4, state.Pair!.OtherId);

        answers.Add(new ThisOrThatAnswer(8, 4, Above));
        Assert.Equal(2, Replay(queue, answers, live).Pair!.OtherId);

        answers.Add(new ThisOrThatAnswer(8, 2, Below));
        Assert.Equal(3, Replay(queue, answers, live).Pair!.OtherId);

        answers.Add(new ThisOrThatAnswer(8, 3, Above));
        var done = Replay(queue, answers, live);
        Assert.True(done.IsFinished);
        Assert.Equal([1, 2, 8, 3, 4, 5, 6, 7], done.Order);
        Assert.Equal(3, answers.Count(a => a.ChallengerId == 8));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 2)]
    [InlineData(4, 3)]
    [InlineData(5, 3)]
    [InlineData(6, 4)]
    [InlineData(7, 4)]
    public void AnEvenRangeTakesTheLowerPlacedMiddle(int placed, int expectedPivot)
    {
        var queue = Ids(1, placed + 1);
        var live = LiveOf(queue);
        var answers = new List<ThisOrThatAnswer>();

        var state = PlayOut(queue, answers, live, Truthful, stopAt: p => p.ChallengerId == placed + 1);

        Assert.Equal(expectedPivot, state.Pair!.OtherId);
        Assert.Equal(placed, state.Pair.Placed);
    }

    [Fact]
    public void TheFirstAnimeIsPlacedWithNoPickAndTheFirstPairIsTheSecondAgainstTheFirst()
    {
        var state = Replay([5, 3, 9], [], LiveOf([5, 3, 9]));

        Assert.False(state.IsFinished);
        Assert.Equal(new ThisOrThatPair(ChallengerId: 3, OtherId: 5, Placed: 1, Total: 3, PicksLeftAtMost: 3), state.Pair);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    [InlineData(3, 3)]
    [InlineData(12, 33)]
    [InlineData(77, 412)]
    [InlineData(149, 937)]
    public void MaxPicksIsTheSumOfTheWorstCasesOfEachPlacement(int count, int expected)
    {
        Assert.Equal(expected, ThisOrThatReplay.MaxPicks(count));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(12)]
    [InlineData(77)]
    public void TheFirstPairPromisesExactlyTheMostPicksTheScoreCanTake(int count)
    {
        var queue = Ids(1, count);

        var state = Replay(queue, [], LiveOf(queue));

        Assert.Equal(ThisOrThatReplay.MaxPicks(count), state.Pair!.PicksLeftAtMost);
        Assert.Equal(count, state.Pair.Total);
        Assert.Equal(1, state.Pair.Placed);
    }

    // ---- Can't decide ----

    [Fact]
    public void CantDecidePlacesTheChallengerDirectlyBelowThePivotAndEndsItsPlacement()
    {
        var queue = Ids(1, 7);
        var live = LiveOf(queue);
        var answers = new List<ThisOrThatAnswer>();
        var state = PlayOut(queue, answers, live, Truthful, stopAt: p => p.ChallengerId == 7);
        Assert.Equal(4, state.Pair!.OtherId);

        answers.Add(new ThisOrThatAnswer(7, 4, CantDecide));

        Assert.Equal([1, 2, 3, 4, 7, 5, 6], Replay(queue, answers, live).Order);
    }

    [Fact]
    public void CantDecideBeginsTheNextPlacementAtOnce()
    {
        var queue = Ids(1, 8);
        var live = LiveOf(queue);
        var answers = new List<ThisOrThatAnswer>();
        PlayOut(queue, answers, live, Truthful, stopAt: p => p.ChallengerId == 7);
        answers.Add(new ThisOrThatAnswer(7, 4, CantDecide));

        var next = Replay(queue, answers, live).Pair!;

        Assert.Equal(8, next.ChallengerId);
        Assert.Equal(7, next.Placed);
    }

    [Fact]
    public void CantDecideBeatsEveryOtherConstraintOfTheSameAnime()
    {
        // 7 was ranked above the 6th, then below the 3rd (so inside [3rd, 6th)),
        // then couldn't decide against the 4th: below the 4th, whatever order the
        // answers are in.
        var queue = Ids(1, 7);
        var live = LiveOf(queue);
        var answers = new List<ThisOrThatAnswer>(Enumerable.Range(2, 5).Select(c => new ThisOrThatAnswer(c, c - 1, Below)));
        ThisOrThatAnswer[] sevens =
        [
            new(7, 6, Above), new(7, 3, Below), new(7, 4, CantDecide),
        ];

        foreach (var order in new[] { sevens, [sevens[2], sevens[0], sevens[1]], [sevens[1], sevens[2], sevens[0]] })
        {
            var result = Replay(queue, [.. answers, .. order], live);
            Assert.Equal([1, 2, 3, 4, 7, 5, 6], result.Order);
        }
    }

    // ---- Undo ----

    [Fact]
    public void DroppingTheLastAnswerReturnsThePairThatAnswerWasMadeOn()
    {
        var queue = Ids(1, 12);
        var live = LiveOf(queue);
        var answers = new List<ThisOrThatAnswer>();
        var asked = new List<ThisOrThatPair>();
        PlayOut(queue, answers, live, Truthful, asked: asked);
        Assert.Equal(answers.Count, asked.Count);

        var crossedAPlacement = false;
        for (var k = 1; k <= answers.Count; k++)
        {
            var afterUndo = Replay(queue, answers.Take(k - 1).ToList(), live);

            Assert.Equal(asked[k - 1], afterUndo.Pair);
            if (k >= 2 && answers[k - 1].ChallengerId != answers[k - 2].ChallengerId)
                crossedAPlacement = true;
        }

        Assert.True(crossedAPlacement, "the run should have placed more than one anime");
    }

    [Fact]
    public void UndoOnAFinishedRankingBringsBackItsLastPair()
    {
        var queue = Ids(1, 5);
        var live = LiveOf(queue);
        var answers = new List<ThisOrThatAnswer>();
        var asked = new List<ThisOrThatPair>();
        var done = PlayOut(queue, answers, live, Truthful, asked: asked);
        Assert.True(done.IsFinished);

        var afterUndo = Replay(queue, answers.Take(answers.Count - 1).ToList(), live);

        Assert.Equal(asked[^1], afterUndo.Pair);
    }

    // ---- the list changing ----

    [Fact]
    public void AnAnimeThatLeftIsNeverInAPairOrTheResultAgain()
    {
        var queue = Ids(1, 20);
        var live = LiveOf(queue);
        var answers = new List<ThisOrThatAnswer>();
        PlayPicks(queue, answers, live, 30);
        var before = Replay(queue, answers, live).Pair!;
        var placed = queue.TakeWhile(id => id != before.ChallengerId).ToList();
        Assert.Equal(before.Placed, placed.Count);

        foreach (var leaver in placed)
        {
            var without = LiveOf(live.Where(id => id != leaver));
            var asked = new List<ThisOrThatPair>();

            var done = PlayOut(queue, [.. answers], without, Truthful, asked: asked);

            Assert.DoesNotContain(asked, p => p.ChallengerId == leaver || p.OtherId == leaver);
            Assert.DoesNotContain(leaver, done.Order!);
            Assert.Equal(19, done.Order!.Count);
        }
    }

    [Fact]
    public void ARemovedPlacedAnimeLowersThePlacedCountByOneWhileTheChallengerStillHasSomeoneToBeShownWith()
    {
        var queue = Ids(1, 20);
        var live = LiveOf(queue);
        var answers = new List<ThisOrThatAnswer>();
        PlayPicks(queue, answers, live, 30);
        var before = Replay(queue, answers, live).Pair!;

        foreach (var leaver in queue.TakeWhile(id => id != before.ChallengerId))
        {
            var after = Replay(queue, answers, LiveOf(live.Where(id => id != leaver))).Pair!;

            Assert.Equal(before.Placed - 1, after.Placed);
            Assert.Equal(before.Total - 1, after.Total);
            Assert.Equal(before.ChallengerId, after.ChallengerId);
        }
    }

    [Fact]
    public void RemovingTheOnlyAnimeTheChallengerCouldStillBeShownWithPlacesTheChallengerOnTheSpot()
    {
        // 3 has been ranked below 1, so only 2 is left to compare it with. When 2
        // leaves there is nobody: 3 is placed without a pick, the placed count stays
        // at two (it lost 2 and gained 3), and 4 begins.
        var queue = Ids(1, 4);
        var answers = new List<ThisOrThatAnswer> { new(2, 1, Below), new(3, 1, Below) };
        var before = Replay(queue, answers, LiveOf(queue)).Pair;
        // One pick for 3 against 2, then 4 among three placed takes two.
        Assert.Equal(new ThisOrThatPair(ChallengerId: 3, OtherId: 2, Placed: 2, Total: 4, PicksLeftAtMost: 3), before);

        var after = Replay(queue, answers, LiveOf([1, 3, 4])).Pair;

        Assert.Equal(new ThisOrThatPair(ChallengerId: 4, OtherId: 3, Placed: 2, Total: 3, PicksLeftAtMost: 2), after);
    }

    [Fact]
    public void NoPairAlreadyAnsweredIsAskedAgainAfterAnAnimeLeaves()
    {
        var queue = Ids(1, 20);
        var live = LiveOf(queue);
        var answers = new List<ThisOrThatAnswer>();
        PlayPicks(queue, answers, live, 30);

        foreach (var leaver in queue.Take(10))
        {
            var without = LiveOf(live.Where(id => id != leaver));
            var played = new List<ThisOrThatAnswer>(answers);
            var asked = new List<ThisOrThatPair>();

            PlayOut(queue, played, without, Truthful, asked: asked);

            foreach (var pair in asked)
                Assert.False(Asked(answers, pair.ChallengerId, pair.OtherId), $"{pair} was already answered (leaver {leaver})");
        }
    }

    [Fact]
    public void AnAnimeThatLeftKeepsItsSlotWhenItsPicksDeterminedIt()
    {
        // 2 is placed below 1, and 3 below 2, so 3 is below 1 too. When 2 leaves,
        // that stays known: 4 is placed against 1 and 3, and 3 is never asked
        // against 1 again.
        var queue = Ids(1, 4);
        var answers = new List<ThisOrThatAnswer> { new(2, 1, Below), new(3, 2, Below) };
        var live = LiveOf([1, 3, 4]);

        var state = Replay(queue, answers, live);

        Assert.Equal(new ThisOrThatPair(ChallengerId: 4, OtherId: 3, Placed: 2, Total: 3, PicksLeftAtMost: 2), state.Pair);
    }

    [Fact]
    public void AChallengerThatLeftBeforeItWasPlacedDropsOut()
    {
        // 5 was ranked above the 3rd of four, then left: nothing depends on it.
        var queue = Ids(1, 6);
        var answers = new List<ThisOrThatAnswer>(Enumerable.Range(2, 3).Select(c => new ThisOrThatAnswer(c, c - 1, Below)))
        {
            new(5, 3, Above),
        };
        var live = LiveOf([1, 2, 3, 4, 6]);

        var state = Replay(queue, answers, live);

        Assert.Equal(6, state.Pair!.ChallengerId);
        Assert.Equal(4, state.Pair.Placed);
        Assert.Equal(5, state.Pair.Total);
    }

    [Fact]
    public void ARemovedChallengerMidPlacementDropsOutAndTheNextOneBegins()
    {
        var queue = Ids(1, 20);
        var live = LiveOf(queue);
        var answers = new List<ThisOrThatAnswer>();
        var state = PlayOut(queue, answers, live, Truthful,
            stopAt: p => answers.Any(a => a.ChallengerId == p.ChallengerId));
        var challenger = state.Pair!.ChallengerId;
        var nextInQueue = queue[queue.IndexOf(challenger) + 1];

        var without = LiveOf(live.Where(id => id != challenger));
        var after = Replay(queue, answers, without).Pair!;
        var done = PlayOut(queue, [.. answers], without, Truthful);

        Assert.Equal(nextInQueue, after.ChallengerId);
        Assert.Equal(state.Pair.Placed, after.Placed);
        Assert.Equal(state.Pair.Total - 1, after.Total);
        Assert.Equal(Ids(1, 20).Where(id => id != challenger), done.Order);
    }

    [Fact]
    public void AnAnimeJoiningIsPlacedAfterEveryAnimeStillWaiting()
    {
        var queue = Ids(1, 8);
        var live = LiveOf(queue);
        var answers = new List<ThisOrThatAnswer>();
        PlayPicks(queue, answers, live, 6);
        var before = Replay(queue, answers, live).Pair!;

        // 99 joined: the service appends it to the end of the queue.
        List<int> withJoiner = [.. queue, 99];
        var liveWithJoiner = LiveOf(withJoiner);
        var after = Replay(withJoiner, answers, liveWithJoiner).Pair!;
        var asked = new List<ThisOrThatPair>();
        var done = PlayOut(withJoiner, answers, liveWithJoiner, Truthful, asked: asked);

        // The anime that was placing carries on, the total counts one more, and 99
        // is the last anime to be placed, in one run of pairs.
        var challengers = asked.Select(p => p.ChallengerId).Distinct().ToList();
        Assert.Equal(before.ChallengerId, after.ChallengerId);
        Assert.Equal(before.Total + 1, after.Total);
        Assert.Equal(99, challengers[^1]);
        Assert.Equal(1, challengers.Count(id => id == 99));
        Assert.Equal([.. Ids(1, 8), 99], done.Order);
    }

    [Fact]
    public void SeveralAnimeJoiningAreAskedInTheOrderTheyWereAppended()
    {
        List<int> queue = [1, 2, 3, 40, 30, 50];
        var live = LiveOf(queue);
        var asked = new List<ThisOrThatPair>();

        PlayOut(queue, [], live, Truthful, asked: asked);

        Assert.Equal([2, 3, 40, 30, 50], asked.Select(p => p.ChallengerId).Distinct());
    }

    [Fact]
    public void AnAnimeLeavingAndRejoiningRightAwayRestoresTheStateExactly()
    {
        var queue = Ids(1, 12);
        var live = LiveOf(queue);
        var answers = new List<ThisOrThatAnswer>();
        PlayPicks(queue, answers, live, 14);
        var before = Replay(queue, answers, live);

        foreach (var anime in queue.TakeWhile(id => id != before.Pair!.ChallengerId))
        {
            var gone = Replay(queue, answers, LiveOf(live.Where(id => id != anime)));
            var back = Replay(queue, answers, live);

            Assert.NotEqual(before.Pair, gone.Pair);
            Assert.Equal(before, back);
        }
    }

    [Fact]
    public void AnAnimeThatLeftAndRejoinedTakesBackThePlaceItsEarlierPicksGaveIt()
    {
        var queue = Ids(1, 12);
        var live = LiveOf(queue);
        var answers = new List<ThisOrThatAnswer>();
        PlayPicks(queue, answers, live, 14);
        var placed = queue.TakeWhile(id => id != Replay(queue, answers, live).Pair!.ChallengerId).ToList();
        var leaver = placed[2];

        // It goes, three more picks are made without it, and it comes back.
        var without = LiveOf(live.Where(id => id != leaver));
        PlayPicks(queue, answers, without, 3);
        var asked = new List<ThisOrThatPair>();
        var done = PlayOut(queue, answers, live, Truthful, asked: asked);

        Assert.Equal(Ids(1, 12), done.Order);
        Assert.False(answers.GroupBy(a => (a.ChallengerId, a.OtherId)).Any(g => g.Count() > 1), "a pair was answered twice");
    }

    [Fact]
    public void AScoreLeftWithOneLiveAnimeFinishesWithThatOne()
    {
        var queue = Ids(1, 5);

        var noPicks = Replay(queue, [], LiveOf([4]));
        var somePicks = Replay(queue, [new(2, 1, Below), new(3, 1, Above)], LiveOf([3]));

        Assert.Equal([4], noPicks.Order);
        Assert.Equal([3], somePicks.Order);
    }

    [Fact]
    public void AScoreLeftWithNoLiveAnimeFinishesEmpty()
    {
        Assert.Empty(Replay(Ids(1, 3), [new(2, 1, Below)], LiveOf([])).Order!);
    }

    [Fact]
    public void AnAnimeInTheQueueTwiceCountsOnceAtItsFirstPlace()
    {
        var state = Replay([1, 2, 1, 3], [], LiveOf([1, 2, 3]));

        Assert.Equal(3, state.Pair!.Total);
        Assert.Equal(2, state.Pair.ChallengerId);
    }

    [Fact]
    public void AnAnswerThatContradictsItselfIsClampedNotThrown()
    {
        // 3 "above 1" and "below 2" cannot both hold with 1 above 2, as a hand-edited
        // row might say. 3 has left, and its range is clamped to one slot, so it keeps
        // that slot and what 4 said about it still counts: 4 is placed, not asked.
        List<ThisOrThatAnswer> answers = [new(2, 1, Below), new(3, 1, Above), new(3, 2, Below), new(4, 3, Below)];

        var done = Replay(Ids(1, 4), answers, LiveOf([1, 2, 4]));

        Assert.True(done.IsFinished);
        Assert.Equal([1, 2, 4], done.Order);
    }

    [Fact]
    public void AnAnswerAboutAnAnimeNotInTheQueueIsIgnored()
    {
        var state = Replay([1, 2], [new(2, 77, Above)], LiveOf([1, 2]));

        Assert.Equal(new ThisOrThatPair(2, 1, 1, 2, 1), state.Pair);
    }

    // ---- the next pair's pictures ----

    [Fact]
    public void UpcomingPairIsTheStateTheOutcomeWouldLeadTo()
    {
        var queue = Ids(1, 8);
        var live = LiveOf(queue);
        var answers = new List<ThisOrThatAnswer>();
        PlayPicks(queue, answers, live, 9);
        var current = Replay(queue, answers, live).Pair!;

        foreach (var outcome in new[] { Above, Below, CantDecide })
        {
            var expected = Replay(queue, [.. answers, new(current.ChallengerId, current.OtherId, outcome)], live).Pair;

            Assert.Equal(expected, ThisOrThatReplay.UpcomingPair(queue, answers, live, current, outcome));
        }
    }

    [Fact]
    public void UpcomingPairIsNullWhenTheAnswerFinishesTheRanking()
    {
        List<int> queue = [1, 2];
        var live = LiveOf(queue);
        var current = Replay(queue, [], live).Pair!;

        foreach (var outcome in new[] { Above, Below, CantDecide })
            Assert.Null(ThisOrThatReplay.UpcomingPair(queue, [], live, current, outcome));
    }

    [Fact]
    public void TheThreeUpcomingPairsOfAMidPlacementNameAtMostFourOtherAnime()
    {
        var queue = Ids(1, 30);
        var live = LiveOf(queue);
        var answers = new List<ThisOrThatAnswer>();
        PlayOut(queue, answers, live, Truthful, stopAt: p => p.ChallengerId == 20);
        var current = Replay(queue, answers, live).Pair!;

        var others = new[] { Above, Below, CantDecide }
            .Select(o => ThisOrThatReplay.UpcomingPair(queue, answers, live, current, o))
            .Where(p => p is not null)
            .SelectMany(p => new[] { p!.ChallengerId, p.OtherId })
            .Where(id => id != current.ChallengerId && id != current.OtherId)
            .Distinct()
            .ToList();

        Assert.InRange(others.Count, 1, 4);
    }

    // ---- the properties of halving_sim.py ----

    [Fact]
    public void ConsistentPicksGiveTheTrueOrderInAtMostMaxPicksPicks()
    {
        foreach (var (count, seeds) in new[] { (2, 20), (3, 20), (12, 30), (77, 8), (149, 3) })
        {
            for (var seed = 0; seed < seeds; seed++)
            {
                var run = Simulate(count, seed: seed);

                var trueOrder = run.Truth.Keys.OrderByDescending(id => run.Truth[id]).ToList();
                Assert.True(trueOrder.SequenceEqual(run.Order), $"n={count} seed={seed}: not the true order");
                Assert.True(run.Picks <= ThisOrThatReplay.MaxPicks(count),
                    $"n={count} seed={seed}: {run.Picks} picks is over {ThisOrThatReplay.MaxPicks(count)}");
            }
        }
    }

    [Fact]
    public void WithRemovalsJoinsRejoinsTiesWrongPicksAndUndosTheOrderAlwaysAgreesAndNothingIsAskedTwice()
    {
        // Simulate asserts, for every state of every run: the finished order agrees
        // with every answer between live anime, no answered pair is asked again, and
        // the most picks left never rises after a pick (only an Undo or a change to
        // the list can raise it).
        for (var seed = 0; seed < 300; seed++)
        {
            Simulate(60, removals: 6, joins: 6, rejoins: 2, tieRate: 0.1, noise: 0.15, undoRate: 0.05, seed: seed);
        }
    }

    [Fact]
    public void AnAnimeLeavingMidRankingLowersThePlacedCountByAtMostOneAndNeverRaisesIt()
    {
        for (var seed = 0; seed < 200; seed++)
        {
            var rng = new Random(seed);
            var truth = Ids(1, 40).ToDictionary(id => id, _ => rng.NextDouble());
            var queue = Shuffle(Ids(1, 40), rng);
            var live = LiveOf(queue);
            var answers = new List<ThisOrThatAnswer>();
            for (var i = 0; i < 120 && Replay(queue, answers, live).Pair is { } p; i++)
                answers.Add(new ThisOrThatAnswer(p.ChallengerId, p.OtherId, truth[p.ChallengerId] > truth[p.OtherId] ? Above : Below));

            if (Replay(queue, answers, live).Pair is not { } pair)
                continue;

            var placed = queue.TakeWhile(id => id != pair.ChallengerId).ToList();
            var leaver = placed[rng.Next(placed.Count)];
            var after = Replay(queue, answers, LiveOf(live.Where(id => id != leaver))).Pair!;

            // One fewer, or the same when the removal left the anime being placed
            // nobody to be shown with, so it was placed on the spot and the next began.
            var lowersByOne = after.Placed == pair.Placed - 1;
            var challengerPlacedOnTheSpot = after.Placed == pair.Placed && after.ChallengerId != pair.ChallengerId;
            Assert.True(lowersByOne || challengerPlacedOnTheSpot,
                $"seed={seed}: {pair.Placed} placed became {after.Placed} ({pair} -> {after}, leaver {leaver})");
        }
    }

    private sealed record Run(int Picks, IReadOnlyList<int> Order, Dictionary<int, double> Truth);

    private static List<int> Shuffle(List<int> ids, Random rng)
    {
        var list = ids.ToList();
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }

        return list;
    }

    private static Run Simulate(
        int count, int removals = 0, int joins = 0, int rejoins = 0,
        double tieRate = 0, double noise = 0, double undoRate = 0, int seed = 0)
    {
        var rng = new Random(seed);
        var ids = Ids(1, count);
        var truth = ids.ToDictionary(id => id, _ => rng.NextDouble());
        var queue = Shuffle(ids, rng);
        var live = LiveOf(ids);
        var answers = new List<ThisOrThatAnswer>();
        var removed = new List<int>();
        var picks = 0;
        var nextId = count + 1;
        (int Bound, int Placed)? last = null;
        var changed = false;

        for (var guard = 0; guard < 200_000; guard++)
        {
            var state = Replay(queue, answers, live);
            if (state.Order is { } order)
            {
                var position = order.Select((id, index) => (id, index)).ToDictionary(x => x.id, x => x.index);
                foreach (var a in answers)
                {
                    if (a.Outcome != CantDecide && position.TryGetValue(a.ChallengerId, out var c) && position.TryGetValue(a.OtherId, out var p))
                        Assert.True((c < p) == (a.Outcome == Above), $"seed={seed}: the order disagrees with {a}");
                }

                Assert.True(order.OrderBy(x => x).SequenceEqual(live.OrderBy(x => x)), $"seed={seed}: the order is not the live set");
                return new Run(picks, order, truth);
            }

            var pair = state.Pair!;
            Assert.False(answers.Any(a => a.ChallengerId == pair.ChallengerId && a.OtherId == pair.OtherId),
                $"seed={seed}: {pair} was asked again");
            if (last is { } before && !changed)
                Assert.True(pair.PicksLeftAtMost <= before.Bound, $"seed={seed}: the most picks left rose from {before.Bound} to {pair.PicksLeftAtMost}");
            changed = false;
            last = (pair.PicksLeftAtMost, pair.Placed);

            if (answers.Count > 0 && rng.NextDouble() < undoRate)
            {
                answers.RemoveAt(answers.Count - 1);
                changed = true;
                continue;
            }

            picks++;
            var outcome = rng.NextDouble() < tieRate
                ? CantDecide
                : (truth[pair.ChallengerId] > truth[pair.OtherId]) != (rng.NextDouble() < noise) ? Above : Below;
            answers.Add(new ThisOrThatAnswer(pair.ChallengerId, pair.OtherId, outcome));

            if (removals > 0 && rng.NextDouble() < 0.03)
            {
                var leaver = live.OrderBy(x => x).ElementAt(rng.Next(live.Count));
                live.Remove(leaver);
                removed.Add(leaver);
                removals--;
                changed = true;
            }

            if (joins > 0 && rng.NextDouble() < 0.03)
            {
                queue.Add(nextId);
                truth[nextId] = rng.NextDouble();
                live.Add(nextId);
                nextId++;
                joins--;
                changed = true;
            }

            if (rejoins > 0 && removed.Count > 0 && rng.NextDouble() < 0.03)
            {
                live.Add(removed[^1]);
                removed.RemoveAt(removed.Count - 1);
                rejoins--;
                changed = true;
            }
        }

        throw new InvalidOperationException($"seed={seed}: the ranking never finished");
    }
}
