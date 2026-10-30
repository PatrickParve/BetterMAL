using AnimeTracker.Api.Services.Games;

namespace AnimeTracker.Api.Tests.Services.Games;

// add-games-higher-or-lower design D17: Level is pure, so the whole sequence is
// pinned here and the client only draws the level it is sent.
public class HigherOrLowerMilestonesTests
{
    // 10, 25, 50, then each ten times larger, to a million.
    private static readonly (int Streak, int Level)[] Sequence =
    [
        (10, 0), (25, 1), (50, 2),
        (100, 3), (250, 4), (500, 5),
        (1_000, 6), (2_500, 7), (5_000, 8),
        (10_000, 9), (25_000, 10), (50_000, 11),
        (100_000, 12), (250_000, 13), (500_000, 14),
        (1_000_000, 15),
    ];

    public static TheoryData<int, int> Milestones()
    {
        var data = new TheoryData<int, int>();
        foreach (var (streak, level) in Sequence)
            data.Add(streak, level);
        return data;
    }

    [Theory]
    [MemberData(nameof(Milestones))]
    public void EveryMilestoneHasItsLevel(int streak, int level)
    {
        Assert.Equal(level, HigherOrLowerMilestones.Level(streak));
    }

    [Fact]
    public void TheLevelsCountUpByOneWithNoneSkipped()
    {
        Assert.Equal(Enumerable.Range(0, Sequence.Length), Sequence.Select(s => s.Level));
    }

    public static TheoryData<int> NeighboursOfEveryMilestone()
    {
        var data = new TheoryData<int>();
        foreach (var (streak, _) in Sequence)
        {
            data.Add(streak - 1);
            data.Add(streak + 1);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(NeighboursOfEveryMilestone))]
    public void TheNumberEitherSideOfAMilestoneIsNotOne(int streak)
    {
        Assert.Null(HigherOrLowerMilestones.Level(streak));
    }

    [Fact]
    public void NoStreakBelowTenIsAMilestone()
    {
        for (var streak = -3; streak < 10; streak++)
            Assert.Null(HigherOrLowerMilestones.Level(streak));
    }

    [Fact]
    public void ZeroIsNotAMilestone()
    {
        Assert.Null(HigherOrLowerMilestones.Level(0));
    }

    [Theory]
    [InlineData(75)]
    [InlineData(150)]
    [InlineData(300)]
    [InlineData(750)]
    [InlineData(1_500)]
    [InlineData(20)]
    [InlineData(30)]
    [InlineData(200)]
    public void BetweenTheStepsIsNotAMilestone(int streak)
    {
        Assert.Null(HigherOrLowerMilestones.Level(streak));
    }

    [Fact]
    public void ThereAreExactlyThreeMilestonesBelowAHundredAndSixBelowAThousand()
    {
        var below100 = Enumerable.Range(0, 100).Count(s => HigherOrLowerMilestones.Level(s) is not null);
        var below1000 = Enumerable.Range(0, 1_000).Count(s => HigherOrLowerMilestones.Level(s) is not null);

        Assert.Equal(3, below100);
        Assert.Equal(6, below1000);
    }

    [Fact]
    public void TheSequenceHasNoEnd()
    {
        // The largest streak an int holds is still answered without overflow.
        Assert.Null(HigherOrLowerMilestones.Level(int.MaxValue));
        Assert.Equal(24, HigherOrLowerMilestones.Level(1_000_000_000));
    }
}
