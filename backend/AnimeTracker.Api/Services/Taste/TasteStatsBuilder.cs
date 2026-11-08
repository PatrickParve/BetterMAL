using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Ranking;
using AnimeTracker.Api.Services.Recap;
using AnimeTracker.Api.Services.Scheduling;

namespace AnimeTracker.Api.Services.Taste;

/// <summary>The genre and studio figures the recap's highlights and the profile's
/// taste sections draw (the <c>taste-stats</c> capability, design.md D1). Pure: it
/// takes the <b>counted set</b> of entries, the whole list's mean score and my
/// ranking, and does no I/O, so the recap and the profile can never count or order
/// the same genre or studio differently.
/// <para>The caller decides what is counted: every entry but Plan to watch on the
/// profile, the recap's included set on a recap. An entry counts once in a figure
/// however often I have watched it, since the counted set holds one entry per
/// anime.</para>
/// <para>No figure here reads MyAnimeList's community score. <c>wholeListMean</c>
/// is C in the Bayesian formula: my mean score across every scored entry of my
/// whole list, never only the counted set; it is null only when nothing at all is
/// scored, in which case no group has a weighted score either.</para></summary>
public static class TasteStatsBuilder
{
    /// <summary>The trust threshold <c>m</c> for a genre or a genre combination.
    /// Genres are broad, so they need more scored anime before their own mean is
    /// believed (design.md D3).</summary>
    public const int GenreTrustThreshold = 10;

    /// <summary>The trust threshold <c>m</c> for a studio.</summary>
    public const int StudioTrustThreshold = 5;

    /// <summary>A hidden gem is scored this high or higher (design.md D5).</summary>
    public const int HiddenGemMinimumScore = 8;

    /// <summary>A hidden gem's popularity rank is above this: it is not among
    /// MyAnimeList's 1,000 most popular.</summary>
    public const int HiddenGemPopularityFloor = 1000;

    /// <summary>A split names this many categories and merges the rest.</summary>
    public const int SplitNamedCategories = 4;

    /// <summary>The label of a split's merged category.</summary>
    public const string OtherLabel = "other";

    // A combination of one anime is not a combination.
    private const int MinimumCombinationAnime = 2;

    // MyAnimeList's own source value for "some other source". It is folded into
    // the merged category below rather than listed beside it, so the label
    // "other" never names two different things in one legend.
    private const string MalOtherSource = "other";

    private sealed record Group(
        string Name, List<UserAnimeEntry> Entries, List<UserAnimeEntry> Scored, double? Weighted, int[] Histogram);

    // --- Genres and studios ---------------------------------------------------

    /// <summary>Every genre of the counted set, in most-watched order, each
    /// carrying its place in both orders.</summary>
    public static List<TasteGenreDto> Genres(
        IReadOnlyList<UserAnimeEntry> counted, double? wholeListMean, AnimeRankingSnapshot rankingSnapshot) =>
        RankGroups(counted, GenresOf, GenreTrustThreshold, wholeListMean, rankingSnapshot,
            (g, countRank, scoreRank) => new TasteGenreDto(
                g.Name, g.Entries.Count, g.Scored.Count, RoundedScore(g), countRank, scoreRank,
                GroupRanking.TopPosters(g.Entries, rankingSnapshot)));

    /// <summary>Every studio of the counted set, in most-watched order, each
    /// carrying its place in both orders. A studio is the name MyAnimeList credits,
    /// and an anime credited to two studios counts once toward each.</summary>
    public static List<TasteStudioDto> Studios(
        IReadOnlyList<UserAnimeEntry> counted, double? wholeListMean, AnimeRankingSnapshot rankingSnapshot) =>
        RankGroups(counted, StudiosOf, StudioTrustThreshold, wholeListMean, rankingSnapshot,
            (g, countRank, scoreRank) => new TasteStudioDto(
                g.Name, g.Entries.Count, g.Scored.Count, RoundedScore(g), countRank, scoreRank,
                GroupRanking.TopPosters(g.Entries, rankingSnapshot)));

    // Tallies the groups, ranks them in both orders and returns them in the
    // most-watched one. Both orders end on the name, compared ignoring case and then
    // exactly, so the order is the same on every load.
    private static List<T> RankGroups<T>(
        IReadOnlyList<UserAnimeEntry> counted, Func<AnimeMetadata, List<string>> namesOf, int trustThreshold,
        double? wholeListMean, AnimeRankingSnapshot rankingSnapshot, Func<Group, int, int?, T> create)
    {
        var byName = new Dictionary<string, List<UserAnimeEntry>>(StringComparer.Ordinal);
        foreach (var entry in counted)
        {
            foreach (var name in namesOf(entry.Anime))
            {
                if (!byName.TryGetValue(name, out var entries))
                    byName[name] = entries = [];
                entries.Add(entry);
            }
        }

        var groups = byName
            .Select(pair =>
            {
                var scored = pair.Value.Where(e => e.MyScore is not null).ToList();
                double? weighted = scored.Count > 0 && wholeListMean is { } mean
                    ? GroupRanking.WeightedAverage(scored, mean, trustThreshold)
                    : null;
                return new Group(pair.Key, pair.Value, scored, weighted, GroupRanking.BuildHistogram(scored));
            })
            .ToList();

        var mostWatched = groups.ToList();
        mostWatched.Sort(CompareMostWatched);

        // Only a group with a weighted score is rated at all (taste-stats "Highest-rated
        // order"): it has a scored anime, so it is neither given C nor zero in place.
        var highestRated = groups.Where(g => g.Weighted is not null).ToList();
        highestRated.Sort(CompareHighestRated);
        var scoreRankByName = highestRated
            .Select((g, index) => (g.Name, Rank: index + 1))
            .ToDictionary(x => x.Name, x => x.Rank, StringComparer.Ordinal);

        return mostWatched
            .Select((g, index) => create(g, index + 1, scoreRankByName.TryGetValue(g.Name, out var rank) ? rank : null))
            .ToList();
    }

    // Anime count, then weighted score (one with a score ahead of one without), then
    // the name.
    private static int CompareMostWatched(Group a, Group b)
    {
        if (a.Entries.Count != b.Entries.Count)
            return a.Entries.Count > b.Entries.Count ? -1 : 1;

        return CompareWeightedThenNames(a.Weighted, b.Weighted, () => CompareNames(a.Name, b.Name));
    }

    // The season and year rankings' order, with the name in place of recency.
    private static int CompareHighestRated(Group a, Group b) =>
        GroupRanking.CompareGroups(
            a.Weighted!.Value, a.Scored.Count, a.Histogram,
            b.Weighted!.Value, b.Scored.Count, b.Histogram,
            () => CompareNames(a.Name, b.Name));

    private static int CompareWeightedThenNames(double? a, double? b, Func<int> thenBy)
    {
        if (a is { } wa && b is { } wb)
        {
            // Exact equality, no epsilon — see GroupRanking.CompareGroups.
            if (wa != wb)
                return wa > wb ? -1 : 1;
        }
        else if (a is not null)
        {
            return -1;
        }
        else if (b is not null)
        {
            return 1;
        }

        return thenBy();
    }

    private static double? RoundedScore(Group group) =>
        group.Weighted is { } weighted ? Math.Round(weighted, 2) : null;

    // The names MyAnimeList stores under an anime's one list, minus the five
    // demographics (a demographic is never a genre); each once.
    private static List<string> GenresOf(AnimeMetadata anime) =>
        anime.Genres?
            .Where(name => !string.IsNullOrWhiteSpace(name) && !TasteNames.IsDemographic(name))
            .Distinct(StringComparer.Ordinal)
            .ToList() ?? [];

    private static List<string> StudiosOf(AnimeMetadata anime) =>
        anime.Studios?
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .ToList() ?? [];

    // Alphabetical, ignoring case. Two names that differ only in case still order
    // the same way every time, by their exact characters.
    private static int CompareNames(string a, string b)
    {
        var ignoringCase = string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
        return ignoringCase != 0 ? ignoringCase : string.CompareOrdinal(a, b);
    }

    // --- Combinations -----------------------------------------------------------

    /// <summary>The genre pairs carried by at least two anime of the counted set,
    /// most carried first, at most <paramref name="limit"/> of them.</summary>
    public static List<TasteComboDto> Pairs(IReadOnlyList<UserAnimeEntry> counted, double? wholeListMean, int limit) =>
        Combinations(counted, wholeListMean, size: 2, limit);

    /// <summary>The genre trios carried by at least two anime of the counted set,
    /// most carried first, at most <paramref name="limit"/> of them.</summary>
    public static List<TasteComboDto> Trios(IReadOnlyList<UserAnimeEntry> counted, double? wholeListMean, int limit) =>
        Combinations(counted, wholeListMean, size: 3, limit);

    // Ordered by count, then weighted score at the genre threshold, then the genres'
    // names. Ordering by score alone returned nine arrangements of one franchise's
    // seven entries (design.md D4), which is why count leads.
    private static List<TasteComboDto> Combinations(
        IReadOnlyList<UserAnimeEntry> counted, double? wholeListMean, int size, int limit)
    {
        var byKey = new Dictionary<string, (List<string> Genres, List<UserAnimeEntry> Entries)>(StringComparer.Ordinal);

        foreach (var entry in counted)
        {
            var genres = GenresOf(entry.Anime);
            if (genres.Count < size)
                continue;

            // Alphabetical, so the same set of genres is one combination whichever
            // order an anime lists them in.
            genres.Sort(CompareNames);

            foreach (var combination in Choose(genres, size))
            {
                var key = string.Join('\u001f', combination);
                if (!byKey.TryGetValue(key, out var group))
                    byKey[key] = group = (combination, []);
                group.Entries.Add(entry);
            }
        }

        var rows = byKey.Values
            .Where(group => group.Entries.Count >= MinimumCombinationAnime)
            .Select(group =>
            {
                var scored = group.Entries.Where(e => e.MyScore is not null).ToList();
                double? weighted = scored.Count > 0 && wholeListMean is { } mean
                    ? GroupRanking.WeightedAverage(scored, mean, GenreTrustThreshold)
                    : null;
                return (group.Genres, Count: group.Entries.Count, Weighted: weighted);
            })
            .ToList();

        rows.Sort((a, b) =>
        {
            if (a.Count != b.Count)
                return a.Count > b.Count ? -1 : 1;

            return CompareWeightedThenNames(a.Weighted, b.Weighted, () => CompareNameLists(a.Genres, b.Genres));
        });

        return rows.Take(limit).Select(row => new TasteComboDto(row.Genres, row.Count)).ToList();
    }

    // Every way of picking two or three of the genres, in their given order.
    private static IEnumerable<List<string>> Choose(List<string> sorted, int size)
    {
        for (var i = 0; i < sorted.Count; i++)
        {
            for (var j = i + 1; j < sorted.Count; j++)
            {
                if (size == 2)
                {
                    yield return [sorted[i], sorted[j]];
                    continue;
                }

                for (var k = j + 1; k < sorted.Count; k++)
                    yield return [sorted[i], sorted[j], sorted[k]];
            }
        }
    }

    private static int CompareNameLists(List<string> a, List<string> b)
    {
        for (var i = 0; i < Math.Min(a.Count, b.Count); i++)
        {
            var compared = CompareNames(a[i], b[i]);
            if (compared != 0)
                return compared;
        }

        return a.Count.CompareTo(b.Count);
    }

    // --- Hidden gems --------------------------------------------------------------

    /// <summary>The anime of the counted set I scored 8 or higher that are outside
    /// MyAnimeList's 1,000 most popular, least popular first: ties fall to my score,
    /// then my ranking's order, then the title. An anime with no stored popularity
    /// rank does not qualify.</summary>
    public static List<TasteGemDto> HiddenGems(
        IReadOnlyList<UserAnimeEntry> counted, AnimeRankingSnapshot rankingSnapshot, int limit) =>
        counted
            .Where(e => e.MyScore is >= HiddenGemMinimumScore && e.Anime.PopularityRank is > HiddenGemPopularityFloor)
            .OrderByDescending(e => e.Anime.PopularityRank!.Value)
            .ThenByDescending(e => e.MyScore!.Value)
            .ThenBy(e => GroupRanking.RankKey(rankingSnapshot, e.AnimeId))
            .ThenBy(e => e.Anime.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.AnimeId)
            .Take(limit)
            .Select(e => new TasteGemDto(
                e.AnimeId, e.Anime.Title, e.Anime.EnglishTitle, e.Anime.PictureUrl,
                e.MyScore!.Value, e.Anime.PopularityRank!.Value))
            .ToList();

    // --- Splits ---------------------------------------------------------------------

    private readonly record struct Category(string Label, int Count, int Decade);

    /// <summary>The counted set split by demographic. An anime carrying none takes no
    /// part, and one carrying two counts toward both.</summary>
    public static List<TasteSplitDto> Demographics(IReadOnlyList<UserAnimeEntry> counted)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var entry in counted)
        {
            var carried = (entry.Anime.Genres ?? []).Where(TasteNames.IsDemographic).Distinct(StringComparer.Ordinal);
            foreach (var name in carried)
                counts[name] = counts.GetValueOrDefault(name) + 1;
        }

        return Split(
            counts.Select(pair => new Category(pair.Key, pair.Value, 0)).ToList(),
            (a, b) => CompareNames(a.Label, b.Label));
    }

    /// <summary>The counted set split by source material, as MyAnimeList stores the
    /// key. An anime with no stored source takes no part.</summary>
    public static List<TasteSplitDto> Sources(IReadOnlyList<UserAnimeEntry> counted)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var other = 0;
        foreach (var entry in counted)
        {
            var source = entry.Anime.Source;
            if (string.IsNullOrWhiteSpace(source))
                continue;

            if (source == MalOtherSource)
                other++;
            else
                counts[source] = counts.GetValueOrDefault(source) + 1;
        }

        return Split(
            counts.Select(pair => new Category(pair.Key, pair.Value, 0)).ToList(),
            (a, b) => CompareNames(a.Label, b.Label),
            alsoOther: other);
    }

    /// <summary>The counted set split by the decade of its air-start year, named like
    /// "2010s". An anime with no stored air date takes no part.</summary>
    public static List<TasteSplitDto> Eras(IReadOnlyList<UserAnimeEntry> counted)
    {
        var counts = new Dictionary<int, int>();
        foreach (var entry in counted)
        {
            if (entry.Anime.AiredFrom is not { } aired)
                continue;

            var decade = aired.Year / 10 * 10;
            counts[decade] = counts.GetValueOrDefault(decade) + 1;
        }

        // A tie goes to the more recent decade.
        return Split(
            counts.Select(pair => new Category($"{pair.Key}s", pair.Value, pair.Key)).ToList(),
            (a, b) => b.Decade.CompareTo(a.Decade));
    }

    // Largest first; the four largest are named and every one after is merged into one
    // category placed last. A share is a count over the sum of every count in the
    // split, which the caller can compute because the merged count is the sum of what
    // it holds.
    private static List<TasteSplitDto> Split(List<Category> categories, Comparison<Category> tieBreak, int alsoOther = 0)
    {
        categories.Sort((a, b) => a.Count != b.Count ? b.Count.CompareTo(a.Count) : tieBreak(a, b));

        var split = categories
            .Take(SplitNamedCategories)
            .Select(c => new TasteSplitDto(c.Label, c.Count))
            .ToList();

        var merged = categories.Skip(SplitNamedCategories).Sum(c => c.Count) + alsoOther;
        if (merged > 0)
            split.Add(new TasteSplitDto(OtherLabel, merged));

        return split;
    }

    // --- The biggest day --------------------------------------------------------------

    /// <summary>The local calendar day on which the activity log records the most
    /// episodes watched across anime of the counted set, or null when the log records
    /// none for them. A day's figure is the sum of its positive episode increases,
    /// read by the very rule the recap's per-period episode count reads
    /// (<see cref="RecapWatchLog.PositiveIncreases"/>), so a decrease contributes
    /// nothing and the two can never disagree. Days follow the app's local time zone;
    /// the earlier of two equal days is the biggest. The posters are up to three of
    /// that day's anime, most episodes that day first, then in my ranking's order.</summary>
    public static RecapBiggestDayDto? BiggestDay(
        IEnumerable<ActivityLog> logRows, IReadOnlyCollection<UserAnimeEntry> counted,
        IBroadcastLocalTimeConverter localTimeConverter, AnimeRankingSnapshot rankingSnapshot)
    {
        var countedById = counted.ToDictionary(e => e.AnimeId);
        var episodesByDay = new Dictionary<DateOnly, Dictionary<int, int>>();

        foreach (var (row, increase) in RecapWatchLog.PositiveIncreases(logRows))
        {
            if (!countedById.ContainsKey(row.AnimeId))
                continue;

            var day = localTimeConverter.GetLocalDate(row.Timestamp);
            if (!episodesByDay.TryGetValue(day, out var perAnime))
                episodesByDay[day] = perAnime = [];
            perAnime[row.AnimeId] = perAnime.GetValueOrDefault(row.AnimeId) + increase;
        }

        if (episodesByDay.Count == 0)
            return null;

        var (date, episodesByAnime) = episodesByDay
            .OrderByDescending(day => day.Value.Values.Sum())
            .ThenBy(day => day.Key)
            .First();

        var posters = episodesByAnime
            .Select(pair => (Entry: countedById[pair.Key], Episodes: pair.Value))
            .OrderByDescending(x => x.Episodes)
            .ThenBy(x => GroupRanking.RankKey(rankingSnapshot, x.Entry.AnimeId))
            .ThenBy(x => x.Entry.Anime.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Entry.AnimeId)
            .Take(GroupRanking.PosterCount)
            .Select(x => GroupRanking.ToPoster(x.Entry))
            .ToList();

        return new RecapBiggestDayDto(date, episodesByAnime.Values.Sum(), posters);
    }
}
