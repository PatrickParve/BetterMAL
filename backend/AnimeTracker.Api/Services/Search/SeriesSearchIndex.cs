using AnimeTracker.Api.Services.Series;

namespace AnimeTracker.Api.Services.Search;

/// <summary>In-memory index of every series member's title/picture, loaded
/// once per search by SeriesSearchLookup. Matching (design.md decision 1),
/// collapsing to one row per series, and ordering by match quality then
/// popularity (decision 2) all happen here, over data already in memory
/// rather than in further queries.</summary>
public sealed class SeriesSearchIndex
{
    public static SeriesSearchIndex Empty { get; } = new([]);

    private readonly List<SeriesMemberProjection> _members;
    private readonly ILookup<int, SeriesMemberProjection> _membersBySeriesId;
    private readonly HashSet<int> _memberAnimeIds;

    internal SeriesSearchIndex(List<SeriesMemberProjection> members)
    {
        _members = members;
        _membersBySeriesId = members.ToLookup(m => m.SeriesId);
        _memberAnimeIds = members.Select(m => m.AnimeId).ToHashSet();
    }

    /// <summary>True when <paramref name="animeId"/> belongs to a stored
    /// series — the membership check the background-build trigger reuses
    /// (design.md decision 6/task 3.5) instead of issuing a second
    /// query.</summary>
    public bool HasSeries(int animeId) => _memberAnimeIds.Contains(animeId);

    /// <summary>Matched series for the query, ordered by best match quality
    /// (exact, then prefix, then contains) and, as a tie-break, the best
    /// popularity rank among the members that matched (design.md decision
    /// 2).</summary>
    // No empty-normalized-term guard here (design.md D2): this is only ever
    // reached from AnimeSearchService.SearchAsync/SearchPageAsync, both of
    // which already return early before loading the series index when the
    // term normalizes to nothing, so `term` here is never punctuation-only.
    public List<SeriesSearchResultDto> Match(string term, bool exact)
    {
        if (_members.Count == 0)
            return [];

        var bestBySeriesId = new Dictionary<int, (MatchQuality Quality, int PopularityKey)>();
        foreach (var member in _members)
        {
            var quality = MatchQualityOf(member, term, exact);
            if (quality is null)
                continue;

            var popularityKey = SearchTextMatch.PopularityKey(member.PopularityRank);
            if (!bestBySeriesId.TryGetValue(member.SeriesId, out var best) ||
                quality.Value < best.Quality ||
                (quality.Value == best.Quality && popularityKey < best.PopularityKey))
            {
                bestBySeriesId[member.SeriesId] = (quality.Value, popularityKey);
            }
        }

        return bestBySeriesId
            .OrderBy(kv => kv.Value.Quality)
            .ThenBy(kv => kv.Value.PopularityKey)
            .Select(kv => ToResultDto(kv.Key))
            .ToList();
    }

    // The series id is the root's anime id, and the root is always a member
    // of its own series (SeriesGraphBuilder invariant, key-series-by-root-
    // anime-id design.md D6), so display fields come from that row (via
    // SeriesIdentity, design.md D7) regardless of which member actually
    // matched the query.
    private SeriesSearchResultDto ToResultDto(int seriesId)
    {
        var members = _membersBySeriesId[seriesId].ToList();
        var root = members.First(m => m.AnimeId == seriesId);
        var (title, englishTitle, pictureUrl) = SeriesIdentity.Resolve(
            root.SelectedTitle, root.SelectedPictureUrl, root.Title, root.EnglishTitle, root.MalPictureUrl);

        return new SeriesSearchResultDto(seriesId, title, englishTitle, pictureUrl, members.Count);
    }

    // A chosen title is added to the match set alongside every member's own
    // title/English title (spec `series-identity` "A renamed series is still
    // found by its members' titles") — it never *replaces* member matching,
    // since a trimmed title no member title begins with must still be
    // findable by its own text.
    private static MatchQuality? MatchQualityOf(SeriesMemberProjection member, string term, bool exact)
    {
        if (exact)
        {
            return SearchTextMatch.EqualsNormalized(member.Title, term)
                || SearchTextMatch.EqualsNormalized(member.EnglishTitle, term)
                || SearchTextMatch.EqualsNormalized(member.SelectedTitle, term)
                ? MatchQuality.Exact
                : null;
        }

        if (SearchTextMatch.StartsWithNormalized(member.Title, term)
            || SearchTextMatch.StartsWithNormalized(member.EnglishTitle, term)
            || SearchTextMatch.StartsWithNormalized(member.SelectedTitle, term))
            return MatchQuality.Prefix;

        // Whole-word, not raw substring: a series card stands for an entire
        // franchise, so a mid-word fragment inside one obscure member's title
        // (e.g. "miss" inside "...World Heroes' Mission") must not be enough
        // to surface it — unlike the type-ahead's plain anime rows, where a
        // loose substring match is naming that one exact title.
        if (SearchTextMatch.ContainsWholeWord(member.Title, term)
            || SearchTextMatch.ContainsWholeWord(member.EnglishTitle, term)
            || SearchTextMatch.ContainsWholeWord(member.SelectedTitle, term))
            return MatchQuality.Contains;

        return null;
    }

    private enum MatchQuality { Exact = 0, Prefix = 1, Contains = 2 }
}
