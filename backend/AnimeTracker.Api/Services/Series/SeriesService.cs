using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Artwork;
using AnimeTracker.Api.Services.Entries;
using AnimeTracker.Api.Services.Infrastructure;
using AnimeTracker.Api.Services.Profile;
using AnimeTracker.Api.Services.Ranking;
using AnimeTracker.Api.Services.Watching;
using Microsoft.EntityFrameworkCore;
// Alias needed because this namespace's last segment ("Series") shadows the
// Models.Series type name.
using SeriesEntity = AnimeTracker.Api.Models.Series;

namespace AnimeTracker.Api.Services.Series;

/// <summary>Resolves a franchise for the read endpoint: decides whether the
/// stored series needs (re)building, runs that build single-flight through
/// <see cref="RefreshGate"/>, and projects the stored rows to
/// <see cref="SeriesDto"/> — scores and stats are computed fresh on every
/// call (design.md decision 5/task 3.9), never cached.</summary>
public class SeriesService(
    AnimeTrackerDbContext db,
    SeriesGraphBuilder graphBuilder,
    RefreshGate refreshGate,
    IEpisodeScheduleService scheduleService,
    IAnimeRankingService rankingService) : ISeriesService
{
    private static readonly TimeSpan StaleAfter = TimeSpan.FromDays(30);

    public Task<SeriesDto> GetSeriesAsync(int animeId, CancellationToken ct = default) =>
        ResolveAsync(animeId, forceRebuild: false, ct);

    public Task<SeriesDto> RebuildSeriesAsync(int animeId, CancellationToken ct = default) =>
        ResolveAsync(animeId, forceRebuild: true, ct);

    private async Task<SeriesDto> ResolveAsync(int animeId, bool forceRebuild, CancellationToken ct)
    {
        var series = await FindSeriesAsync(animeId, ct);

        if (forceRebuild || NeedsBuild(series))
        {
            // Known series rebuilds collapse on its id; a never-built one
            // collapses on the seed anime id instead, since there's no
            // series id yet to key on (design.md decision 6).
            var key = series is not null ? $"series:{series.Id}" : $"series:anime:{animeId}";
            using (await refreshGate.LockAsync(key, ct))
            {
                // Re-check after acquiring the gate: a waiter sees the
                // winner's build and skips a redundant one.
                series = await FindSeriesAsync(animeId, ct);
                if (forceRebuild || NeedsBuild(series))
                {
                    var fetchBudget = forceRebuild ? SeriesGraphBuilder.RebuildFetchBudget : SeriesGraphBuilder.VisitFetchBudget;
                    var probeBudget = forceRebuild ? SeriesGraphBuilder.RebuildProbeBudget : SeriesGraphBuilder.VisitProbeBudget;
                    // BuildAsync returns null when the seed's component has
                    // no other member. That's not necessarily "no series at
                    // all" if one was already stored (relations can thin out
                    // between visits) — falls back to the pre-build snapshot
                    // rather than treating a already-stored series as gone.
                    //
                    // expandLeanMembers is always on, not just on an explicit
                    // rebuild: a lean member (season/top-anime browsing) has
                    // no outgoing relations of its own, so a real season
                    // reachable only *through* it never gets discovered, and
                    // main-line classification (sequel/prequel edges only)
                    // can hand the main line to whichever chain happened to
                    // be full-fetched. Spending part of the visit budget here
                    // is what lets a first visit self-heal instead of always
                    // depending on the user clicking Rebuild.
                    series = await graphBuilder.BuildAsync(animeId, fetchBudget, probeBudget, expandLeanMembers: true, ct) ?? series;
                }
            }
        }

        if (series is null)
            throw new SeriesNotFoundException(animeId);

        return await ProjectAsync(series.Id, ct);
    }

    // Deliberately does not check IsTruncated: a series genuinely over the
    // cap would then re-traverse and spend fetch budget on every single
    // visit, forever. A truncated series instead picks up a raised cap on
    // the next explicitly requested rebuild or the next staleness-triggered
    // one (design.md decision 3). The ClassificationRevisedAt clause is the
    // same "row predates X, heal it on first read" pattern, one-shot rather
    // than recurring: it rebuilds every series built under superseded
    // main-line rules exactly once, on its next read, without a schema
    // change or a manual migration.
    private static bool NeedsBuild(SeriesEntity? series) =>
        series is null || series.IsPartial || series.BuiltAt < DateTimeOffset.UtcNow - StaleAfter
        || series.BuiltAt < SeriesGraphBuilder.ClassificationRevisedAt;

    // Filtered to IsPrimary (split-series-by-version task 7.1): an anime can
    // now hold more than one membership (its own telling, plus any other
    // telling it's a shared or boundary member of), and every "the series for
    // this anime" lookup resolves to the one series-versions capability marks
    // primary.
    public async Task<int?> FindSeriesIdAsync(int animeId, CancellationToken ct = default) =>
        await db.SeriesMembers.AsNoTracking()
            .Where(m => m.AnimeId == animeId && m.IsPrimary)
            .Select(m => (int?)m.SeriesId)
            .FirstOrDefaultAsync(ct);

    private async Task<SeriesEntity?> FindSeriesAsync(int animeId, CancellationToken ct)
    {
        var seriesId = await db.SeriesMembers.AsNoTracking()
            .Where(m => m.AnimeId == animeId && m.IsPrimary)
            .Select(m => (int?)m.SeriesId)
            .FirstOrDefaultAsync(ct);

        return seriesId is null
            ? null
            : await db.Series.AsNoTracking().FirstOrDefaultAsync(s => s.Id == seriesId, ct);
    }

    // --- Projection (3.1-3.4; extras grouping and related entries by
    // split-series-by-version tasks 7.2-7.4) ---

    private async Task<SeriesDto> ProjectAsync(int seriesId, CancellationToken ct)
    {
        var series = await db.Series.AsNoTracking()
            .Include(s => s.Members).ThenInclude(m => m.Anime).ThenInclude(a => a.RelatedAnime)
            .Include(s => s.Members).ThenInclude(m => m.Anime).ThenInclude(a => a.UserEntry)
            .FirstAsync(s => s.Id == seriesId, ct);

        var memberAnimeIds = series.Members.Select(m => m.AnimeId).ToHashSet();
        var mainLineMembers = series.Members.Where(m => m.IsMainLine).OrderBy(m => m.Order).ToList();
        // RelationGroup, not media type (design.md decision 4) — order-within-
        // group here only feeds BuildStats' favourite-tie-break watch order
        // below; the client-facing group ordering is recomputed fresh in
        // BuildDisplayExtrasAsync so it can interleave with related entries.
        var extraMembers = series.Members.Where(m => !m.IsMainLine)
            .OrderBy(m => SeriesRelationGroupOrder.GroupOf(ParseRelationGroup(m.RelationGroup)))
            .ThenBy(m => m.Order)
            .ToList();
        var allAnime = series.Members.Select(m => m.Anime).ToList();

        var root = allAnime.First(a => a.Id == series.Id);
        var (title, englishTitle, pictureUrl) = SeriesIdentity.Resolve(
            series.SelectedTitle, series.SelectedPictureUrl, root.Title, root.EnglishTitle, root.MalPictureUrl);
        var (firstYear, lastYear) = YearSpan(allAnime);
        var rootAniListId = await db.AnimeAiringSyncs.AsNoTracking()
            .Where(s => s.AnimeId == series.Id)
            .Select(s => (int?)s.AniListId)
            .FirstOrDefaultAsync(ct);
        var airedEpisodesByAnimeId = await AiredEpisodesByAnimeIdAsync(allAnime, ct);

        // The whole-library ranking (polish-... design.md D?), fetched fresh
        // on every read like every other figure here: each member's own rank
        // for SeriesEntryDto.GlobalRank, and the favourite tie-break below —
        // so a series-page favourite reorder, which writes this same
        // ranking, is reflected the moment the series is next read.
        var rankingSnapshot = await rankingService.GetSnapshotAsync(ct);
        var globalRankByAnimeId = allAnime.ToDictionary(a => a.Id, a => rankingSnapshot.RankOf(a.Id));

        var pictureOptions = SeriesPicturePool.Build(mainLineMembers);
        if (series.SelectedPictureUrl is { } selectedPictureUrl && !pictureOptions.Contains(selectedPictureUrl))
            pictureOptions.Add(selectedPictureUrl); // a stored choice is never re-validated away (design.md D9)
        var titleOptions = SeriesTitleRule.OfferedTitles(mainLineMembers);
        var picturesPendingCount = mainLineMembers.Count(m => m.Anime.UserEntry is not null && m.Anime.PicturesSyncedAt is null);

        var displayExtras = await BuildDisplayExtrasAsync(mainLineMembers, extraMembers, memberAnimeIds, airedEpisodesByAnimeId, globalRankByAnimeId, ct);

        // Version slots and their per-combination stats (design.md D4/D6,
        // tasks 7.2-7.3): read straight off the stored VersionSlotKey/
        // BranchHeadAnimeId columns, no re-walk of the relation graph.
        // Score averages (BuildScores above) deliberately keep reading the
        // whole, unfiltered mainLineMembers regardless of any pick.
        var slots = BuildSlots(mainLineMembers);
        var defaultPick = slots.ToDictionary(s => s.SlotKey, s => s.DefaultBranchHeadAnimeId);
        var stats = BuildStats(mainLineMembers, VisibleMainLineMembers(mainLineMembers, defaultPick), extraMembers, allAnime, airedEpisodesByAnimeId, globalRankByAnimeId);
        var statsByPick = EnumeratePickCombinations(slots)
            .Select(combo => new SeriesStatsByPickDto(
                slots.Select(s => combo[s.SlotKey]).ToList(),
                BuildStats(mainLineMembers, VisibleMainLineMembers(mainLineMembers, combo), extraMembers, allAnime, airedEpisodesByAnimeId, globalRankByAnimeId)))
            .ToList();

        return new SeriesDto(
            series.Id,
            rootAniListId,
            title,
            englishTitle,
            pictureUrl,
            ComputeStatus(mainLineMembers.Select(m => m.Anime).ToList(), allAnime),
            firstYear,
            lastYear,
            series.BuiltAt,
            series.IsPartial,
            series.IsTruncated,
            BuildScores(mainLineMembers, series.Members),
            stats,
            slots,
            statsByPick,
            mainLineMembers.Select(m => ToEntryDto(m, memberAnimeIds, airedEpisodesByAnimeId, globalRankByAnimeId)).ToList(),
            displayExtras,
            series.SelectedTitle,
            series.SelectedPictureUrl,
            pictureOptions,
            titleOptions,
            picturesPendingCount);
    }

    // --- Version slots (design.md D4/D5/D6, tasks 7.2-7.3) ---

    /// <summary>The main line's version slot descriptors, read straight off
    /// each member's stored <see cref="SeriesMember.VersionSlotKey"/>/
    /// <see cref="SeriesMember.BranchHeadAnimeId"/> rather than re-walking
    /// the relation graph SeriesGraphBuilder already walked once at build
    /// time (design.md D4: "the projection needs no re-walk"). Reconstructs
    /// the <see cref="SeriesVersionSlots.VersionSlot"/> shape
    /// <see cref="SeriesVersionSlots.DefaultAlternativeId"/> expects purely
    /// so that rule (design.md D5) can be reused rather than restated here.
    /// Empty when no two main-line members share a <c>VersionSlotKey</c>.</summary>
    private static List<SeriesSlotDto> BuildSlots(List<SeriesMember> mainLineMembers)
    {
        var memberById = mainLineMembers.ToDictionary(m => m.AnimeId, m => m.Anime);

        return mainLineMembers
            .Where(m => m.VersionSlotKey is not null)
            .GroupBy(m => m.VersionSlotKey!.Value)
            .OrderBy(g => g.Key)
            .Select(group =>
            {
                var alternativeIds = group.Select(m => m.AnimeId)
                    .OrderBy(id => SeriesGraphBuilder.OrderKey(memberById[id]))
                    .ToList();
                var branchMemberIdsByAlternativeId = alternativeIds.ToDictionary(
                    id => id,
                    id => mainLineMembers.Where(m => m.BranchHeadAnimeId == id).Select(m => m.AnimeId).ToHashSet());

                var slot = new SeriesVersionSlots.VersionSlot
                {
                    SlotKey = group.Key,
                    AlternativeIds = alternativeIds,
                    BranchMemberIdsByAlternativeId = branchMemberIdsByAlternativeId,
                };

                return new SeriesSlotDto(group.Key, alternativeIds, SeriesVersionSlots.DefaultAlternativeId(slot, memberById));
            })
            .ToList();
    }

    /// <summary>The main-line members visible under one combination of slot
    /// picks (design.md D4/D6): trunk — no <see cref="SeriesMember.BranchHeadAnimeId"/>
    /// at all — plus, per slot, the picked alternative's branch.
    /// <paramref name="pick"/> maps each slot's key to the alternative anime
    /// id chosen for it.</summary>
    private static List<SeriesMember> VisibleMainLineMembers(List<SeriesMember> mainLineMembers, IReadOnlyDictionary<int, int> pick)
    {
        var pickedBranchHeadIds = pick.Values.ToHashSet();
        return mainLineMembers.Where(m => m.BranchHeadAnimeId is null || pickedBranchHeadIds.Contains(m.BranchHeadAnimeId.Value)).ToList();
    }

    // A defensive ceiling, not a working limit: no series observed today has
    // more than one slot, so EnumeratePickCombinations's cap logic below is
    // exercised only by a hypothetical multi-slot main line (design.md D6).
    internal const int MaxStatsCombinations = 24;

    /// <summary>Every admissible combination of slot picks (design.md D6,
    /// task 7.3): built slot by slot in <c>SlotKey</c> order, expanding the
    /// running set of combinations across a slot's alternatives while doing
    /// so would stay within <see cref="MaxStatsCombinations"/>; once it would
    /// not, that slot and every slot after it are pinned to their own
    /// default instead of varied, so a hypothetical multi-slot main line
    /// degrades gracefully rather than combinatorially exploding. The
    /// all-defaults combination is always among the results, since a varied
    /// slot's alternatives always include its own default and a pinned slot
    /// is set to its default directly — so <c>SeriesDto.Stats</c> is always
    /// equal to some entry of <c>StatsByPick</c>.</summary>
    internal static List<Dictionary<int, int>> EnumeratePickCombinations(List<SeriesSlotDto> slots)
    {
        var combinations = new List<Dictionary<int, int>> { new() };

        foreach (var slot in slots.OrderBy(s => s.SlotKey))
        {
            if (combinations.Count * slot.AlternativeAnimeIds.Count <= MaxStatsCombinations)
            {
                combinations = combinations
                    .SelectMany(combo => slot.AlternativeAnimeIds.Select(altId =>
                        new Dictionary<int, int>(combo) { [slot.SlotKey] = altId }))
                    .ToList();
            }
            else
            {
                foreach (var combo in combinations)
                    combo[slot.SlotKey] = slot.DefaultBranchHeadAnimeId;
            }
        }

        return combinations;
    }

    // --- More section: extras + related entries, merged (7.2-7.4) ---

    /// <summary>The More section's full tile list: real extra members plus
    /// related entries — anime a main-line member relates to by a relation
    /// the series traversal doesn't follow (design.md D5) — merged into the
    /// same relation groups and interleaved by the same aired-from/MAL-id
    /// order extras use (<see cref="SeriesGraphBuilder.OrderKey"/>), since a
    /// real extra's stored <c>Order</c> alone has no way to interleave with a
    /// related entry computed only at read time. Related entries never enter
    /// any average, stat, or the member cap (task 7.4) — they're excluded
    /// from every stats input above and only ever joined into this display
    /// list.</summary>
    private async Task<List<SeriesEntryDto>> BuildDisplayExtrasAsync(
        List<SeriesMember> mainLineMembers,
        List<SeriesMember> extraMembers,
        HashSet<int> memberAnimeIds,
        Dictionary<int, int?> airedEpisodesByAnimeId,
        Dictionary<int, int?> globalRankByAnimeId,
        CancellationToken ct)
    {
        var relatedEntries = await ProjectRelatedEntriesAsync(mainLineMembers, memberAnimeIds, ct);
        var relatedAiredEpisodes = relatedEntries.Count > 0
            ? await AiredEpisodesByAnimeIdAsync(relatedEntries.Select(r => r.Anime).ToList(), ct)
            : new Dictionary<int, int?>();

        return extraMembers
            .Select(m => (Group: ParseRelationGroup(m.RelationGroup), Sort: SeriesGraphBuilder.OrderKey(m.Anime),
                Dto: ToEntryDto(m, memberAnimeIds, airedEpisodesByAnimeId, globalRankByAnimeId)))
            .Concat(relatedEntries.Select(r => (Group: r.Group, Sort: SeriesGraphBuilder.OrderKey(r.Anime),
                Dto: ToRelatedEntryDto(r, relatedAiredEpisodes))))
            .OrderBy(x => SeriesRelationGroupOrder.GroupOf(x.Group))
            .ThenBy(x => x.Sort)
            .GroupBy(x => x.Group)
            .SelectMany(g => g.Select((x, i) => x.Dto with { Order = i }))
            .ToList();
    }

    private static RelationGroup ParseRelationGroup(string? relationGroup) =>
        relationGroup is not null && Enum.TryParse<RelationGroup>(relationGroup, out var parsed) ? parsed : RelationGroup.Other;

    private sealed record RelatedEntryCandidate(AnimeMetadata Anime, RelationGroup Group, string RelationType);

    /// <summary>Every anime a main-line member relates to by a relation the
    /// series traversal doesn't follow — <c>character</c>, <c>adaptation</c>,
    /// a non-companion <c>other</c>, any unrecognized relation string, and,
    /// since <see cref="SeriesRelations.TraversalSet"/> narrowed back to story
    /// relations alone, <c>alternative_version</c>/<c>alternative_setting</c>
    /// to a version neighbour that isn't (yet, or ever going to be) a member
    /// of this series (rebuild-series-by-story-component design.md decision
    /// D2) — read in both directions, exactly as
    /// <see cref="Services.Relations.RelationResolver.GetEdgesAsync"/>
    /// reads an anime's own relation set (design.md D5, task 7.3). An
    /// outgoing edge (a main-line member's own row) already carries the far
    /// end's title/picture/media type as MAL reported them; an incoming edge
    /// (another anime's row pointing at a main-line member) instead reads the
    /// *owner's* own cached metadata, since that row describes the main-line
    /// member, not its owner — and the owner always has a cached row, because
    /// a relation row can only exist because its owner was once full-detail
    /// fetched. Never requires a fetch: an outgoing far end with no cache row
    /// renders from the relation row's own snapshot. Excludes anything
    /// already a member of this series (task 7.4's "shown once, as a
    /// member").</summary>
    private async Task<List<RelatedEntryCandidate>> ProjectRelatedEntriesAsync(
        List<SeriesMember> mainLineMembers, HashSet<int> memberAnimeIds, CancellationToken ct)
    {
        var mainLineIds = mainLineMembers.Select(m => m.AnimeId).ToHashSet();

        var outgoing = mainLineMembers
            .SelectMany(m => m.Anime.RelatedAnime
                .Where(r => !SeriesRelations.TraversalSet.Contains(r.RelationType) && !memberAnimeIds.Contains(r.RelatedAnimeId))
                .Select(r => (FarEndId: r.RelatedAnimeId, r.RelationType, r.Title, r.PictureUrl, r.MediaType)))
            .ToList();

        var incoming = await db.AnimeRelatedAnime.AsNoTracking()
            .Where(r => mainLineIds.Contains(r.RelatedAnimeId) &&
                !SeriesRelations.TraversalSet.Contains(r.RelationType) &&
                !memberAnimeIds.Contains(r.AnimeId))
            .Select(r => new { r.AnimeId, r.RelationType })
            .ToListAsync(ct);

        var farEndIds = outgoing.Select(o => o.FarEndId).Concat(incoming.Select(r => r.AnimeId)).Distinct().ToList();
        if (farEndIds.Count == 0)
            return [];

        var cachedFarEnds = await db.AnimeMetadata.AsNoTracking()
            .Include(a => a.UserEntry)
            .Where(a => farEndIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, ct);

        var edgesByFarEndId = new Dictionary<int, List<(string RelationType, bool ExtraIsOwner)>>();
        foreach (var edge in outgoing)
        {
            if (!edgesByFarEndId.TryGetValue(edge.FarEndId, out var list))
                edgesByFarEndId[edge.FarEndId] = list = [];
            list.Add((edge.RelationType, ExtraIsOwner: false)); // the main-line member owns this edge
        }
        foreach (var row in incoming)
        {
            if (!edgesByFarEndId.TryGetValue(row.AnimeId, out var list))
                edgesByFarEndId[row.AnimeId] = list = [];
            list.Add((row.RelationType, ExtraIsOwner: true)); // the far end owns this edge
        }

        var candidates = new List<RelatedEntryCandidate>();
        foreach (var (farEndId, edges) in edgesByFarEndId)
        {
            var winningEdge = edges.OrderBy(e => SeriesRelationGroupOrder.GroupOf(SeriesRelations.ResolveDirectional(e.RelationType, e.ExtraIsOwner))).First();
            var group = SeriesRelations.ResolveDirectional(winningEdge.RelationType, winningEdge.ExtraIsOwner);
            var anime = cachedFarEnds.TryGetValue(farEndId, out var cached)
                ? cached
                : BuildSyntheticRelatedAnime(farEndId, outgoing);
            candidates.Add(new RelatedEntryCandidate(anime, group, winningEdge.RelationType));
        }

        return candidates;
    }

    // An outgoing-only far end with no cached row at all renders purely from
    // the relation row's own snapshot (design.md D5: "no fetch is needed").
    // Never called for an incoming-only far end, which is always cached (see
    // ProjectRelatedEntriesAsync's doc comment).
    private static AnimeMetadata BuildSyntheticRelatedAnime(
        int farEndId, List<(int FarEndId, string RelationType, string Title, string? PictureUrl, string? MediaType)> outgoing)
    {
        var snapshot = outgoing.First(o => o.FarEndId == farEndId);
        return new AnimeMetadata { Id = farEndId, Title = snapshot.Title, PictureUrl = snapshot.PictureUrl, MediaType = snapshot.MediaType };
    }

    private static SeriesEntryDto ToRelatedEntryDto(RelatedEntryCandidate candidate, Dictionary<int, int?> airedEpisodesByAnimeId)
    {
        var anime = candidate.Anime;
        return new SeriesEntryDto(
            anime.Id,
            anime.Title,
            anime.EnglishTitle,
            anime.PictureUrl,
            anime.MediaType,
            anime.AiringStatus,
            anime.TotalEpisodes,
            anime.AverageEpisodeDurationSeconds,
            anime.AiredFrom,
            anime.AiredTo,
            anime.MalScore,
            candidate.RelationType,
            Order: 0, // reassigned by BuildDisplayExtrasAsync's merge sort
            airedEpisodesByAnimeId.GetValueOrDefault(anime.Id),
            anime.UserEntry is null ? null : UserAnimeEntryDto.FromEntity(anime.UserEntry),
            candidate.Group.ToString(),
            IsRelatedEntry: true,
            OpensOwnSeries: false, // every related entry opens the anime's own detail page (design.md D7)
            VersionSlotKey: null,
            BranchHeadAnimeId: null,
            GlobalRank: null); // never enters the favourite tie-break or any other figure (design.md D5)
    }

    // Per-member AiredEpisodes (design.md decision 1) for every series member,
    // main line and extras alike, so both the entry rows and the aggregate
    // stat below read from one map instead of disagreeing. Only a
    // currently-airing member hits IEpisodeScheduleService — typically zero
    // or one per series.
    private async Task<Dictionary<int, int?>> AiredEpisodesByAnimeIdAsync(List<AnimeMetadata> allAnime, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var result = new Dictionary<int, int?>();

        foreach (var anime in allAnime)
        {
            result[anime.Id] = anime.AiringStatus switch
            {
                "finished_airing" => anime.TotalEpisodes,
                "currently_airing" => await CurrentlyAiringEpisodesAsync(anime, now, ct),
                "not_yet_aired" => 0,
                _ => null,
            };
        }

        return result;
    }

    private async Task<int?> CurrentlyAiringEpisodesAsync(AnimeMetadata anime, DateTimeOffset now, CancellationToken ct)
    {
        var aired = await scheduleService.EpisodesAiredAsOfAsync(anime, now, ct);
        if (aired is null)
            return null;

        return anime.TotalEpisodes is { } total ? Math.Min(aired.Value, total) : aired;
    }

    // Kept distinct from the per-entry AiredEpisodes above: a member with an
    // unknown TotalEpisodes contributes 0 here even when its own
    // AiredEpisodes is known, which is what keeps aired ≤ total on the bar
    // (design.md decision 1).
    private static int MainLineAiredEpisodesFromMap(List<SeriesMember> mainLineMembers, Dictionary<int, int?> airedEpisodesByAnimeId)
    {
        var airedEpisodes = 0;

        foreach (var member in mainLineMembers)
        {
            var anime = member.Anime;
            if (anime.TotalEpisodes is not { } total)
                continue;

            airedEpisodes += anime.AiringStatus switch
            {
                "finished_airing" => total,
                "currently_airing" => Math.Min(airedEpisodesByAnimeId.GetValueOrDefault(anime.Id) ?? 0, total),
                _ => 0,
            };
        }

        return airedEpisodes;
    }

    private static SeriesEntryDto ToEntryDto(
        SeriesMember member, HashSet<int> memberAnimeIds, Dictionary<int, int?> airedEpisodesByAnimeId,
        Dictionary<int, int?> globalRankByAnimeId)
    {
        var anime = member.Anime;
        return new SeriesEntryDto(
            anime.Id,
            anime.Title,
            anime.EnglishTitle,
            anime.PictureUrl,
            anime.MediaType,
            anime.AiringStatus,
            anime.TotalEpisodes,
            anime.AverageEpisodeDurationSeconds,
            anime.AiredFrom,
            anime.AiredTo,
            anime.MalScore,
            RelationTypeWithinSeries(anime, memberAnimeIds),
            member.Order,
            airedEpisodesByAnimeId.GetValueOrDefault(anime.Id),
            anime.UserEntry is null ? null : UserAnimeEntryDto.FromEntity(anime.UserEntry),
            member.RelationGroup,
            IsRelatedEntry: false,
            OpensOwnSeries: member.MembershipKind == nameof(MembershipKind.NeighbourTelling),
            member.VersionSlotKey,
            member.BranchHeadAnimeId,
            GlobalRank: globalRankByAnimeId.GetValueOrDefault(anime.Id));
    }

    // Only this anime's own relation rows are considered, so a member
    // reached solely by a reverse edge (its own relations were never
    // fetched) reports no relation type here rather than guessing one.
    private static string? RelationTypeWithinSeries(AnimeMetadata anime, HashSet<int> memberAnimeIds) =>
        anime.RelatedAnime
            .Where(r => SeriesRelations.IsTraversable(r.RelationType) && memberAnimeIds.Contains(r.RelatedAnimeId))
            .OrderBy(r => r.RelationType, StringComparer.Ordinal)
            .Select(r => r.RelationType)
            .FirstOrDefault();

    // --- Status pill (3.4) ---

    // Internal (not private), like BuildStats above, so tests can exercise
    // the precedence directly against plain in-memory models. Delegates to
    // SeriesStatusRules (design.md D5 of add-series-browser) so the series
    // list projection can compute the same precedence without loading
    // navigation-property-bearing entities.
    internal static string ComputeStatus(List<AnimeMetadata> mainLineMembers, List<AnimeMetadata> members) =>
        SeriesStatusRules.Compute(
            mainLineMembers.Select(a => a.AiringStatus),
            members.Select(a => a.AiringStatus));

    // The last year is the latest entry's *end* year (AiredTo), not the start
    // year of whichever entry started airing most recently — a multi-cour or
    // still-running entry's AiredFrom.Year understates how far the franchise
    // actually runs. Falls back to AiredFrom when AiredTo isn't known yet
    // (currently airing or not yet aired).
    private static (int? First, int? Last) YearSpan(List<AnimeMetadata> members)
    {
        var aired = members.Where(a => a.AiredFrom is not null).ToList();
        if (aired.Count == 0) return (null, null);
        var firstYear = aired.Min(a => a.AiredFrom!.Value.Year);
        var lastYear = aired.Max(a => (a.AiredTo ?? a.AiredFrom!.Value).Year);
        return (firstYear, lastYear);
    }

    // --- Score averages (3.2) ---

    private static SeriesScoresDto BuildScores(List<SeriesMember> mainLineMembers, List<SeriesMember> allMembers) =>
        new(
            MalMain: SeriesAverages.Mal(mainLineMembers.Select(m => m.Anime.MalScore)),
            MalAll: SeriesAverages.Mal(allMembers.Select(m => m.Anime.MalScore)),
            MineMain: SeriesAverages.Mine(mainLineMembers.Select(m => m.Anime.UserEntry?.MyScore)),
            MineAll: SeriesAverages.Mine(allMembers.Select(m => m.Anime.UserEntry?.MyScore)));

    // --- Stats (3.3; split into a pick-scoped half and a whole-franchise
    // half by rebuild-series-by-story-component design.md D6, task 7.3) ---

    // Internal (not private) so tests can exercise the aired-count fallback
    // and tied-stat computations directly against plain in-memory models,
    // without standing up a database.
    //
    // mainLineMembers is the whole, unfiltered main line — every alternative
    // of every slot included — and feeds only the figures design.md D6 says
    // span the whole franchise regardless of pick: the favourite/highest-
    // score/most-rewatched tie-break order below. visibleMainLineMembers is
    // one pick's trunk-plus-picked-branches subset (see
    // VisibleMainLineMembers) and feeds every other main-line figure.
    // ProjectAsync calls this once per admissible combination (task 7.3);
    // for a series whose main line holds no slot the two lists are the same
    // list, and every figure covers the whole main line exactly as before
    // this capability.
    //
    // globalRankByAnimeId is the anime-ranking capability's whole-library
    // rank per anime (design.md D?) — the favourite tie-break's source below.
    // Optional/nullable so the existing BuildStats tests, which don't care
    // about favourite tie order, need no dictionary of their own.
    internal static SeriesStatsDto BuildStats(
        List<SeriesMember> mainLineMembers, List<SeriesMember> visibleMainLineMembers, List<SeriesMember> extraMembers,
        List<AnimeMetadata> allAnime, Dictionary<int, int?> airedEpisodesByAnimeId,
        IReadOnlyDictionary<int, int?>? globalRankByAnimeId = null)
    {
        var visibleMainLineAnime = visibleMainLineMembers.Select(m => m.Anime).ToList();
        var extraAnime = extraMembers.Select(m => m.Anime).ToList();

        var (mainEpisodes, mainRuntimeSeconds, hasUnknown) = EpisodesAndRuntime(visibleMainLineAnime, airedEpisodesByAnimeId);
        var (extraEpisodes, extraRuntimeSeconds, _) = EpisodesAndRuntime(extraAnime, airedEpisodesByAnimeId);
        var mainLineAiredEpisodes = MainLineAiredEpisodesFromMap(visibleMainLineMembers, airedEpisodesByAnimeId);

        // A Rewatching main-line entry counts as fully watched (polish-rewatch
        // design.md D2), as the greater of its own episodes-watched and its
        // aired-so-far figure — the same rule SeriesRankingIndex applies to
        // the card's badge and My-progress sort, so the badge and these
        // figures beside it can never disagree about the same entry. Governs
        // only the watched side: the episode total, aired figure, and runtime
        // above are untouched.
        var myWatchedEpisodes = visibleMainLineAnime.Sum(a => EffectiveWatchedEpisodes(a, airedEpisodesByAnimeId));
        var myWatchedSeconds = visibleMainLineAnime.Sum(a => (long)EffectiveWatchedEpisodes(a, airedEpisodesByAnimeId) * EpisodeSeconds(a));
        // Orthogonal to the above (design D10): entering Rewatching zeroes
        // episodes-watched and the rewatch count only increments once a run
        // finishes, so EffectiveWatchedEpisodes above contributes exactly the
        // original run while this contributes exactly the rewatches — the two
        // provably can't double-count the same viewing.
        var myRewatchedSeconds = visibleMainLineAnime.Sum(a => (long)WatchMath.RewatchEpisodesIncludingCurrentRun(
            a.UserEntry?.RewatchCount ?? 0, a.TotalEpisodes, a.UserEntry?.EpisodesWatched ?? 0, a.UserEntry?.Status)
            * EpisodeSeconds(a));
        // A rewatch can only follow a completed run, so a Rewatching entry
        // counts as completed here too (polish-rewatch design.md D2) —
        // otherwise this stat would read "5 of 6" beside a "Completed" badge.
        var entriesCompleted = visibleMainLineAnime.Count(a => a.UserEntry?.Status is WatchStatus.Completed or WatchStatus.Rewatching);
        var extrasCompleted = extraAnime.Count(a => a.UserEntry?.Status == WatchStatus.Completed);
        var mainLineSettledByMe = SeriesAverages.MainLineSettledByMe(
            visibleMainLineAnime.Select(a => (a.AiringStatus, a.UserEntry?.Status)));

        var (gapDays, gapFromId, gapToId) = LongestGap(visibleMainLineAnime);

        // Watch order for tie-breaking: the whole main line (already
        // Order-sorted, unfiltered by any pick per design.md D6), then
        // extras (already media-group-then-Order-sorted) — design.md
        // decision 8.
        var orderedMembers = mainLineMembers.Concat(extraMembers).ToList();
        var highestMalIds = TiedTopIds(
            orderedMembers.Where(m => m.Anime.MalScore is not null),
            m => m.Anime.MalScore!.Value);
        var highestMineIds = TiedTopIds(
            orderedMembers.Where(m => m.Anime.UserEntry?.MyScore is > 0),
            m => m.Anime.UserEntry!.MyScore!.Value,
            // Ties break by each anime's rank in the whole-library ranking
            // (polish-... design.md D?), so a series' favourite list agrees
            // with — and a reorder made here moves — the same ranking the
            // ranking editor produces. An anime outside that ranking (never
            // hand-ordered, e.g. dropped or a music entry) sorts after every
            // ranked one; ties within that keep watch order, since OrderBy is
            // a stable sort over the already watch-ordered sequence.
            m => globalRankByAnimeId?.GetValueOrDefault(m.AnimeId) ?? int.MaxValue);
        var mostRewatchedIds = TiedTopIds(
            orderedMembers.Where(m => m.Anime.UserEntry?.RewatchCount is > 0),
            m => m.Anime.UserEntry!.RewatchCount);

        var studios = allAnime
            .Select(a => a.Studio)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct()
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .Select(s => s!)
            .ToList();
        var genres = allAnime
            .SelectMany(a => a.Genres ?? [])
            .Distinct()
            .OrderBy(g => g, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new SeriesStatsDto(
            mainEpisodes,
            mainRuntimeSeconds,
            hasUnknown,
            mainLineAiredEpisodes,
            extraEpisodes,
            extraRuntimeSeconds,
            myWatchedEpisodes,
            myWatchedSeconds,
            myRewatchedSeconds,
            entriesCompleted,
            extrasCompleted,
            mainLineSettledByMe,
            visibleMainLineMembers.Count,
            extraMembers.Count,
            gapDays,
            gapFromId,
            gapToId,
            highestMalIds,
            highestMineIds,
            mostRewatchedIds,
            studios,
            genres);
    }

    // Every member whose comparable key ties the maximum, in the order given
    // (already watch order) unless a tieBreakKey is supplied — used for
    // favourites, which order by global rank first (design.md decision 8).
    private static List<int> TiedTopIds<TKey>(
        IEnumerable<SeriesMember> candidates, Func<SeriesMember, TKey> scoreKey, Func<SeriesMember, int>? tieBreakKey = null)
        where TKey : IComparable<TKey>
    {
        var list = candidates.ToList();
        if (list.Count == 0)
            return [];

        var max = list.Max(scoreKey);
        var tied = list.Where(m => scoreKey(m).CompareTo(max) == 0);
        if (tieBreakKey is not null)
            tied = tied.OrderBy(tieBreakKey);

        return tied.Select(m => m.AnimeId).ToList();
    }

    // An entry with an unknown TotalEpisodes still contributes its known
    // aired-so-far count (0 when that's unknown too) and flips HasUnknown,
    // so the resulting total is a tighter lower bound rather than dropping
    // the entry entirely.
    private static (int Episodes, long RuntimeSeconds, bool HasUnknown) EpisodesAndRuntime(
        List<AnimeMetadata> anime, Dictionary<int, int?> airedEpisodesByAnimeId)
    {
        var episodes = 0;
        var runtimeSeconds = 0L;
        var hasUnknown = false;

        foreach (var a in anime)
        {
            if (a.TotalEpisodes is not { } total)
            {
                hasUnknown = true;
                var aired = airedEpisodesByAnimeId.GetValueOrDefault(a.Id) ?? 0;
                episodes += aired;
                runtimeSeconds += (long)aired * EpisodeSeconds(a);
                continue;
            }

            episodes += total;
            runtimeSeconds += (long)total * EpisodeSeconds(a);
        }

        return (episodes, runtimeSeconds, hasUnknown);
    }

    private static int EpisodeSeconds(AnimeMetadata anime) =>
        anime.AverageEpisodeDurationSeconds ?? ProfileService.AssumedMinutesPerEpisode * 60;

    // Wraps WatchMath.EffectiveWatchedEpisodes (task 3.1/3.4) with this
    // service's own per-anime aired-episodes map, which already carries
    // exactly the figure that helper wants (null = unknown).
    private static int EffectiveWatchedEpisodes(AnimeMetadata anime, Dictionary<int, int?> airedEpisodesByAnimeId) =>
        WatchMath.EffectiveWatchedEpisodes(
            anime.UserEntry?.EpisodesWatched ?? 0,
            airedEpisodesByAnimeId.GetValueOrDefault(anime.Id),
            anime.UserEntry?.Status);

    // mainLineAnime is already in release order (the caller selects it from
    // members ordered by Order), so the longest gap is the largest jump
    // between consecutive dated entries in that sequence.
    private static (int? Days, int? FromAnimeId, int? ToAnimeId) LongestGap(List<AnimeMetadata> mainLineAnime)
    {
        var dated = mainLineAnime.Where(a => a.AiredFrom is not null).ToList();
        if (dated.Count < 2)
            return (null, null, null);

        int? bestGapDays = null;
        int? fromAnimeId = null;
        int? toAnimeId = null;

        for (var i = 0; i < dated.Count - 1; i++)
        {
            var gapDays = dated[i + 1].AiredFrom!.Value.DayNumber - dated[i].AiredFrom!.Value.DayNumber;
            if (bestGapDays is not null && gapDays <= bestGapDays)
                continue;

            bestGapDays = gapDays;
            fromAnimeId = dated[i].Id;
            toAnimeId = dated[i + 1].Id;
        }

        return (bestGapDays, fromAnimeId, toAnimeId);
    }
}
