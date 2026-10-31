using AnimeTracker.Api.Services.Games;

namespace AnimeTracker.Api.Tests.Services.Games;

// add-games-higher-or-lower design D5/D6: both factors are pure, so each case is
// a plain unit test of the curve's shape. The constants are named so they can be
// tuned, so the tests read them rather than restate their values.
public class GameWeightsTests
{
    private const int Full = GameWeights.FullWeightRank;
    private const double Floor = GameWeights.PopularityFloor;

    // ---- popularity ----

    [Theory]
    [InlineData(1)]
    [InlineData(500)]
    [InlineData(Full + 1)]
    [InlineData(20_000)]
    [InlineData(24_943)]
    public void AnAnimeOnMyListHasFullWeightHoweverUnpopular(int rank)
    {
        Assert.Equal(1, GameWeights.Popularity(rank, onMyList: true));
    }

    [Fact]
    public void AnAnimeOnMyListHasFullWeightWithNoRankAtAll()
    {
        Assert.Equal(1, GameWeights.Popularity(null, onMyList: true));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(20)]
    [InlineData(Full - 1)]
    [InlineData(Full)]
    public void AnAnimeNotOnMyListHasFullWeightDownToTheSetRank(int rank)
    {
        Assert.Equal(1, GameWeights.Popularity(rank, onMyList: false));
    }

    [Fact]
    public void WeightFallsOffSquaredPastTheSetRank()
    {
        // Twice the rank is a quarter of the weight, three times a ninth.
        Assert.Equal(0.25, GameWeights.Popularity(Full * 2, onMyList: false), precision: 10);
        Assert.Equal(1.0 / 9, GameWeights.Popularity(Full * 3, onMyList: false), precision: 10);
    }

    [Fact]
    public void WeightOnlyEverFallsAsRankGrows()
    {
        var previous = double.MaxValue;
        for (var rank = 1; rank <= 30_000; rank += 7)
        {
            var weight = GameWeights.Popularity(rank, onMyList: false);
            Assert.True(weight <= previous, $"rank {rank} weighs more than the one before it");
            previous = weight;
        }
    }

    [Theory]
    [InlineData(10_000)]
    [InlineData(24_943)]
    [InlineData(1_000_000)]
    public void TheFloorKeepsTheLeastPopularPossible(int rank)
    {
        var weight = GameWeights.Popularity(rank, onMyList: false);

        Assert.Equal(Floor, weight);
        Assert.True(weight > 0);
    }

    [Fact]
    public void TheFloorIsReachedOnlyOnceTheSquareWouldGoUnderIt()
    {
        // The rank where (Full / rank)² crosses the floor.
        var crossing = (int)Math.Ceiling(Full / Math.Sqrt(Floor));

        Assert.True(GameWeights.Popularity(crossing - 100, onMyList: false) > Floor);
        Assert.Equal(Floor, GameWeights.Popularity(crossing, onMyList: false));
    }

    [Fact]
    public void ANullRankTakesTheFloorUnlessTheAnimeIsOnMyList()
    {
        Assert.Equal(Floor, GameWeights.Popularity(null, onMyList: false));
    }

    // ---- freshness ----

    private const int C = 100;

    [Theory]
    [InlineData(C)]
    [InlineData(C + 1)]
    [InlineData(5_000)]
    public void AnAnimeOutOfTheWindowHasFullWeight(int roundsAgo)
    {
        Assert.Equal(1, GameWeights.Freshness(roundsAgo, C));
    }

    [Fact]
    public void TheCardJustShownIsAlmostImpossible()
    {
        var weight = GameWeights.Freshness(0, C);

        Assert.True(weight > 0);
        Assert.True(weight < 0.001);
    }

    [Fact]
    public void WeightRecoversGraduallyAcrossTheWindow()
    {
        var previous = 0.0;
        for (var roundsAgo = 0; roundsAgo < C; roundsAgo++)
        {
            var weight = GameWeights.Freshness(roundsAgo, C);
            Assert.InRange(weight, previous, 1);
            Assert.True(weight > previous, $"{roundsAgo} rounds ago recovers nothing over the one before");
            previous = weight;
        }
    }

    [Fact]
    public void WeightNearlyReachesFullAtTheEndOfTheWindowAndIsFullBeyondIt()
    {
        var lastInside = GameWeights.Freshness(C - 1, C);

        Assert.True(lastInside > 0.97);
        Assert.True(lastInside < 1);
        Assert.Equal(1, GameWeights.Freshness(C, C));
    }

    [Fact]
    public void RecoveryIsSlowThenQuick()
    {
        // A square: the first half of the window recovers less than the second.
        var half = GameWeights.Freshness(C / 2, C);

        Assert.True(half < 0.5);
    }

    [Fact]
    public void ACooldownOfOneHoldsBackOnlyTheLastCard()
    {
        Assert.Equal(0.25, GameWeights.Freshness(0, 1), precision: 10);
        Assert.Equal(1, GameWeights.Freshness(1, 1));
    }

    // ---- the cooldown's length ----

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(7)]
    public void ACooldownNeverFallsBelowOne(int poolSize)
    {
        Assert.Equal(GameWeights.MinCooldown, GameWeights.Cooldown(poolSize));
    }

    [Theory]
    [InlineData(8, 2)]
    [InlineData(20, 5)]
    [InlineData(586, 146)]
    [InlineData(1_599, 399)]
    public void ACooldownIsAQuarterOfThePool(int poolSize, int expected)
    {
        Assert.Equal(expected, GameWeights.Cooldown(poolSize));
    }

    [Theory]
    [InlineData(1_600)]
    [InlineData(11_525)]
    [InlineData(5_000_000)]
    public void ACooldownNeverPassesFourHundred(int poolSize)
    {
        Assert.Equal(GameWeights.MaxCooldown, GameWeights.Cooldown(poolSize));
    }

    // ---- how long ago ----

    [Fact]
    public void TheLatestShownIsZeroRoundsAgo()
    {
        var roundsAgo = GameWeights.RoundsAgo([30, 20, 10]);

        Assert.Equal(0, roundsAgo[30]);
        Assert.Equal(1, roundsAgo[20]);
        Assert.Equal(2, roundsAgo[10]);
    }

    [Fact]
    public void AnAnimeShownTwiceCountsFromItsLatestShowing()
    {
        // Newest first: 7 was shown last, and again three showings before that.
        var roundsAgo = GameWeights.RoundsAgo([7, 8, 9, 7]);

        Assert.Equal(0, roundsAgo[7]);
        Assert.Equal(1, roundsAgo[8]);
        Assert.Equal(2, roundsAgo[9]);
        Assert.Equal(3, roundsAgo.Count);
    }

    [Fact]
    public void NothingShownIsNothingAgo()
    {
        Assert.Empty(GameWeights.RoundsAgo([]));
    }
}
