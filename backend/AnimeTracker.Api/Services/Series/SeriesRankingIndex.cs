using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Ranking;
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
    /// averages and MAL reveal boolean computed over the main line only, plus
    /// the two figures (tier-season-refresh-and-top-series-order design.md
    /// D1) the profile's my-score ordering breaks ties on: the average
    /// ranking position over the whole main line and the main-line episodes
    /// aired over its default combination — the same figures and the same
    /// scopes <see cref="ListedSeries"/> reports, shared rather than
    /// restated. Still applies its own eligibility rules — membership of any
    /// member, plus the two-aired-main-line-entries coverage rule — which
    /// this change does not touch. Unordered; the caller applies whatever
    /// ordering it needs.</summary>
    public List<SeriesRankingResult> EligibleSeries(Dictionary<int, int> airedEpisodesByAnimeId, AnimeRankingSnapshot ranking)
    {
        var results = new List<SeriesRankingResult>();

        foreach (var group in _membersBySeriesId)
        {
            var members = group.ToList();
            if (!members.Any(m => m.EntryStatus is not null))
                continue; // no member of this series is in my list (task 2.5)

            // The series id is the root's anime id, and every series holds a
            // membership for its own root (key-series-by-root-anime-id
            // design.md D6), so display fields come from that row.
            var root = members.First(m => m.AnimeId == m.SeriesId);
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
            var (title, englishTitle, pictureUrl) = SeriesIdentity.Resolve(
                root.SelectedTitle, root.SelectedPictureUrl, root.Title, root.EnglishTitle, root.MalPictureUrl);

            // mainLineAverageRank accompanies mineAverage above and so covers
            // the same (whole, unfiltered) main line it does. mainLineAired-
            // Episodes is a card-scale figure and follows the default
            // combination of version alternatives, as it does on the Series
            // page — the two figures deliberately use different member sets
            // (design.md D2).
            var mainLineAverageRank = MainLineAverageRankOf(mainLine, ranking);
            var defaultVisibleIds = DefaultVisibleMainLineAnimeIds(mainLine);
            var scopedMainLine = mainLine.Where(m => defaultVisibleIds.Contains(m.AnimeId)).ToList();
            var mainLineAiredEpisodes = ListedSeriesMainLineAiredEpisodes(scopedMainLine, airedEpisodesByAnimeId);

            results.Add(new SeriesRankingResult(
                group.Key, title, englishTitle, pictureUrl,
                members.Count, mainLineAiredCount, malAverage, mineAverage, malRevealed,
                mainLineAiredEpisodes, mainLineAverageRank));
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
    /// page's own rule for that case. <paramref name="ranking"/> is the
    /// derived ranking my score edits and hand-order already produce
    /// (polish-search-sort-and-titles design.md D7/D8) — read here, never
    /// stored or duplicated, so a score edit changes the average rank with no
    /// rebuild. Unordered; the caller applies whatever ordering it
    /// needs.</summary>
    public List<SeriesListItemDto> ListedSeries(Dictionary<int, int> airedEpisodesByAnimeId, AnimeRankingSnapshot ranking)
    {
        var results = new List<SeriesListItemDto>();

        foreach (var group in _membersBySeriesId)
        {
            var members = group.ToList();
            // A version-neighbour-only membership must not make its host
            // series eligible here (rebuild-series-by-story-component
            // design.md, series-browser spec "A version-neighbour membership
            // does not list a series", task 7.6) — otherwise listing one
            // telling would also list every neighbouring telling it holds as
            // an extra. An ordinary (Core) extra still counts, unchanged.
            if (!members.Any(m => m.EntryStatus is not null && m.MembershipKind == nameof(MembershipKind.Core)))
                continue;

            var root = members.First(m => m.AnimeId == m.SeriesId);
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

            // The card's episode/progress figures use the default
            // combination of alternatives (rebuild-series-by-story-component
            // design.md D6, series-browser spec "A card's figures cover its
            // own telling" — "the card's figures SHALL use the default
            // combination... the browser SHALL NOT re-derive figures per
            // alternative", task 7.5). Score averages above stay over the
            // whole, unfiltered main line, per the same rule.
            var defaultVisibleIds = DefaultVisibleMainLineAnimeIds(mainLine);
            var scopedMainLine = mainLine.Where(m => defaultVisibleIds.Contains(m.AnimeId)).ToList();

            var (episodeTotal, hasUnknown) = MainLineEpisodeTotal(scopedMainLine, airedEpisodesByAnimeId);
            var (badge, behindEpisodes) = ProgressBadge(scopedMainLine, status, airedEpisodesByAnimeId);
            var mainLineWatchedEpisodes = scopedMainLine.Sum(m => MemberEffectiveWatchedEpisodes(m, airedEpisodesByAnimeId));
            var mainLineAiredEpisodes = ListedSeriesMainLineAiredEpisodes(scopedMainLine, airedEpisodesByAnimeId);
            var (title, englishTitle, pictureUrl) = SeriesIdentity.Resolve(
                root.SelectedTitle, root.SelectedPictureUrl, root.Title, root.EnglishTitle, root.MalPictureUrl);

            // Both figures below describe the whole main line, not
            // scopedMainLine — unlike the episode figures two lines above,
            // they accompany the score averages (also over the whole main
            // line) and must describe the same member set (design.md D8).
            var mainLineAiredCount = mainLine.Count(m => m.AiringStatus != "not_yet_aired");
            var mainLineAverageRank = MainLineAverageRankOf(mainLine, ranking);

            results.Add(new SeriesListItemDto(
                group.Key, title, englishTitle, pictureUrl,
                status, badge, behindEpisodes, malAverage, mineAverage, malRevealed,
                firstYear, lastYear, episodeTotal, hasUnknown, members.Count,
                mainLineWatchedEpisodes, mainLineAiredEpisodes,
                mainLineAiredCount, mainLineAverageRank));
        }

        return results;
    }

    /// <summary>The mean ranking position over main-line members that hold a
    /// rank, <c>null</c> when none does (tier-season-refresh-and-top-series-
    /// order design.md D1) — shared by <see cref="ListedSeries"/> and
    /// <see cref="EligibleSeries"/> rather than each restating it. Unranked
    /// members are filtered out before averaging, so they contribute neither
    /// a rank nor a divisor.</summary>
    private static double? MainLineAverageRankOf(List<SeriesRankingMemberProjection> mainLine, AnimeRankingSnapshot ranking)
    {
        var ranks = mainLine.Select(m => ranking.RankOf(m.AnimeId)).Where(r => r is not null).ToList();
        return ranks.Count > 0 ? ranks.Average(r => r!.Value) : (double?)null;
    }

    /// <summary>The main line's default-combination visible set
    /// (rebuild-series-by-story-component design.md D6, task 7.5): trunk —
    /// no <c>BranchHeadAnimeId</c> at all — plus, per version slot present,
    /// the default alternative's own branch. Mirrors
    /// <c>SeriesService.VisibleMainLineMembers</c>/<c>BuildSlots</c>, adapted
    /// to this index's flatter projection; the browser card carries no
    /// picker, so its pick-dependent figures always read this one
    /// combination. Synthesizes a minimal <see cref="AnimeMetadata"/> per
    /// member purely to reuse <see cref="SeriesVersionSlots.DefaultAlternativeId"/>
    /// rather than restating that rule here.</summary>
    private static HashSet<int> DefaultVisibleMainLineAnimeIds(List<SeriesRankingMemberProjection> mainLine)
    {
        var visible = mainLine.Where(m => m.BranchHeadAnimeId is null).Select(m => m.AnimeId).ToHashSet();

        var alternativeGroups = mainLine.Where(m => m.VersionSlotKey is not null).GroupBy(m => m.VersionSlotKey!.Value).ToList();
        if (alternativeGroups.Count == 0)
            return visible;

        var memberById = mainLine.ToDictionary(m => m.AnimeId, ToSyntheticAnime);

        foreach (var group in alternativeGroups)
        {
            var alternativeIds = group.Select(m => m.AnimeId).OrderBy(id => SeriesGraphBuilder.OrderKey(memberById[id])).ToList();
            var branchMemberIdsByAlternativeId = alternativeIds.ToDictionary(
                id => id,
                id => mainLine.Where(m => m.BranchHeadAnimeId == id).Select(m => m.AnimeId).ToHashSet());

            var slot = new SeriesVersionSlots.VersionSlot
            {
                SlotKey = group.Key,
                AlternativeIds = alternativeIds,
                BranchMemberIdsByAlternativeId = branchMemberIdsByAlternativeId,
            };

            var defaultId = SeriesVersionSlots.DefaultAlternativeId(slot, memberById);
            visible.UnionWith(branchMemberIdsByAlternativeId[defaultId]);
        }

        return visible;
    }

    private static AnimeMetadata ToSyntheticAnime(SeriesRankingMemberProjection m) => new()
    {
        Id = m.AnimeId,
        Title = m.Title,
        MalScore = m.MalScore,
        PopularityRank = m.PopularityRank,
        AiredFrom = m.AiredFrom,
        UserEntry = m.EntryStatus is null
            ? null
            : new UserAnimeEntry { AnimeId = m.AnimeId, EpisodesWatched = m.EpisodesWatched ?? 0, Status = m.EntryStatus.Value },
    };

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
        // main-line entry is marked Completed or Rewatching (polish-rewatch
        // design.md D2 — starting a rewatch of a finished franchise mustn't
        // downgrade its badge). Vacuously skipped (not vacuously true) when
        // nothing has finished airing, since it also requires at least one
        // such entry — a single still-running entry falls through to the
        // rules below instead.
        if (status == "Finished" && finishedAiring.Count > 0
            && finishedAiring.All(m => m.EntryStatus is WatchStatus.Completed or WatchStatus.Rewatching))
            return (SeriesProgressBadge.Completed, null);

        // 2. Dropped: the most recently *aired* Dropped entry, with nothing
        // aired after it ever watched (design.md D2) — a drop later resumed
        // and watched past doesn't count. A later entry marked Rewatching
        // counts as watched past the drop (polish-rewatch design.md D2), via
        // the same effective-watched figure rule (3) below uses. Decided from
        // watch status alone, never blocked by an unknown broadcast count
        // (design.md D4).
        var droppedEntries = airedMembers.Where(m => m.EntryStatus == WatchStatus.Dropped).ToList();
        if (droppedEntries.Count > 0)
        {
            var lastDroppedOrder = droppedEntries.Max(m => m.Order);
            var nothingWatchedAfter = airedMembers
                .Where(m => m.Order > lastDroppedOrder)
                .All(m => MemberEffectiveWatchedEpisodes(m, airedEpisodesByAnimeId) == 0);
            if (nothingWatchedAfter)
                return (SeriesProgressBadge.Dropped, null);
        }

        // A Rewatching main-line entry counts as fully watched throughout the
        // rules below (polish-rewatch design.md D2), as the greater of its
        // own episodes-watched and its aired-so-far figure.
        var watchedTotal = mainLine.Sum(m => MemberEffectiveWatchedEpisodes(m, airedEpisodesByAnimeId));

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

            var root = members.First(m => m.AnimeId == m.SeriesId);
            var (title, englishTitle, pictureUrl) = SeriesIdentity.Resolve(
                root.SelectedTitle, root.SelectedPictureUrl, root.Title, root.EnglishTitle, root.MalPictureUrl);
            results.Add(new SeriesRewatchResult(
                group.Key, title, englishTitle, pictureUrl, totalSeconds));
        }

        return results
            .OrderByDescending(r => r.RewatchSeconds)
            .ThenBy(r => r.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // A member's aired-so-far figure for WatchMath.EffectiveWatchedEpisodes
    // (polish-rewatch design.md D2, task 3.1): a finished-airing member has
    // aired its full published total (null when that total itself is
    // unknown, which EffectiveWatchedEpisodes then treats as "unknown" too);
    // a currently-airing member's aired count comes from the caller's
    // per-currently-airing-main-line-member map; any other member — not yet
    // aired — has aired nothing.
    private static int? MemberAiredEpisodes(SeriesRankingMemberProjection m, Dictionary<int, int> airedEpisodesByAnimeId) =>
        m.AiringStatus switch
        {
            "finished_airing" => m.TotalEpisodes,
            "currently_airing" => airedEpisodesByAnimeId.TryGetValue(m.AnimeId, out var aired) ? aired : null,
            _ => 0,
        };

    // A Rewatching member counts as fully watched (polish-rewatch design.md
    // D2): used everywhere this index sums my watched main-line episodes, so
    // the badge and the browser's My-progress sort read a rewatch the same
    // way the series page does.
    private static int MemberEffectiveWatchedEpisodes(SeriesRankingMemberProjection m, Dictionary<int, int> airedEpisodesByAnimeId) =>
        WatchMath.EffectiveWatchedEpisodes(m.EpisodesWatched ?? 0, MemberAiredEpisodes(m, airedEpisodesByAnimeId), m.EntryStatus);

    // Kept in step with WatchMath rather than restating its fallbacks
    // (RewatchOnlyEpisodes' published-total baseline, EpisodeSeconds' 24
    // minutes) — task 9.3. Also folds in an in-progress rewatch's episodes
    // (design.md D1, tasks.md 2.1): a member never in my list (RewatchCount
    // and EpisodesWatched both null, EntryStatus null) or never rewatched and
    // not currently rewatching both still resolve to a zero episode figure
    // and so still contribute nothing.
    private static long MemberRewatchSeconds(SeriesRankingMemberProjection m)
    {
        var rewatchEpisodes = WatchMath.RewatchEpisodesIncludingCurrentRun(
            m.RewatchCount ?? 0, m.TotalEpisodes, m.EpisodesWatched ?? 0, m.EntryStatus);
        if (rewatchEpisodes == 0)
            return 0;

        return (long)rewatchEpisodes * WatchMath.EpisodeSeconds(m.AverageEpisodeDurationSeconds);
    }
}

/// <summary>One series' worth of Top series data: display fields from the
/// root member, its total member count (main line and extras), its
/// main-line count that has actually started airing, its two main-series
/// averages with the MAL reveal boolean (design.md decision 5), and the two
/// figures the profile's my-score ordering breaks ties on
/// (tier-season-refresh-and-top-series-order design.md D1/D2):
/// <see cref="MainLineAiredEpisodes"/>, over the main line's default
/// combination of version alternatives, and <see cref="MainLineAverageRank"/>,
/// the mean ranking position over the whole main line — <c>null</c> when no
/// main-line member is ranked. The two deliberately cover different member
/// sets; see <see cref="SeriesRankingIndex.EligibleSeries"/>.</summary>
public sealed record SeriesRankingResult(
    int SeriesId,
    string Title,
    string? EnglishTitle,
    string? PictureUrl,
    int EntryCount,
    int MainLineAiredCount,
    SeriesAverageDto MalMain,
    SeriesAverageDto MineMain,
    bool MalRevealed,
    int MainLineAiredEpisodes,
    double? MainLineAverageRank);

/// <summary>One series' worth of rewatch-time data: display fields from the
/// root member and the total rewatch time summed across every member
/// (design.md D9).</summary>
public sealed record SeriesRewatchResult(
    int SeriesId,
    string Title,
    string? EnglishTitle,
    string? PictureUrl,
    long RewatchSeconds);
