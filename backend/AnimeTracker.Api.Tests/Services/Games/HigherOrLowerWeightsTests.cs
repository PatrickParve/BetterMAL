using AnimeTracker.Api.Services.Games;

namespace AnimeTracker.Api.Tests.Services.Games;

// add-games-higher-or-lower design D5/D6: both factors are pure, so each case is
// a plain unit test of the curve's shape. The constants are named so they can be
// tuned, so the tests read them rather than restate their values.
public class HigherOrLowerWeightsTests
{
    private const int Full = HigherOrLowerWeights.FullWeightRank;
    private const double Floor = HigherOrLowerWeights.PopularityFloor;

    // ---- popularity ----

    [Theory]
    [InlineData(1)]
    [InlineData(500)]
    [InlineData(Full + 1)]
    [InlineData(20_000)]
    [InlineData(24_943)]
    public void AnAnimeOnMyListHasFullWeightHoweverUnpopular(int rank)
    {
        Assert.Equal(1, HigherOrLowerWeights.Popularity(rank, onMyList: true));
    }

    [Fact]
    public void AnAnimeOnMyListHasFullWeightWithNoRankAtAll()
    {
        Assert.Equal(1, HigherOrLowerWeights.Popularity(null, onMyList: true));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(20)]
    [InlineData(Full - 1)]
    [InlineData(Full)]
    public void AnAnimeNotOnMyListHasFullWeightDownToTheSetRank(int rank)
    {
        Assert.Equal(1, HigherOrLowerWeights.Popularity(rank, onMyList: false));
    }

    [Fact]
    public void WeightFallsOffSquaredPastTheSetRank()
    {
        // Twice the rank is a quarter of the weight, three times a ninth.
        Assert.Equal(0.25, HigherOrLowerWeights.Popularity(Full * 2, onMyList: false), precision: 10);
        Assert.Equal(1.0 / 9, HigherOrLowerWeights.Popularity(Full * 3, onMyList: false), precision: 10);
    }

    [Fact]
    public void WeightOnlyEverFallsAsRankGrows()
    {
        var previous = double.MaxValue;
        for (var rank = 1; rank <= 30_000; rank += 7)
        {
            var weight = HigherOrLowerWeights.Popularity(rank, onMyList: false);
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
        var weight = HigherOrLowerWeights.Popularity(rank, onMyList: false);

        Assert.Equal(Floor, weight);
        Assert.True(weight > 0);
    }

    [Fact]
    public void TheFloorIsReachedOnlyOnceTheSquareWouldGoUnderIt()
    {
        // The rank where (Full / rank)² crosses the floor.
        var crossing = (int)Math.Ceiling(Full / Math.Sqrt(Floor));

        Assert.True(HigherOrLowerWeights.Popularity(crossing - 100, onMyList: false) > Floor);
        Assert.Equal(Floor, HigherOrLowerWeights.Popularity(crossing, onMyList: false));
    }

    [Fact]
    public void ANullRankTakesTheFloorUnlessTheAnimeIsOnMyList()
    {
        Assert.Equal(Floor, HigherOrLowerWeights.Popularity(null, onMyList: false));
    }

    // ---- freshness ----

    private const int C = 100;

    [Theory]
    [InlineData(C)]
    [InlineData(C + 1)]
    [InlineData(5_000)]
    public void AnAnimeOutOfTheWindowHasFullWeight(int roundsAgo)
    {
        Assert.Equal(1, HigherOrLowerWeights.Freshness(roundsAgo, C));
    }

    [Fact]
    public void TheCardJustShownIsAlmostImpossible()
    {
        var weight = HigherOrLowerWeights.Freshness(0, C);

        Assert.True(weight > 0);
        Assert.True(weight < 0.001);
    }

    [Fact]
    public void WeightRecoversGraduallyAcrossTheWindow()
    {
        var previous = 0.0;
        for (var roundsAgo = 0; roundsAgo < C; roundsAgo++)
        {
            var weight = HigherOrLowerWeights.Freshness(roundsAgo, C);
            Assert.InRange(weight, previous, 1);
            Assert.True(weight > previous, $"{roundsAgo} rounds ago recovers nothing over the one before");
            previous = weight;
        }
    }

    [Fact]
    public void WeightNearlyReachesFullAtTheEndOfTheWindowAndIsFullBeyondIt()
    {
        var lastInside = HigherOrLowerWeights.Freshness(C - 1, C);

        Assert.True(lastInside > 0.97);
        Assert.True(lastInside < 1);
        Assert.Equal(1, HigherOrLowerWeights.Freshness(C, C));
    }

    [Fact]
    public void RecoveryIsSlowThenQuick()
    {
        // A square: the first half of the window recovers less than the second.
        var half = HigherOrLowerWeights.Freshness(C / 2, C);

        Assert.True(half < 0.5);
    }

    [Fact]
    public void ACooldownOfOneHoldsBackOnlyTheLastCard()
    {
        Assert.Equal(0.25, HigherOrLowerWeights.Freshness(0, 1), precision: 10);
        Assert.Equal(1, HigherOrLowerWeights.Freshness(1, 1));
    }

    // ---- the cooldown's length ----

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(7)]
    public void ACooldownNeverFallsBelowOne(int poolSize)
    {
        Assert.Equal(HigherOrLowerWeights.MinCooldown, HigherOrLowerWeights.Cooldown(poolSize));
    }

    [Theory]
    [InlineData(8, 2)]
    [InlineData(20, 5)]
    [InlineData(586, 146)]
    [InlineData(1_599, 399)]
    public void ACooldownIsAQuarterOfThePool(int poolSize, int expected)
    {
        Assert.Equal(expected, HigherOrLowerWeights.Cooldown(poolSize));
    }

    [Theory]
    [InlineData(1_600)]
    [InlineData(11_525)]
    [InlineData(5_000_000)]
    public void ACooldownNeverPassesFourHundred(int poolSize)
    {
        Assert.Equal(HigherOrLowerWeights.MaxCooldown, HigherOrLowerWeights.Cooldown(poolSize));
    }
}
