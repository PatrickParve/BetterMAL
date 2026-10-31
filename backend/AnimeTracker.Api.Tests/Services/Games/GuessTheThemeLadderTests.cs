using AnimeTracker.Api.Services.Games;

namespace AnimeTracker.Api.Tests.Services.Games;

// add-anime-themes-and-guess-the-theme task 5.3: the ladder is a constant, the one place the
// six lengths live. The board reads them from the game, so these pin the values the spec names.
public class GuessTheThemeLadderTests
{
    [Fact]
    public void TheSixLengthsAreThoseTheSpecNames()
    {
        Assert.Equal([0.1, 0.5, 1, 3, 5, 10], GuessTheThemeLadder.Seconds);
    }

    [Fact]
    public void ThereAreSixAndTheyGetLonger()
    {
        Assert.Equal(6, GuessTheThemeLadder.Steps);
        for (var i = 1; i < GuessTheThemeLadder.Steps; i++)
            Assert.True(GuessTheThemeLadder.Seconds[i] > GuessTheThemeLadder.Seconds[i - 1]);
    }

    [Fact]
    public void TheLastStepIsTheTenSecondRung()
    {
        Assert.Equal(5, GuessTheThemeLadder.LastStep);
        Assert.Equal(10, GuessTheThemeLadder.LengthOf(GuessTheThemeLadder.LastStep));
    }

    [Theory]
    [InlineData(0, 0.1)]
    [InlineData(1, 0.5)]
    [InlineData(2, 1)]
    [InlineData(3, 3)]
    [InlineData(4, 5)]
    [InlineData(5, 10)]
    public void EachStepIsItsLength(int step, double seconds)
    {
        Assert.Equal(seconds, GuessTheThemeLadder.LengthOf(step));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(5, true)]
    [InlineData(-1, false)]
    [InlineData(6, false)]
    public void OnlyTheSixRungsAreSteps(int step, bool expected)
    {
        Assert.Equal(expected, GuessTheThemeLadder.IsStep(step));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(6)]
    public void AStepOffTheLadderHasNoLength(int step)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GuessTheThemeLadder.LengthOf(step));
    }
}
