namespace AnimeTracker.Api.Services.Games;

/// <summary>The streaks Higher or Lower celebrates (add-games-higher-or-lower
/// design.md D17): 10, 25, 50, 100, 250, 500, 1,000 and on, the same three steps
/// ten times larger each time, with no last one. The client only draws the level
/// it is sent.</summary>
public static class HigherOrLowerMilestones
{
    private const long First = 10;

    /// <summary>A milestone's place in the sequence (10 is 0, 25 is 1, 50 is 2,
    /// 100 is 3, 250 is 4 and so on), or null when the streak isn't one. The
    /// level sets how much confetti there is, so each milestone is bigger than
    /// the one before.</summary>
    public static int? Level(int streak)
    {
        var level = 0;
        for (var decade = First; decade <= streak; decade *= 10)
        {
            // decade × 1, × 2.5 and × 5; decade is a multiple of 10, so 2.5 is whole.
            if (streak == decade) return level;
            if (streak == decade * 5 / 2) return level + 1;
            if (streak == decade * 5) return level + 2;
            level += 3;
        }

        return null;
    }
}
