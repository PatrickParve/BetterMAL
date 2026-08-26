using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Services.Ranking;

/// <summary>One computed ranking (design.md D1: derived, never stored) —
/// every anime of mine that qualifies, in order, each carrying its 1-based
/// overall rank. <see cref="RankByAnimeId"/> answers "what rank does this
/// anime hold" for anime the ranking covers; an anime absent from it has no
/// rank at all, per the anime-ranking capability's coverage
/// requirement.</summary>
public class AnimeRankingSnapshot
{
    private readonly Dictionary<int, int> rankByAnimeId;

    private AnimeRankingSnapshot(List<UserAnimeEntry> rankedEntries, Dictionary<int, int> rankByAnimeId)
    {
        RankedEntries = rankedEntries;
        this.rankByAnimeId = rankByAnimeId;
    }

    /// <summary>Every ranked (band 0-2) anime, best first.</summary>
    public List<UserAnimeEntry> RankedEntries { get; }

    public int? RankOf(int animeId) => rankByAnimeId.TryGetValue(animeId, out var rank) ? rank : null;

    /// <summary>Pure: takes whatever entry list and stored order the caller
    /// already has in hand, so any caller already holding the entries can
    /// build a snapshot without a further database round trip (design.md
    /// D1/tasks.md 1.3).</summary>
    public static AnimeRankingSnapshot Build(IReadOnlyList<UserAnimeEntry> entries, IReadOnlyList<int> storedOrder)
    {
        var positionByAnimeId = storedOrder
            .Select((animeId, index) => (animeId, index))
            .ToDictionary(x => x.animeId, x => x.index);

        var rankedEntries = AnimeRankingKey.OrderEntries(
            entries.Where(e => RankBandResolver.Resolve(e) != RankBand.Unranked), positionByAnimeId);

        var rankByAnimeId = rankedEntries
            .Select((entry, index) => (entry.AnimeId, Rank: index + 1))
            .ToDictionary(x => x.AnimeId, x => x.Rank);

        return new AnimeRankingSnapshot(rankedEntries, rankByAnimeId);
    }
}
