using AnimeTracker.Api.Services.Games;

namespace AnimeTracker.Api.Tests.Services.Games;

// add-guess-the-anime design D5: the curve is pure, so each case is a plain unit
// test of its shape. The constants are named so they can be tuned, so the tests
// read them rather than restate their values.
public class GuessTheAnimeWeightsTests
{
    private const int Full = GuessTheAnimeWeights.FullWeightRank;
    private const double Floor = GuessTheAnimeWeights.PopularityFloor;

    [Theory]
    [InlineData(1)]
    [InlineData(500)]
    [InlineData(Full + 1)]
    [InlineData(20_000)]
    [InlineData(24_943)]
    public void AnAnimeOnMyListHasFullWeightHoweverUnpopular(int rank)
    {
        Assert.Equal(1, GuessTheAnimeWeights.Popularity(rank, onMyList: true));
    }

    [Fact]
    public void AnAnimeOnMyListHasFullWeightWithNoRankAtAll()
    {
        Assert.Equal(1, GuessTheAnimeWeights.Popularity(null, onMyList: true));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(250)]
    [InlineData(Full - 1)]
    [InlineData(Full)]
    public void AnAnimeNotOnMyListHasFullWeightDownToTheFullWeightRank(int rank)
    {
        Assert.Equal(1, GuessTheAnimeWeights.Popularity(rank, onMyList: false));
    }

    [Fact]
    public void TheFullWeightRankIsOneThousand()
    {
        // The spec says "down to popularity rank 1,000", so this one is pinned.
        Assert.Equal(1_000, Full);
        Assert.Equal(1, GuessTheAnimeWeights.Popularity(1_000, onMyList: false));
        Assert.True(GuessTheAnimeWeights.Popularity(1_001, onMyList: false) < 1);
    }

    [Fact]
    public void WeightFallsAsRankGetsWorsePastTheFullWeightRank()
    {
        var previous = 1.0;
        foreach (var rank in new[] { Full + 1, Full + 100, 1_500, 2_000, 3_000 })
        {
            var weight = GuessTheAnimeWeights.Popularity(rank, onMyList: false);
            Assert.True(weight < previous, $"rank {rank} should weigh less than the one before it");
            previous = weight;
        }
    }

    [Theory]
    [InlineData(1_500)]
    [InlineData(2_000)]
    [InlineData(3_000)]
    [InlineData(5_000)]
    public void PastTheFullWeightRankItFallsFarSteeperThanHigherOrLowers(int rank)
    {
        var guess = GuessTheAnimeWeights.Popularity(rank, onMyList: false);
        var higherOrLower = GameWeights.Popularity(rank, onMyList: false);

        Assert.True(guess < higherOrLower / 2, $"rank {rank}: {guess} should be far below {higherOrLower}");
    }

    [Fact]
    public void ItAlwaysKeepsSomeWeight()
    {
        foreach (var rank in new[] { 5_000, 10_000, 24_943, 1_000_000 })
        {
            var weight = GuessTheAnimeWeights.Popularity(rank, onMyList: false);

            Assert.True(weight >= Floor);
            Assert.True(weight > 0);
        }
    }

    [Fact]
    public void ADeepCutSitsOnTheFloor()
    {
        Assert.Equal(Floor, GuessTheAnimeWeights.Popularity(1_000_000, onMyList: false));
    }

    [Fact]
    public void ANullRankTakesTheFloor()
    {
        Assert.Equal(Floor, GuessTheAnimeWeights.Popularity(null, onMyList: false));
    }

    [Fact]
    public void TheFloorIsAboveZeroAndBelowEveryOtherWeightWorthMentioning()
    {
        Assert.True(Floor > 0);
        Assert.True(Floor < GuessTheAnimeWeights.Popularity(3_000, onMyList: false));
    }

    [Fact]
    public void ItCanBeHandedToThePickerInPlaceOfHigherOrLowers()
    {
        Func<int?, bool, double> curve = GuessTheAnimeWeights.Popularity;

        Assert.Equal(1, curve(10, false));
    }
}
