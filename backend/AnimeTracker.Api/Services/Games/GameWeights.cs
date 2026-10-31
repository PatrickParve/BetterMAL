namespace AnimeTracker.Api.Services.Games;

/// <summary>The two factors of an anime's weight in a game's draw
/// (add-games-higher-or-lower design.md D5 and D6): weight = popularity ×
/// freshness. Freshness and the cooldown are every game's; <see cref="Popularity"/>
/// is Higher or Lower's own curve, which a game with a different one hands the
/// picker instead. The constants are named here so the tuning lives in one place;
/// the tests pin the curves' shape rather than these exact values.</summary>
public static class GameWeights
{
    /// <summary>An anime not on my list carries full popularity weight down to
    /// this rank, about the point where a typical MAL user has heard of most of
    /// what's above it.</summary>
    public const int FullWeightRank = 1500;

    /// <summary>The least popularity weight an anime can have, so the deepest
    /// cuts stay possible: less often, not never.</summary>
    public const double PopularityFloor = 0.03;

    /// <summary>The cooldown spans a quarter of the pool, within these bounds, so
    /// a small pool isn't permanently held back and a huge one doesn't hold back
    /// thousands of anime.</summary>
    public const int PoolShareDivisor = 4;
    public const int MinCooldown = 1;
    public const int MaxCooldown = 400;

    /// <summary>Higher or Lower's curve: 1 for anime on my list, however
    /// unpopular, and down to <see cref="FullWeightRank"/>; past it
    /// <c>(FullWeightRank / rank)²</c>, never below <see cref="PopularityFloor"/>.
    /// A null rank means popularity is unknown, which is no evidence the anime is
    /// familiar, so it takes the floor.</summary>
    public static double Popularity(int? popularityRank, bool onMyList)
    {
        if (onMyList)
            return 1;
        if (popularityRank is not { } rank)
            return PopularityFloor;
        if (rank <= FullWeightRank)
            return 1;

        var ratio = (double)FullWeightRank / rank;
        return Math.Max(PopularityFloor, ratio * ratio);
    }

    /// <summary>1 once an anime is out of the last <paramref name="cooldown"/>
    /// cards shown; inside it <c>((roundsAgo + 1) / (cooldown + 1))²</c>, so the
    /// card just shown (roundsAgo 0) is almost impossible and one nearly out of
    /// the window is almost back to normal. <paramref name="roundsAgo"/> counts
    /// the cards shown since: 0 for the one shown last.</summary>
    public static double Freshness(int roundsAgo, int cooldown)
    {
        if (roundsAgo >= cooldown)
            return 1;

        var ratio = (double)(Math.Max(roundsAgo, 0) + 1) / (cooldown + 1);
        return ratio * ratio;
    }

    /// <summary>How many cards back the cooldown reaches for a pool of this
    /// size: a quarter of it, clamped to
    /// <see cref="MinCooldown"/>–<see cref="MaxCooldown"/>.</summary>
    public static int Cooldown(int poolSize) =>
        Math.Clamp(poolSize / PoolShareDivisor, MinCooldown, MaxCooldown);

    /// <summary>How many shown ago each anime of a list of recent appearances
    /// was, 0 for the latest. <paramref name="latestFirst"/> is the anime ids in
    /// the order they were shown, newest first; an anime shown more than once
    /// counts from its latest showing, so a later duplicate is ignored.</summary>
    public static Dictionary<int, int> RoundsAgo(IReadOnlyList<int> latestFirst)
    {
        var roundsAgo = new Dictionary<int, int>();
        for (var i = 0; i < latestFirst.Count; i++)
            roundsAgo.TryAdd(latestFirst[i], i);
        return roundsAgo;
    }
}
