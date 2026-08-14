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

            var malAverage = SeriesAverages.Mal(mainLine.Select(m => m.MalScore));
            var mineAverage = SeriesAverages.Mine(mainLine.Select(m => m.MyScore));

            // Main-series reveal rule (design.md decision 5): completed by me
            // and nothing in the main line is currently airing.
            var mainLineAiring = mainLine.Any(m => m.AiringStatus == "currently_airing");
            var mainLineCompletedByMe = SeriesAverages.MainLineCompletedByMe(
                mainLine.Select(m => (m.AiringStatus, m.EntryStatus)));
            var malRevealed = mainLineCompletedByMe && !mainLineAiring;

            results.Add(new SeriesRankingResult(
                group.Key, root.RootAnimeId, root.Title, root.EnglishTitle, root.PictureUrl,
                members.Count, malAverage, mineAverage, malRevealed));
        }

        return results;
    }
}

/// <summary>One series' worth of Top series data: display fields from the
/// root member, its total member count (main line and extras), and its two
/// main-series averages with the MAL reveal boolean (design.md decision
/// 5).</summary>
public sealed record SeriesRankingResult(
    int SeriesId,
    int RootAnimeId,
    string Title,
    string? EnglishTitle,
    string? PictureUrl,
    int EntryCount,
    SeriesAverageDto MalMain,
    SeriesAverageDto MineMain,
    bool MalRevealed);
