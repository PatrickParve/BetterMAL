namespace AnimeTracker.Api.Services.Games;

/// <summary>How hard Guess the Anime's draw leans on popularity
/// (add-guess-the-anime design.md D5). Higher or Lower only asks me to compare two
/// anime, so a deep cut is a fair card there; here I have to name it, so the curve
/// is much steeper. Freshness and the cooldown are <see cref="GameWeights"/>'s,
/// shared with every game. The constants are named so the tuning lives in one
/// place; the tests pin the curve's shape rather than these exact values.</summary>
public static class GuessTheAnimeWeights
{
    /// <summary>An anime not on my list carries full popularity weight down to
    /// this rank.</summary>
    public const int FullWeightRank = 1000;

    /// <summary>How steeply the weight falls past <see cref="FullWeightRank"/>:
    /// <c>(FullWeightRank / rank)</c> to this power. Higher or Lower's is 2.</summary>
    public const int Steepness = 4;

    /// <summary>The least popularity weight an anime can have, so a deep cut stays
    /// possible: about one answer in 2,000 over the whole pool, not never.</summary>
    public const double PopularityFloor = 0.0001;

    /// <summary>1 for anime on my list, however unpopular, and down to
    /// <see cref="FullWeightRank"/>; past it <c>(FullWeightRank / rank)⁴</c>, never
    /// below <see cref="PopularityFloor"/>. A null rank means popularity is
    /// unknown, which is no evidence the anime is familiar, so it takes the floor.
    /// The same shape as <see cref="GameWeights.Popularity"/>, so it can be handed
    /// to <see cref="GamePicker.Pick"/> in its place.</summary>
    public static double Popularity(int? popularityRank, bool onMyList)
    {
        if (onMyList)
            return 1;
        if (popularityRank is not { } rank)
            return PopularityFloor;
        if (rank <= FullWeightRank)
            return 1;

        var ratio = (double)FullWeightRank / rank;
        return Math.Max(PopularityFloor, Math.Pow(ratio, Steepness));
    }
}
