using AnimeTracker.Api.Models;
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

    /// <summary>Every anime id belonging to a currently-airing main-line
    /// member across the whole index — the set <see cref="SeriesListService"/>
    /// resolves aired-so-far counts for, in one batched call, before calling
    /// <see cref="ListedSeries"/> (add-series-browser design.md D6).</summary>
    public IReadOnlyCollection<int> CurrentlyAiringMainLineAnimeIds() =>
        _membersBySeriesId
            .SelectMany(group => group)
            .Where(m => m.IsMainLine && m.AiringStatus == "currently_airing")
            .Select(m => m.AnimeId)
            .Distinct()
            .ToList();

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

    /// <summary>Every series with at least one member — main line or extra —
    /// in my list, each carrying the figures the Series page's card needs
    /// (add-series-browser design.md D2). Eligibility is membership alone:
    /// deliberately does not apply EligibleSeries()'s two-aired-main-line-
    /// entries coverage rule, since that rule exists only to protect an
    /// average this page doesn't rank by — a browse surface should list a
    /// franchise the user has seen even one entry of (design.md D3).
    /// <paramref name="airedEpisodesByAnimeId"/> holds the aired-so-far count
    /// for every currently-airing main-line member across the whole index
    /// (design.md D6); an id with no entry means "unknown", per the series
    /// page's own rule for that case. Unordered; the caller applies whatever
    /// ordering it needs.</summary>
    public List<SeriesListItemDto> ListedSeries(Dictionary<int, int> airedEpisodesByAnimeId)
    {
        var results = new List<SeriesListItemDto>();

        foreach (var group in _membersBySeriesId)
        {
            var members = group.ToList();
            if (!members.Any(m => m.EntryStatus is not null))
                continue; // no member of this series is in my list

            var root = members.First(m => m.AnimeId == m.RootAnimeId);
            var mainLine = members.Where(m => m.IsMainLine).ToList();

            var status = SeriesStatusRules.Compute(
                mainLine.Select(m => m.AiringStatus),
                members.Select(m => m.AiringStatus));

            var malAverage = SeriesAverages.Mal(mainLine.Select(m => m.MalScore));
            var mineAverage = SeriesAverages.Mine(mainLine.Select(m => m.MyScore));

            // Same reveal composition EligibleSeries() uses (design.md D4), so
            // a card can never reveal what the series page itself would blur.
            var mainLineAiring = mainLine.Any(m => m.AiringStatus == "currently_airing");
            var mainLineSettledByMe = SeriesAverages.MainLineSettledByMe(
                mainLine.Select(m => (m.AiringStatus, m.EntryStatus)));
            var malRevealed = mainLineSettledByMe && !mainLineAiring;

            var (firstYear, lastYear) = ListedSeriesYearSpan(members);
            var (episodeTotal, hasUnknown) = MainLineEpisodeTotal(mainLine, airedEpisodesByAnimeId);
            var (badge, behindEpisodes) = ProgressBadge(mainLine, status, airedEpisodesByAnimeId);
            var mainLineWatchedEpisodes = mainLine.Sum(m => m.EpisodesWatched ?? 0);
            var mainLineAiredEpisodes = ListedSeriesMainLineAiredEpisodes(mainLine, airedEpisodesByAnimeId);

            results.Add(new SeriesListItemDto(
                group.Key, root.RootAnimeId, root.Title, root.EnglishTitle, root.PictureUrl,
                status, badge, behindEpisodes, malAverage, mineAverage, malRevealed,
                firstYear, lastYear, episodeTotal, hasUnknown, members.Count,
                mainLineWatchedEpisodes, mainLineAiredEpisodes));
        }

        return results;
    }

    // Mirrors SeriesService.YearSpan, over every member rather than main-line
    // only (design.md D7/task 2.7) — the card's year span matches the series
    // page's.
    private static (int? First, int? Last) ListedSeriesYearSpan(List<SeriesRankingMemberProjection> members)
    {
        var aired = members.Where(m => m.AiredFrom is not null).ToList();
        if (aired.Count == 0) return (null, null);
        var firstYear = aired.Min(m => m.AiredFrom!.Value.Year);
        var lastYear = aired.Max(m => (m.AiredTo ?? m.AiredFrom!.Value).Year);
        return (firstYear, lastYear);
    }

    // Mirrors SeriesService.BuildStats' episode-total fallback (design.md
    // D7): a known TotalEpisodes contributes in full; an unknown one
    // contributes its known aired-so-far count instead (0 when that's
    // unknown too) and sets HasUnknown.
    private static (int Total, bool HasUnknown) MainLineEpisodeTotal(
        List<SeriesRankingMemberProjection> mainLine, Dictionary<int, int> airedEpisodesByAnimeId)
    {
        var total = 0;
        var hasUnknown = false;

        foreach (var member in mainLine)
        {
            if (member.TotalEpisodes is { } known)
            {
                total += known;
                continue;
            }

            hasUnknown = true;
            total += airedEpisodesByAnimeId.GetValueOrDefault(member.AnimeId);
        }

        return (total, hasUnknown);
    }

    // Mirrors SeriesService.MainLineAiredEpisodesFromMap (design.md/task
    // 2.9): summed over known-total main-line members only, so it can never
    // exceed MainLineEpisodeTotal.
    private static int ListedSeriesMainLineAiredEpisodes(
        List<SeriesRankingMemberProjection> mainLine, Dictionary<int, int> airedEpisodesByAnimeId)
    {
        var aired = 0;

        foreach (var member in mainLine)
        {
            if (member.TotalEpisodes is not { } total)
                continue;

            aired += member.AiringStatus switch
            {
                "finished_airing" => total,
                "currently_airing" => Math.Min(airedEpisodesByAnimeId.GetValueOrDefault(member.AnimeId), total),
                _ => 0,
            };
        }

        return aired;
    }

    // The series-page/series-browser specs' six-step precedence, mirroring
    // SeriesPage.tsx's client-side completionBadge exactly
    // (polish-series-badges-and-filters design.md D1) so a card and the
    // series page it links to can never disagree. Evaluated over aired
    // main-line entries (finished or currently airing) in release order.
    private static (SeriesProgressBadge Badge, int? BehindEpisodes) ProgressBadge(
        List<SeriesRankingMemberProjection> mainLine, string status, Dictionary<int, int> airedEpisodesByAnimeId)
    {
        var airedMembers = mainLine
            .Where(m => m.AiringStatus is "finished_airing" or "currently_airing")
            .OrderBy(m => m.Order)
            .ToList();
        var finishedAiring = airedMembers.Where(m => m.AiringStatus == "finished_airing").ToList();

        // 1. Completed: the whole series is done and every finished-airing
        // main-line entry is marked Completed. Vacuously skipped (not
        // vacuously true) when nothing has finished airing, since it also
        // requires at least one such entry — a single still-running entry
        // falls through to the rules below instead.
        if (status == "Finished" && finishedAiring.Count > 0 && finishedAiring.All(m => m.EntryStatus == WatchStatus.Completed))
            return (SeriesProgressBadge.Completed, null);

        // 2. Dropped: the most recently *aired* Dropped entry, with nothing
        // aired after it ever watched (design.md D2) — a drop later resumed
        // and watched past doesn't count. Decided from watch status alone,
        // never blocked by an unknown broadcast count (design.md D4).
        var droppedEntries = airedMembers.Where(m => m.EntryStatus == WatchStatus.Dropped).ToList();
        if (droppedEntries.Count > 0)
        {
            var lastDroppedOrder = droppedEntries.Max(m => m.Order);
            var nothingWatchedAfter = airedMembers
                .Where(m => m.Order > lastDroppedOrder)
                .All(m => (m.EpisodesWatched ?? 0) == 0);
            if (nothingWatchedAfter)
                return (SeriesProgressBadge.Dropped, null);
        }

        var watchedTotal = mainLine.Sum(m => m.EpisodesWatched ?? 0);

        // 5. Unwatched: something has aired but nothing has ever been
        // watched, and (2) didn't already claim the case (design.md D3).
        if (watchedTotal == 0)
            return airedMembers.Count > 0 ? (SeriesProgressBadge.Unwatched, null) : (SeriesProgressBadge.None, null);

        // 3/4. Caught up / N behind, generalized over every aired main-line
        // entry rather than only a currently-airing one. EpisodesAiredAsOfAsync
        // does no estimation — an unknown broadcast count means this can't be
        // computed reliably, so it shows nothing rather than inventing a figure.
        var airedTotal = 0;
        foreach (var member in airedMembers)
        {
            if (member.AiringStatus == "finished_airing")
            {
                if (member.TotalEpisodes is not { } total)
                    return (SeriesProgressBadge.None, null);
                airedTotal += total;
            }
            else if (!airedEpisodesByAnimeId.TryGetValue(member.AnimeId, out var aired))
            {
                return (SeriesProgressBadge.None, null);
            }
            else
            {
                airedTotal += aired;
            }
        }

        var behind = Math.Max(0, airedTotal - watchedTotal);
        return behind == 0 ? (SeriesProgressBadge.CaughtUp, null) : (SeriesProgressBadge.Behind, behind);
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
