using AnimeTracker.Api.Services.Watching;

namespace AnimeTracker.Api.Services.Series;

/// <summary>In-memory index of every series member, loaded once per Top
/// series read by SeriesRankingLookup. Eligibility, the two main-series
/// averages, and the MAL reveal rule are all computed here, over data already
/// in memory rather than in further queries (mirrors SeriesSearchIndex).</summary>
public sealed class SeriesRankingIndex
{
    public static SeriesRankingIndex Empty { get; } = new([]);

    private readonly ILookup<int, SeriesRankingMemberProjection> _membersBySeriesId;
    private readonly HashSet<int> _memberAnimeIds;

    internal SeriesRankingIndex(List<SeriesRankingMemberProjection> members)
    {
        _membersBySeriesId = members.ToLookup(m => m.SeriesId);
        _memberAnimeIds = members.Select(m => m.AnimeId).ToHashSet();
    }

    /// <summary>True when <paramref name="animeId"/> already belongs to a
    /// stored series — reused by the Top series on-read backfill so it
    /// doesn't issue a second membership query (design.md decision 6/task
    /// 4.1).</summary>
    public bool HasSeries(int animeId) => _memberAnimeIds.Contains(animeId);

    /// <summary>Every series with at least one member — main line or extra —
    /// in my list (design.md decision 2), each carrying its main-series
    /// averages and MAL reveal boolean computed over the main line only.
    /// Unordered; the caller applies whatever ordering it needs.</summary>
    public List<SeriesRankingResult> EligibleSeries()
    {
        var results = new List<SeriesRankingResult>();

        foreach (var group in _membersBySeriesId)
        {
            var members = group.ToList();
            if (!members.Any(m => m.EntryStatus is not null))
                continue; // no member of this series is in my list (task 2.5)

            // The root is always a member of its own series (SeriesGraphBuilder
            // invariant), so display fields come from that row.
            var root = members.First(m => m.AnimeId == m.RootAnimeId);
            var mainLine = members.Where(m => m.IsMainLine).ToList();

            // An announced main-line entry that hasn't started airing yet has
            // no episodes out, so it shouldn't count toward "this is a
            // multi-entry franchise" for the Top series filter — only
            // currently_airing/finished_airing main-line members do.
            var mainLineAired = mainLine.Where(m => m.AiringStatus != "not_yet_aired").ToList();
            var mainLineAiredCount = mainLineAired.Count;

            // Watched-coverage rule (design.md decisions 6/7): a franchise
            // whose main line has two or more aired entries needs at least
            // two of them in my list — any status, plan-to-watch included —
            // or its averages would rank a whole franchise on a single
            // entry's worth of my viewing.
            var mainLineAiredInList = mainLineAired.Count(m => m.EntryStatus is not null);
            if (mainLineAiredCount >= 2 && mainLineAiredInList <= 1)
                continue;

            var malAverage = SeriesAverages.Mal(mainLine.Select(m => m.MalScore));
            var mineAverage = SeriesAverages.Mine(mainLine.Select(m => m.MyScore));

            // Main-series reveal rule (design.md decision 5): settled by me
            // (completed or dropped) and nothing in the main line is
            // currently airing.
            var mainLineAiring = mainLine.Any(m => m.AiringStatus == "currently_airing");
            var mainLineSettledByMe = SeriesAverages.MainLineSettledByMe(
                mainLine.Select(m => (m.AiringStatus, m.EntryStatus)));
            var malRevealed = mainLineSettledByMe && !mainLineAiring;

            results.Add(new SeriesRankingResult(
                group.Key, root.RootAnimeId, root.Title, root.EnglishTitle, root.PictureUrl,
                members.Count, mainLineAiredCount, malAverage, mineAverage, malRevealed));
        }

        return results;
    }

    /// <summary>Every series with above-zero total rewatch time, ranked by
    /// that total descending then title (design.md D9) — summed over
    /// <em>every</em> member, main line and extras alike, since the question
    /// is how much time the franchise as a whole has taken back. First
    /// watches never count: a member with no recorded rewatch contributes
    /// nothing. Deliberately does not apply EligibleSeries()'s
    /// two-aired-main-line-entries coverage rule — that rule exists because
    /// EligibleSeries() ranks by an average, which one entry would
    /// misrepresent; a sum has no such problem, so a franchise where only
    /// one entry has ever been rewatched is still listed with a total
    /// that's exactly right.</summary>
    public List<SeriesRewatchResult> RewatchedSeries()
    {
        var results = new List<SeriesRewatchResult>();

        foreach (var group in _membersBySeriesId)
        {
            var members = group.ToList();
            var totalSeconds = members.Sum(MemberRewatchSeconds);
            if (totalSeconds <= 0)
                continue;

            var root = members.First(m => m.AnimeId == m.RootAnimeId);
            results.Add(new SeriesRewatchResult(
                group.Key, root.RootAnimeId, root.Title, root.EnglishTitle, root.PictureUrl, totalSeconds));
        }

        return results
            .OrderByDescending(r => r.RewatchSeconds)
            .ThenBy(r => r.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // Kept in step with WatchMath rather than restating its fallbacks
    // (RewatchOnlyEpisodes' published-total baseline, EpisodeSeconds' 24
    // minutes) — task 9.3. A member not in my list (both fields null) or
    // never rewatched contributes nothing.
    private static long MemberRewatchSeconds(SeriesRankingMemberProjection m)
    {
        if (m.RewatchCount is not { } rewatchCount || rewatchCount <= 0)
            return 0;

        var rewatchEpisodes = WatchMath.RewatchOnlyEpisodes(rewatchCount, m.TotalEpisodes, m.EpisodesWatched ?? 0);
        return (long)rewatchEpisodes * WatchMath.EpisodeSeconds(m.AverageEpisodeDurationSeconds);
    }
}

/// <summary>One series' worth of Top series data: display fields from the
/// root member, its total member count (main line and extras), its
/// main-line count that has actually started airing, and its two
/// main-series averages with the MAL reveal boolean (design.md decision
/// 5).</summary>
public sealed record SeriesRankingResult(
    int SeriesId,
    int RootAnimeId,
    string Title,
    string? EnglishTitle,
    string? PictureUrl,
    int EntryCount,
    int MainLineAiredCount,
    SeriesAverageDto MalMain,
    SeriesAverageDto MineMain,
    bool MalRevealed);

/// <summary>One series' worth of rewatch-time data: display fields from the
/// root member and the total rewatch time summed across every member
/// (design.md D9).</summary>
public sealed record SeriesRewatchResult(
    int SeriesId,
    int RootAnimeId,
    string Title,
    string? EnglishTitle,
    string? PictureUrl,
    long RewatchSeconds);
