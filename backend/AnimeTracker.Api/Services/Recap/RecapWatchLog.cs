using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Profile;

namespace AnimeTracker.Api.Services.Recap;

/// <summary>Turns a period's raw <see
/// cref="ActivityChangeType.EpisodeIncremented"/> rows into a per-anime
/// in-period episode count (design.md decision 5): the sum of each row's
/// <em>positive</em> increase, discarding decreases and rows whose episode
/// number or prior value can't be read. Summing increases — rather than
/// `max - start` across the period — is what lets a rewatch that resets the
/// counter to zero and climbs again count as a second viewing: a full watch
/// (+12) followed by a reset and a rewatch (+12) sums to 24, where
/// `max - start` would read 0.</summary>
public static class RecapWatchLog
{
    public static Dictionary<int, int> BuildMap(List<ActivityLog> rows)
    {
        var map = new Dictionary<int, int>();

        foreach (var (row, increase) in PositiveIncreases(rows))
            map[row.AnimeId] = map.GetValueOrDefault(row.AnimeId) + increase;

        return map;
    }

    /// <summary>The rows that count, each with its positive episode increase: the
    /// one place that decides which logged rows are genuine progress. The per-anime
    /// map above and the taste stats' biggest day both read it, so the two can
    /// never disagree about how many episodes a period holds (taste-stats design.md
    /// D6).</summary>
    public static IEnumerable<(ActivityLog Row, int Increase)> PositiveIncreases(IEnumerable<ActivityLog> rows)
    {
        foreach (var row in rows)
        {
            if (ProfileService.ParseNewEpisodesWatched(row) is not { } newEpisodesWatched
                || row.PreviousEpisodesWatched is not { } previous)
                continue;

            var increase = newEpisodesWatched - previous;
            if (increase <= 0)
                continue;

            yield return (row, increase);
        }
    }
}
