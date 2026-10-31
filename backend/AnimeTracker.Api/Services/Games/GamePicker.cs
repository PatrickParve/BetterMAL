namespace AnimeTracker.Api.Services.Games;

/// <summary>What the draw needs to know about one eligible anime: the four
/// columns the pool is projected to, not the whole row.</summary>
public record PickCandidate(int AnimeId, double Score, int? PopularityRank, bool OnMyList);

/// <summary>The weighted random draw behind every anime a game shows
/// (add-games-higher-or-lower design.md D5-D7). Pure apart from the
/// <see cref="Random"/> it is handed, so a seeded one makes it repeatable.</summary>
public static class GamePicker
{
    /// <summary>Two scores are the same when they match at the two decimals the
    /// player sees, so floating-point noise can't make two shown 7.85s differ.
    /// Compared as whole hundredths, so no rounded double is compared for
    /// equality.</summary>
    public static bool SameScore(double a, double b) => Hundredths(a) == Hundredths(b);

    public static long Hundredths(double score) =>
        (long)Math.Round(score * 100, MidpointRounding.AwayFromZero);

    /// <summary>Draws one anime from <paramref name="pool"/>, or null with none
    /// left. An anime in <paramref name="usedInGame"/> is out, as is any whose
    /// score matches <paramref name="avoidScore"/> to two decimals (the score of
    /// the anime that stays, or null for the game's first anime). The rest are
    /// weighted popularity × freshness: <paramref name="popularity"/> is the
    /// game's own curve, taking a popularity rank and whether the anime is on my
    /// list; <paramref name="roundsAgo"/> maps an anime to how many cards ago it
    /// was last shown (0 for the latest), and an anime missing from it was not
    /// shown recently. The cooldown's length follows the pool's size, so the
    /// whole pool is passed, not just what is still eligible.</summary>
    public static PickCandidate? Pick(
        IReadOnlyList<PickCandidate> pool,
        IReadOnlySet<int> usedInGame,
        double? avoidScore,
        IReadOnlyDictionary<int, int> roundsAgo,
        Func<int?, bool, double> popularity,
        Random random)
    {
        var cooldown = GameWeights.Cooldown(pool.Count);
        var weights = new double[pool.Count];
        var total = 0.0;

        for (var i = 0; i < pool.Count; i++)
        {
            var candidate = pool[i];
            if (usedInGame.Contains(candidate.AnimeId))
                continue;
            if (avoidScore is { } avoid && SameScore(candidate.Score, avoid))
                continue;

            var weight = popularity(candidate.PopularityRank, candidate.OnMyList);
            if (roundsAgo.TryGetValue(candidate.AnimeId, out var ago))
                weight *= GameWeights.Freshness(ago, cooldown);

            weights[i] = weight;
            total += weight;
        }

        if (total <= 0)
            return null;

        var target = random.NextDouble() * total;
        var lastEligible = -1;
        for (var i = 0; i < weights.Length; i++)
        {
            if (weights[i] <= 0)
                continue;

            lastEligible = i;
            target -= weights[i];
            if (target < 0)
                return pool[i];
        }

        // Rounding left a sliver of the target over: the last one eligible takes it.
        return pool[lastEligible];
    }
}
