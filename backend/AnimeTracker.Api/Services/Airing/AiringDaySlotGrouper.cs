namespace AnimeTracker.Api.Services.Airing;

/// <summary>Groups one local day's airing rows into slots: consecutive
/// same-anime rows (ordered by air instant) merge into one ranged slot
/// unless another anime's row airs strictly between them. Rows with an
/// unknown episode number never merge — with anything — but also never
/// count as an interruption for their own anime's other rows, since the
/// "other anime" interruption set is scoped to a different AnimeId by
/// construction. Pure and stateless; see design.md phases 1-3.</summary>
public static class AiringDaySlotGrouper
{
    /// <summary>One stored episode row, reduced to what grouping and slot
    /// rendering need.</summary>
    public sealed record Row(
        int AnimeId,
        string Title,
        string? EnglishTitle,
        string? PictureUrl,
        DateTimeOffset AirsAtUtc,
        string LocalTime,
        int? EpisodeNumber);

    public static List<AiringSlotDto> Group(IReadOnlyList<Row> dayRows)
    {
        var indexed = dayRows.Select((row, index) => (row, index)).ToList();
        var items = new List<GroupedItem>();

        foreach (var animeRows in indexed.GroupBy(x => x.row.AnimeId))
        {
            foreach (var (row, index) in animeRows.Where(x => x.row.EpisodeNumber is null))
                items.Add(GroupedItem.Singleton(row, index));

            var known = animeRows
                .Where(x => x.row.EpisodeNumber is not null)
                .OrderBy(x => x.row.AirsAtUtc)
                .ToList();
            if (known.Count == 0)
                continue;

            // Instants of every row belonging to a *different* anime this day
            // — the interruption set for this anime's own run formation. A
            // different anime's unknown-episode row still counts here: it
            // represents a real episode airing, even though it can't itself
            // anchor a range.
            var otherInstants = indexed
                .Where(x => x.row.AnimeId != animeRows.Key)
                .Select(x => x.row.AirsAtUtc)
                .ToList();

            var currentRun = new List<(Row row, int index)> { known[0] };
            for (var i = 1; i < known.Count; i++)
            {
                var prev = known[i - 1];
                var next = known[i];
                var interrupted = otherInstants.Any(instant => instant > prev.row.AirsAtUtc && instant < next.row.AirsAtUtc);
                if (interrupted)
                {
                    items.Add(GroupedItem.FromRun(currentRun));
                    currentRun = [next];
                }
                else
                {
                    currentRun.Add(next);
                }
            }
            items.Add(GroupedItem.FromRun(currentRun));
        }

        // Ascending by earliest instant; at an exact tie, a single-instant
        // item sorts before a multi-instant one (covers both the tie-at-first
        // and all-same-instant-batch cases — see design.md phase 2). Any
        // remaining tie falls back to the order the rows arrived in.
        return items
            .OrderBy(i => i.EarliestInstant)
            .ThenBy(i => i.IsMultiInstant)
            .ThenBy(i => i.EarliestIndex)
            .Select(i => i.ToDto())
            .ToList();
    }

    private readonly record struct GroupedItem(
        DateTimeOffset EarliestInstant,
        bool IsMultiInstant,
        int EarliestIndex,
        int AnimeId,
        string Title,
        string? EnglishTitle,
        string? PictureUrl,
        string LocalTime,
        int? EpisodeNumber,
        int? EpisodeNumberEnd)
    {
        public AiringSlotDto ToDto() => new(AnimeId, Title, EnglishTitle, PictureUrl, LocalTime, EpisodeNumber, EpisodeNumberEnd);

        public static GroupedItem Singleton(Row row, int index) => new(
            row.AirsAtUtc, false, index, row.AnimeId, row.Title, row.EnglishTitle, row.PictureUrl, row.LocalTime, row.EpisodeNumber, null);

        public static GroupedItem FromRun(List<(Row row, int index)> runRows)
        {
            var first = runRows[0].row;
            var last = runRows[^1].row;
            var earliestIndex = runRows.Min(r => r.index);
            var minEpisode = runRows.Min(r => r.row.EpisodeNumber!.Value);
            var maxEpisode = runRows.Max(r => r.row.EpisodeNumber!.Value);

            return new GroupedItem(
                first.AirsAtUtc,
                first.AirsAtUtc != last.AirsAtUtc,
                earliestIndex,
                first.AnimeId,
                first.Title,
                first.EnglishTitle,
                first.PictureUrl,
                first.LocalTime,
                minEpisode,
                runRows.Count > 1 ? maxEpisode : null);
        }
    }
}
