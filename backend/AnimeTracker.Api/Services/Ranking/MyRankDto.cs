namespace AnimeTracker.Api.Services.Ranking;

/// <summary>Where one anime or one series stands in my ranking, and how many
/// the ranking holds (add-my-rank-badges design.md D3/D4) — what the rank badge
/// on the anime detail page and on the series page draws, and what the two
/// <c>my-rank</c> endpoints answer. <c>Rank</c> is the 1-based place, null when
/// it holds none (not in my list, unscored, Plan to watch or unaired for an
/// anime; no my-average or not listed for a series). <c>RankedCount</c> is the
/// size of the whole ranking either way, so a caller never needs a second
/// read for the "of M".</summary>
public record MyRankDto(int? Rank, int RankedCount)
{
    /// <summary>An anime's rank in the one anime ranking
    /// (<see cref="AnimeRankingSnapshot.RankOf"/>): dropped anime and Music/CM/PV
    /// entries hold one like any other, per the anime-ranking capability's
    /// coverage requirement.</summary>
    public static MyRankDto From(AnimeRankingSnapshot snapshot, int animeId) =>
        new(snapshot.RankOf(animeId), snapshot.RankedEntries.Count);
}
