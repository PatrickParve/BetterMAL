using System.Text.Json;
using System.Text.Json.Serialization;
using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Games;
using Microsoft.EntityFrameworkCore;
using static AnimeTracker.Api.Tests.Services.Games.HigherOrLowerTestKit;

namespace AnimeTracker.Api.Tests.Services.Games;

// add-games-higher-or-lower task 3.7, second half: judging a pick, the rotation of
// design D8, milestones, the ending of a game and the second tab. Most cases write
// the game straight to its table (AddRunAsync) so they can stand it exactly where
// the rule they check applies; a few play whole games.
public class HigherOrLowerServicePickTests
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private static HigherOrLowerService Service(AnimeTrackerDbContext db, int seed = 11) => new(db, new Random(seed));

    /// <summary>Anime 1 and 2 with the given scores, plus <paramref name="others"/>
    /// more (ids 3 upward) each with a score of its own.</summary>
    private static async Task SeedTableAsync(AnimeTrackerDbContext db, double left, double right, int others = 20)
    {
        db.AnimeMetadata.AddRange(Anime(1, score: left), Anime(2, score: right));
        for (var id = 3; id < 3 + others; id++)
            db.AnimeMetadata.Add(Anime(id, score: Math.Round(4 + id / 100.0, 2)));
        await db.SaveChangesAsync();
    }

    private static async Task SetScoreAsync(AnimeTrackerDbContext db, int animeId, double? score)
    {
        (await db.AnimeMetadata.SingleAsync(a => a.Id == animeId)).MalScore = score;
        await db.SaveChangesAsync();
    }

    private static Task<HigherOrLowerRun?> StoredRunAsync(AnimeTrackerDbContext db) =>
        db.HigherOrLowerRuns.AsNoTracking().SingleOrDefaultAsync();

    private static async Task<List<long>> AppearanceIdsAsync(AnimeTrackerDbContext db) =>
        await db.HigherOrLowerAppearances.AsNoTracking().OrderBy(a => a.Id).Select(a => a.Id).ToListAsync();

    /// <summary>Plays a game to its end by always picking the higher score, as the
    /// scores stand in the database. Returns every table played and the last
    /// answer.</summary>
    private static async Task<(List<HigherOrLowerGameDto> Tables, HigherOrLowerPickDto Last)> PlayHigherAsync(
        AnimeTrackerDbContext db, HigherOrLowerService service, HigherOrLowerGameDto first, int maxPicks)
    {
        var scores = await db.AnimeMetadata.AsNoTracking().ToDictionaryAsync(a => a.Id, a => a.MalScore ?? 0);
        var tables = new List<HigherOrLowerGameDto> { first };
        HigherOrLowerPickDto? last = null;

        while (tables.Count <= maxPicks)
        {
            var table = tables[^1];
            var higher = scores[table.Left.AnimeId] >= scores[table.Right.AnimeId] ? table.Left.AnimeId : table.Right.AnimeId;
            last = await service.PickAsync(table.Id, table.Round, higher);
            if (last.Next is null)
                break;
            tables.Add(last.Next);
        }

        return (tables, last!);
    }

    // ---- judging ----

    [Fact]
    public async Task PickingTheHigherLeftAnimeIsRight()
    {
        using var db = CreateDb();
        await SeedTableAsync(db, left: 9.0, right: 5.0);
        var run = await AddRunAsync(db, 1, 2);

        var result = await Service(db).PickAsync(run.Id, 1, 1);

        Assert.True(result.Correct);
        Assert.Equal(9.0, result.LeftScore);
        Assert.Equal(5.0, result.RightScore);
        Assert.Equal(1, result.Streak);
        Assert.Null(result.End);
        Assert.NotNull(result.Next);
    }

    [Fact]
    public async Task PickingTheHigherRightAnimeIsRight()
    {
        using var db = CreateDb();
        await SeedTableAsync(db, left: 5.0, right: 9.0);
        var run = await AddRunAsync(db, 1, 2);

        var result = await Service(db).PickAsync(run.Id, 1, 2);

        Assert.True(result.Correct);
        Assert.Equal(5.0, result.LeftScore);
        Assert.Equal(9.0, result.RightScore);
    }

    [Theory]
    [InlineData(8.20, 8.17, 1, true)]   // the spec's own pair: the higher is right
    [InlineData(8.20, 8.17, 2, false)]  // and the lower is wrong
    [InlineData(8.17, 8.20, 1, false)]
    [InlineData(8.17, 8.20, 2, true)]
    public async Task OnlyTheHigherScoreIsRight(double left, double right, int picked, bool expected)
    {
        using var db = CreateDb();
        await SeedTableAsync(db, left, right);
        var run = await AddRunAsync(db, 1, 2);

        var result = await Service(db).PickAsync(run.Id, 1, picked);

        Assert.Equal(expected, result.Correct);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task ATieCountsAsRightWhicheverIsPicked(int picked)
    {
        using var db = CreateDb();
        await SeedTableAsync(db, left: 7.0, right: 7.0);
        var run = await AddRunAsync(db, 1, 2);

        Assert.True((await Service(db).PickAsync(run.Id, 1, picked)).Correct);
    }

    [Fact]
    public async Task ScoresThatMatchAtTwoDecimalsAreATie()
    {
        using var db = CreateDb();
        await SeedTableAsync(db, left: 7.85, right: 7.8500000001);
        var run = await AddRunAsync(db, 1, 2);

        Assert.True((await Service(db).PickAsync(run.Id, 1, 2)).Correct);
    }

    [Fact]
    public async Task APickIsJudgedOnTheScoresStoredNowNotWhenTheCardsAppeared()
    {
        using var db = CreateDb();
        await SeedTableAsync(db, left: 9.0, right: 5.0);
        var run = await AddRunAsync(db, 1, 2);
        // A refresh moves the right-hand anime above the left one before the pick.
        await SetScoreAsync(db, 2, 9.5);

        var result = await Service(db).PickAsync(run.Id, 1, 1);

        Assert.False(result.Correct);
        Assert.Equal(9.0, result.LeftScore);
        Assert.Equal(9.5, result.RightScore);
    }

    [Fact]
    public async Task AScoreThatMovesIntoATieJustBeforeThePickCountsAsRight()
    {
        using var db = CreateDb();
        await SeedTableAsync(db, left: 9.0, right: 5.0);
        var run = await AddRunAsync(db, 1, 2);
        await SetScoreAsync(db, 2, 9.0);

        var result = await Service(db).PickAsync(run.Id, 1, 1);

        Assert.True(result.Correct);
        Assert.Equal(result.LeftScore, result.RightScore);
    }

    [Fact]
    public async Task AnAnimeThatHasLostItsScoreCountsAsTheLowest()
    {
        using var db = CreateDb();
        await SeedTableAsync(db, left: 6.0, right: 9.0);
        var run = await AddRunAsync(db, 1, 2);
        await SetScoreAsync(db, 2, null);

        var result = await Service(db).PickAsync(run.Id, 1, 1);

        Assert.True(result.Correct);
        Assert.Equal(0, result.RightScore);
    }

    // ---- a wrong pick ----

    [Fact]
    public async Task AWrongPickEndsTheGameWithItsStreak()
    {
        using var db = CreateDb();
        await SeedTableAsync(db, left: 9.0, right: 5.0);
        var run = await AddRunAsync(db, 1, 2, round: 8, streak: 7);

        var result = await Service(db).PickAsync(run.Id, 8, 2);

        Assert.False(result.Correct);
        Assert.Equal(HigherOrLowerEnd.Wrong, result.End);
        Assert.Equal(7, result.Streak);
        Assert.Null(result.Next);
        Assert.Null(result.MilestoneLevel);
        Assert.Equal(9.0, result.LeftScore);
        Assert.Equal(5.0, result.RightScore);
    }

    [Fact]
    public async Task AWrongFirstPickEndsWithAStreakOfZero()
    {
        using var db = CreateDb();
        await SeedTableAsync(db, left: 9.0, right: 5.0);
        var run = await AddRunAsync(db, 1, 2);

        var result = await Service(db).PickAsync(run.Id, 1, 2);

        Assert.Equal(0, result.Streak);
    }

    [Fact]
    public async Task AWrongPickLeavesNoGameBehind()
    {
        using var db = CreateDb();
        await SeedTableAsync(db, left: 9.0, right: 5.0);
        var run = await AddRunAsync(db, 1, 2, streak: 7);

        await Service(db).PickAsync(run.Id, 1, 2);

        Assert.Null(await StoredRunAsync(db));
    }

    [Fact]
    public async Task AWrongPickAtAMilestoneStreakIsNotCelebrated()
    {
        using var db = CreateDb();
        await SeedTableAsync(db, left: 9.0, right: 5.0);
        var run = await AddRunAsync(db, 1, 2, round: 11, streak: 10);

        var result = await Service(db).PickAsync(run.Id, 11, 2);

        Assert.Null(result.MilestoneLevel);
        Assert.Equal(HigherOrLowerEnd.Wrong, result.End);
    }

    [Fact]
    public async Task ThePickedAnimeNeedNotBeInThePoolAnyMore()
    {
        // Both anime on the table stay judgeable if the settings no longer cover them.
        using var db = CreateDb();
        await SeedTableAsync(db, left: 9.0, right: 5.0);
        var run = await AddRunAsync(db, 1, 2, settings: AnyAnime with { MediaTypes = ["movie"] });

        var result = await Service(db).PickAsync(run.Id, 1, 1);

        Assert.True(result.Correct);
    }

    // ---- refusals ----

    [Fact]
    public async Task APickForNoSuchGameIsNotFound()
    {
        using var db = CreateDb();
        await SeedTableAsync(db, 9.0, 5.0);

        await Assert.ThrowsAsync<HigherOrLowerRunNotFoundException>(() => Service(db).PickAsync(99, 1, 1));
    }

    [Fact]
    public async Task APickForAGameThatEndedIsNotFound()
    {
        using var db = CreateDb();
        await SeedTableAsync(db, 9.0, 5.0);
        var run = await AddRunAsync(db, 1, 2);
        await Service(db).EndAsync(run.Id);

        await Assert.ThrowsAsync<HigherOrLowerRunNotFoundException>(() => Service(db).PickAsync(run.Id, 1, 1));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(0)]
    public async Task APickForAnotherRoundIsStaleAndChangesNothing(int staleRound)
    {
        using var db = CreateDb();
        await SeedTableAsync(db, 9.0, 5.0);
        var run = await AddRunAsync(db, 1, 2, round: 3, streak: 2);
        var before = await AppearanceIdsAsync(db);

        await Assert.ThrowsAsync<HigherOrLowerStaleRoundException>(() => Service(db).PickAsync(run.Id, staleRound, 1));

        var stored = await StoredRunAsync(db);
        Assert.Equal(3, stored!.Round);
        Assert.Equal(2, stored.Streak);
        Assert.Equal(before, await AppearanceIdsAsync(db));
    }

    [Fact]
    public async Task APickOfAnAnimeNotOnTheTableIsRefusedAndChangesNothing()
    {
        using var db = CreateDb();
        await SeedTableAsync(db, 9.0, 5.0);
        var run = await AddRunAsync(db, 1, 2);

        await Assert.ThrowsAsync<HigherOrLowerCardNotOnTableException>(() => Service(db).PickAsync(run.Id, 1, 3));

        var stored = await StoredRunAsync(db);
        Assert.Equal(1, stored!.Round);
        Assert.Equal(0, stored.Streak);
    }

    [Fact]
    public async Task ATabBehindTheGameIsToldItIsBehindEvenWhenItsAnimeHaveLeft()
    {
        // The stale tab names an anime that has since left the table: the round
        // check comes first, so it is told it is behind rather than wrong.
        using var db = CreateDb();
        await SeedTableAsync(db, 9.0, 5.0);
        var run = await AddRunAsync(db, 1, 2, round: 4);

        await Assert.ThrowsAsync<HigherOrLowerStaleRoundException>(() => Service(db).PickAsync(run.Id, 3, 3));
    }

    [Fact]
    public async Task ADoubleSubmitCountsOnce()
    {
        using var db = CreateDb();
        await SeedTableAsync(db, 9.0, 5.0);
        var run = await AddRunAsync(db, 1, 2);
        var service = Service(db);

        await service.PickAsync(run.Id, 1, 1);
        await Assert.ThrowsAsync<HigherOrLowerStaleRoundException>(() => service.PickAsync(run.Id, 1, 1));

        var stored = await StoredRunAsync(db);
        Assert.Equal(2, stored!.Round);
        Assert.Equal(1, stored.Streak);
    }

    [Fact]
    public async Task ASecondConcurrentPickFailsOnTheConcurrencyToken()
    {
        const string name = "concurrent-pick";
        using var tabOne = CreateDb(name);
        using var tabTwo = CreateDb(name);
        await SeedTableAsync(tabOne, 9.0, 5.0);
        // Tab one holds the game at round 1 (AddRunAsync leaves it tracked), so its
        // round check passes. Tab two's pick lands first.
        var run = await AddRunAsync(tabOne, 1, 2);
        await Service(tabTwo).PickAsync(run.Id, 1, 1);

        await Assert.ThrowsAsync<HigherOrLowerStaleRoundException>(() => Service(tabOne).PickAsync(run.Id, 1, 1));

        // Only the first pick counted. (That the losing pick's appearance row is not
        // saved either rests on the save being one transaction, which InMemory does
        // not have; task 10.3 checks it on Npgsql.)
        var stored = await StoredRunAsync(tabTwo);
        Assert.Equal(2, stored!.Round);
        Assert.Equal(1, stored.Streak);
    }

    [Fact]
    public async Task APickAfterAnotherTabEndedTheGameFindsItGone()
    {
        const string name = "pick-after-end";
        using var tabOne = CreateDb(name);
        using var tabTwo = CreateDb(name);
        await SeedTableAsync(tabOne, 9.0, 5.0);
        var run = await AddRunAsync(tabOne, 1, 2);
        await Service(tabTwo).EndAsync(run.Id);

        // Tab one still tracks the game, but the read goes to the store and finds no row.
        await Assert.ThrowsAsync<HigherOrLowerRunNotFoundException>(() => Service(tabOne).PickAsync(run.Id, 1, 1));

        Assert.Null(await StoredRunAsync(tabTwo));
        Assert.Empty(await tabTwo.HigherOrLowerAppearances.AsNoTracking().ToListAsync());
    }

    // ---- what a response shows ----

    [Fact]
    public async Task TheNextRoundShowsTheStayingScoreAndHidesTheNewOne()
    {
        using var db = CreateDb();
        await SeedTableAsync(db, left: 9.0, right: 5.0);
        var run = await AddRunAsync(db, 1, 2);

        var next = (await Service(db).PickAsync(run.Id, 1, 1)).Next!;

        Assert.Equal(2, next.Round);
        Assert.Equal(1, next.Streak);
        Assert.Equal(run.Id, next.Id);
        Assert.Equal(1, next.Left.AnimeId);
        Assert.Equal(9.0, next.Left.Score);
        Assert.Null(next.Right.Score);
    }

    [Fact]
    public async Task TheHiddenScoreOfTheNewAnimeIsNotInTheResponseAtAll()
    {
        using var db = CreateDb();
        await SeedTableAsync(db, left: 9.0, right: 5.0);
        var run = await AddRunAsync(db, 1, 2);

        var result = await Service(db).PickAsync(run.Id, 1, 1);

        var newScore = (await db.AnimeMetadata.AsNoTracking().SingleAsync(a => a.Id == result.Next!.Right.AnimeId)).MalScore;
        var json = JsonSerializer.Serialize(result.Next, Wire);
        Assert.DoesNotContain(JsonSerializer.Serialize(newScore), json);
        Assert.Contains("\"score\":null", json);
    }

    [Fact]
    public async Task TheVerdictAndTheEndAreNamedByString()
    {
        using var db = CreateDb();
        await SeedTableAsync(db, left: 9.0, right: 5.0);
        var run = await AddRunAsync(db, 1, 2);

        var wrong = await Service(db).PickAsync(run.Id, 1, 2);

        Assert.Contains("\"end\":\"Wrong\"", JsonSerializer.Serialize(wrong, Wire));
    }

    [Fact]
    public async Task ACorrectPickRecordsTheNewAnimeAsShownAndAsPartOfTheGame()
    {
        using var db = CreateDb();
        await SeedTableAsync(db, left: 9.0, right: 5.0);
        var run = await AddRunAsync(db, 1, 2);

        var result = await Service(db).PickAsync(run.Id, 1, 1);

        var newId = result.Next!.Right.AnimeId;
        Assert.DoesNotContain(newId, new[] { 1, 2 });
        Assert.Equal([newId], await db.HigherOrLowerAppearances.AsNoTracking().Select(a => a.AnimeId).ToListAsync());
        Assert.Equal([1, 2, newId], (await StoredRunAsync(db))!.ShownAnimeIds);
    }

    // ---- rotation (design D8) ----

    [Fact]
    public async Task TheHigherAnimeStaysFromTheLeft()
    {
        using var db = CreateDb();
        await SeedTableAsync(db, left: 9.0, right: 5.0);
        var run = await AddRunAsync(db, 1, 2, leftStays: 0);

        var result = await Service(db).PickAsync(run.Id, 1, 1);

        Assert.Equal(1, result.Next!.Left.AnimeId);
        Assert.NotEqual(2, result.Next.Right.AnimeId);
        Assert.Equal(1, (await StoredRunAsync(db))!.LeftStays);
    }

    [Fact]
    public async Task TheHigherAnimeStaysFromTheRightAndMovesLeft()
    {
        using var db = CreateDb();
        await SeedTableAsync(db, left: 5.0, right: 9.0);
        var run = await AddRunAsync(db, 1, 2, leftStays: 1);

        var result = await Service(db).PickAsync(run.Id, 1, 2);

        Assert.Equal(2, result.Next!.Left.AnimeId);
        Assert.Equal(9.0, result.Next.Left.Score);
        Assert.DoesNotContain(result.Next.Right.AnimeId, new[] { 1, 2 });
        // The anime that takes over has stayed once, whatever count the one it replaced had.
        Assert.Equal(1, (await StoredRunAsync(db))!.LeftStays);
    }

    [Fact]
    public async Task AnAnimeStaysASecondTime()
    {
        using var db = CreateDb();
        await SeedTableAsync(db, left: 9.0, right: 5.0);
        var run = await AddRunAsync(db, 1, 2, round: 2, streak: 1, leftStays: 1);

        var result = await Service(db).PickAsync(run.Id, 2, 1);

        Assert.Equal(1, result.Next!.Left.AnimeId);
        Assert.Equal(2, (await StoredRunAsync(db))!.LeftStays);
    }

    [Fact]
    public async Task AnAnimeLeavesAfterTwoStaysThoughItScoredHigherAgain()
    {
        using var db = CreateDb();
        await SeedTableAsync(db, left: 9.0, right: 6.0);
        var run = await AddRunAsync(db, 1, 2, round: 3, streak: 2, leftStays: 2);

        var result = await Service(db).PickAsync(run.Id, 3, 1);

        Assert.True(result.Correct);
        // The lower-scored anime takes over the left, score shown, and 1 is off the table.
        Assert.Equal(2, result.Next!.Left.AnimeId);
        Assert.Equal(6.0, result.Next.Left.Score);
        Assert.DoesNotContain(1, new[] { result.Next.Left.AnimeId, result.Next.Right.AnimeId });
        Assert.Equal(1, (await StoredRunAsync(db))!.LeftStays);
    }

    [Fact]
    public async Task ARunOfRoundsFollowsTheRotationAndEachNewAnimeCountsItsOwnStays()
    {
        using var db = CreateDb();
        await SeedTableAsync(db, left: 9.0, right: 6.0, others: 40);
        var run = await AddRunAsync(db, 1, 2);
        var service = Service(db);

        async Task<HigherOrLowerGameDto> StepAsync(HigherOrLowerGameDto table, int picked, double newRightScore)
        {
            var result = await service.PickAsync(table.Id, table.Round, picked);
            Assert.True(result.Correct);
            await SetScoreAsync(db, result.Next!.Right.AnimeId, newRightScore);
            return result.Next!;
        }

        var round1 = (await service.GetStateAsync(false)).Game!;

        // 1 is the highest anime in the game. It stays in rounds 1 and 2 ...
        var round2 = await StepAsync(round1, picked: 1, newRightScore: 7.0);
        Assert.Equal(1, round2.Left.AnimeId);
        var round3 = await StepAsync(round2, picked: 1, newRightScore: 7.0);
        Assert.Equal(1, round3.Left.AnimeId);
        Assert.Equal(2, (await StoredRunAsync(db))!.LeftStays);

        // ... and in round 3 it leaves though it scored higher again. The right anime
        // takes over with its own count of one.
        var y = round3.Right.AnimeId;
        var round4 = await StepAsync(round3, picked: 1, newRightScore: 8.0);
        Assert.Equal(y, round4.Left.AnimeId);
        Assert.Equal(7.0, round4.Left.Score);
        Assert.Equal(1, (await StoredRunAsync(db))!.LeftStays);

        // Round 4: the new right anime scores higher than Y, so it takes over.
        var z = round4.Right.AnimeId;
        var round5 = await StepAsync(round4, picked: z, newRightScore: 6.5);
        Assert.Equal(z, round5.Left.AnimeId);
        Assert.Equal(8.0, round5.Left.Score);
        Assert.Equal(1, (await StoredRunAsync(db))!.LeftStays);

        // Z then has the higher score in its next two rounds: it stays through the
        // first and leaves after the second, the other anime taking over.
        var round6 = await StepAsync(round5, picked: z, newRightScore: 6.6);
        Assert.Equal(z, round6.Left.AnimeId);
        Assert.Equal(2, (await StoredRunAsync(db))!.LeftStays);

        var v = round6.Right.AnimeId;
        var round7 = await StepAsync(round6, picked: z, newRightScore: 5.0);
        Assert.Equal(v, round7.Left.AnimeId);
        Assert.Equal(1, (await StoredRunAsync(db))!.LeftStays);
        Assert.Equal(6, (await StoredRunAsync(db))!.Streak);
    }

    [Fact]
    public async Task ATieKeepsTheLeftAnime()
    {
        using var db = CreateDb();
        await SeedTableAsync(db, left: 7.0, right: 7.0);
        var run = await AddRunAsync(db, 1, 2, leftStays: 0);

        var result = await Service(db).PickAsync(run.Id, 1, 2);

        Assert.Equal(1, result.Next!.Left.AnimeId);
        Assert.Equal(1, (await StoredRunAsync(db))!.LeftStays);
    }

    [Fact]
    public async Task ATieDoesNotKeepALeftAnimeThatHasStayedTwice()
    {
        using var db = CreateDb();
        await SeedTableAsync(db, left: 7.0, right: 7.0);
        var run = await AddRunAsync(db, 1, 2, round: 3, leftStays: 2);

        var result = await Service(db).PickAsync(run.Id, 3, 1);

        Assert.Equal(2, result.Next!.Left.AnimeId);
        Assert.Equal(1, (await StoredRunAsync(db))!.LeftStays);
    }

    [Fact]
    public async Task NoAnimeIsOnScreenForMoreThanThreeRoundsOverALongGame()
    {
        using var db = CreateDb();
        await SeedAsync(db, 200);
        var service = Service(db);
        var first = await service.StartAsync(AnyAnime);

        var (tables, last) = await PlayHigherAsync(db, service, first, maxPicks: 60);

        Assert.True(tables.Count >= 30, $"the game ended after {tables.Count} rounds");
        var inARow = new Dictionary<int, int>();
        foreach (var table in tables)
        {
            var onTable = new[] { table.Left.AnimeId, table.Right.AnimeId };
            inARow = onTable.ToDictionary(id => id, id => inARow.GetValueOrDefault(id) + 1);
            Assert.All(inARow, kv => Assert.True(kv.Value <= 3, $"anime {kv.Key} was on screen {kv.Value} rounds running"));
        }
        // Picking the higher each time, the game was never lost.
        Assert.True(last.Correct);
    }

    [Fact]
    public async Task TheHigherAnimeAlwaysSurvivesTheRoundUnlessItIsAtItsLimit()
    {
        using var db = CreateDb();
        await SeedAsync(db, 200);
        var service = Service(db, seed: 3);
        var scores = await db.AnimeMetadata.AsNoTracking().ToDictionaryAsync(a => a.Id, a => a.MalScore!.Value);
        var first = await service.StartAsync(AnyAnime);

        var (tables, _) = await PlayHigherAsync(db, service, first, maxPicks: 50);

        var leaving = 0;
        for (var i = 0; i + 1 < tables.Count; i++)
        {
            var (l, r) = (tables[i].Left.AnimeId, tables[i].Right.AnimeId);
            var higher = scores[l] >= scores[r] ? l : r;
            var nextLeft = tables[i + 1].Left.AnimeId;
            if (nextLeft == higher)
                continue;
            // The higher anime left: only possible if it was the left one at its limit.
            leaving++;
            Assert.Equal(l, higher);
            Assert.Equal(r, nextLeft);
        }
        Assert.True(leaving > 0, "no anime reached its limit in 50 rounds");
    }

    // ---- the next anime ----

    [Fact]
    public async Task NoAnimeIsEverDrawnTwiceInAGameAndAGameEndsClearedWhenNoneIsLeft()
    {
        using var db = CreateDb();
        await SeedAsync(db, 60);
        var service = Service(db);
        var first = await service.StartAsync(AnyAnime);

        var (tables, last) = await PlayHigherAsync(db, service, first, maxPicks: 200);

        var drawn = new List<int> { first.Left.AnimeId, first.Right.AnimeId };
        drawn.AddRange(tables.Skip(1).Select(t => t.Right.AnimeId));
        Assert.Equal(drawn.Count, drawn.Distinct().Count());
        // 60 anime: two to begin and one more per correct pick, so the 59th pick finds none left.
        Assert.Equal(60, drawn.Count);
        Assert.Equal(HigherOrLowerEnd.Cleared, last.End);
        Assert.Equal(59, last.Streak);
        Assert.True(last.Correct);
        Assert.Null(last.Next);
    }

    [Fact]
    public async Task AGameThatClearsLeavesNoGameBehindButKeepsTheAnimeShown()
    {
        using var db = CreateDb();
        await SeedAsync(db, 5);
        var service = Service(db);
        var first = await service.StartAsync(AnyAnime);

        var (tables, last) = await PlayHigherAsync(db, service, first, maxPicks: 20);

        Assert.Equal(HigherOrLowerEnd.Cleared, last.End);
        Assert.Equal(4, last.Streak);
        Assert.Equal(4, tables.Count);
        Assert.Null(await StoredRunAsync(db));
        Assert.Equal(5, await db.HigherOrLowerAppearances.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task AGameClearsWhenTheRestShareTheStayingScore()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(
            Anime(1, score: 7.00), Anime(2, score: 5.00), Anime(3, score: 7.00), Anime(4, score: 7.00));
        await db.SaveChangesAsync();
        var run = await AddRunAsync(db, 1, 2);

        var result = await Service(db).PickAsync(run.Id, 1, 1);

        Assert.True(result.Correct);
        Assert.Equal(HigherOrLowerEnd.Cleared, result.End);
        Assert.Null(result.Next);
        Assert.Null(await StoredRunAsync(db));
    }

    [Fact]
    public async Task TheNewAnimeNeverHasTheScoreOfTheOneThatStays()
    {
        for (var seed = 0; seed < 40; seed++)
        {
            using var db = CreateDb();
            // The staying anime scores 7.00; eight more share that score and three do not.
            db.AnimeMetadata.AddRange(Anime(1, score: 7.00), Anime(2, score: 5.00));
            for (var id = 3; id <= 10; id++)
                db.AnimeMetadata.Add(Anime(id, score: 7.00));
            for (var id = 11; id <= 13; id++)
                db.AnimeMetadata.Add(Anime(id, score: 6.0 + id / 100.0));
            await db.SaveChangesAsync();
            var run = await AddRunAsync(db, 1, 2);

            var next = (await Service(db, seed).PickAsync(run.Id, 1, 1)).Next!;

            Assert.InRange(next.Right.AnimeId, 11, 13);
        }
    }

    [Fact]
    public async Task AnAnimeAddedToMyListMidGameLeavesANotInMyListPool()
    {
        for (var seed = 0; seed < 20; seed++)
        {
            using var db = CreateDb();
            db.AnimeMetadata.AddRange(
                Anime(1, score: 9.0), Anime(2, score: 5.0), Anime(3, score: 6.0), Anime(4, score: 6.5));
            await db.SaveChangesAsync();
            var run = await AddRunAsync(db, 1, 2, settings: AnyAnime with { List = HigherOrLowerListScope.NotOnMyList });
            // While the game is under way, 3 is added to my list.
            db.UserAnimeEntries.Add(OnList(3));
            await db.SaveChangesAsync();

            var result = await Service(db, seed).PickAsync(run.Id, 1, 1);

            Assert.Equal(4, result.Next!.Right.AnimeId);
        }
    }

    [Fact]
    public async Task AGameKeepsTheHideNsfwSettingItStartedWith()
    {
        using var db = CreateDb();
        for (var id = 1; id <= 6; id++)
            db.AnimeMetadata.Add(Anime(id, score: 5 + id / 10.0, rating: "pg_13"));
        for (var id = 7; id <= 12; id++)
            db.AnimeMetadata.Add(Anime(id, score: 5 + id / 10.0, rating: "rx"));
        await db.SaveChangesAsync();
        var service = Service(db);
        var first = await service.StartAsync(AnyAnime with { HideHentai = true });

        // The state is read with Hide NSFW off, as it is once the setting has been changed.
        await service.GetStateAsync(hideHentai: false);
        var (_, last) = await PlayHigherAsync(db, service, first, maxPicks: 20);

        Assert.Equal(HigherOrLowerEnd.Cleared, last.End);
        Assert.All(
            await db.HigherOrLowerAppearances.AsNoTracking().Select(a => a.AnimeId).ToListAsync(),
            id => Assert.InRange(id, 1, 6));
    }

    [Fact]
    public async Task ARecentlyShownAnimeIsHeldBackAcrossGamesWithOtherSettings()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1, score: 9.0), Anime(2, score: 5.0));
        for (var id = 3; id <= 42; id++)
            db.AnimeMetadata.Add(Anime(id, score: Math.Round(4 + id / 100.0, 2), mediaType: "movie"));
        await db.SaveChangesAsync();

        // A game with other settings (TV only) last showed anime 3, which a game
        // with these settings (everything) can also draw.
        db.HigherOrLowerAppearances.Add(new HigherOrLowerAppearance { AnimeId = 3 });
        await db.SaveChangesAsync();

        var timesThree = 0;
        var timesFour = 0;
        for (var seed = 0; seed < 600; seed++)
        {
            var run = await AddRunAsync(db, 1, 2);
            var result = await Service(db, seed).PickAsync(run.Id, 1, 1);
            if (result.Next!.Right.AnimeId == 3) timesThree++;
            if (result.Next.Right.AnimeId == 4) timesFour++;

            // Every trial meets the same history: forget this pick's own showing and game.
            db.HigherOrLowerAppearances.RemoveRange(await db.HigherOrLowerAppearances.Where(a => a.AnimeId != 3).ToListAsync());
            db.HigherOrLowerRuns.RemoveRange(await db.HigherOrLowerRuns.ToListAsync());
            await db.SaveChangesAsync();
        }

        // Anime 3 was shown last; anime 4 never was. Both are otherwise alike.
        Assert.True(timesFour > 5, $"anime 4 drawn only {timesFour} times");
        Assert.True(timesThree * 5 < timesFour, $"anime 3 drawn {timesThree} times, anime 4 {timesFour}");
    }

    [Fact]
    public async Task TheNextAnimeIsNeverOneThatHasNotAiredAnEpisode()
    {
        for (var seed = 0; seed < 20; seed++)
        {
            using var db = CreateDb();
            db.AnimeMetadata.AddRange(
                Anime(1, score: 9.0), Anime(2, score: 5.0),
                Anime(3, score: 6.0, airingStatus: "not_yet_aired"), Anime(4, score: 6.5),
                Anime(5, score: 6.6, airingStatus: "not_yet_aired"));
            await db.SaveChangesAsync();
            var run = await AddRunAsync(db, 1, 2);
            var service = Service(db, seed);

            var first = await service.PickAsync(run.Id, 1, 1);
            Assert.Equal(4, first.Next!.Right.AnimeId);

            // With 4 shown, the two not yet aired are all that is left: the game is won, not stuck.
            var second = await service.PickAsync(run.Id, 2, 1);
            Assert.Equal(HigherOrLowerEnd.Cleared, second.End);
        }
    }

    // ---- milestones ----

    [Theory]
    [InlineData(8, 9, null)]
    [InlineData(9, 10, 0)]
    [InlineData(10, 11, null)]
    [InlineData(24, 25, 1)]
    [InlineData(49, 50, 2)]
    [InlineData(99, 100, 3)]
    [InlineData(249, 250, 4)]
    public async Task ACorrectPickCarriesTheMilestoneLevelOfItsNewStreak(int streakBefore, int streakAfter, int? level)
    {
        using var db = CreateDb();
        await SeedTableAsync(db, left: 9.0, right: 5.0);
        var run = await AddRunAsync(db, 1, 2, round: streakBefore + 1, streak: streakBefore);

        var result = await Service(db).PickAsync(run.Id, streakBefore + 1, 1);

        Assert.Equal(streakAfter, result.Streak);
        Assert.Equal(level, result.MilestoneLevel);
        Assert.NotNull(result.Next);
    }

    [Fact]
    public async Task AMilestoneOnTheLastAnimeIsStillCarriedWhenTheGameClears()
    {
        using var db = CreateDb();
        await SeedTableAsync(db, left: 9.0, right: 5.0, others: 3);
        // Every anime in the pool has been shown, and the streak is one short of 10.
        var run = await AddRunAsync(db, 1, 2, round: 10, streak: 9, shown: [1, 2, 3, 4, 5]);

        var result = await Service(db).PickAsync(run.Id, 10, 1);

        Assert.Equal(HigherOrLowerEnd.Cleared, result.End);
        Assert.Equal(10, result.Streak);
        Assert.Equal(0, result.MilestoneLevel);
        Assert.Null(result.Next);
        Assert.Null(await StoredRunAsync(db));
    }
}
