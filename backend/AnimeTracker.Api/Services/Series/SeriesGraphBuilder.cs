using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Metadata;
using AnimeTracker.Api.Services.Relations;
using Microsoft.EntityFrameworkCore;
// Alias needed because this namespace's last segment ("Series") shadows the
// Models.Series type name.
using SeriesEntity = AnimeTracker.Api.Models.Series;

namespace AnimeTracker.Api.Services.Series;

/// <summary>Derives and persists a franchise from the related-anime graph
/// (design.md's series-page decisions). A build is a bounded BFS from a seed
/// anime over <see cref="SeriesRelations.TraversalSet"/>, followed by
/// main-line classification, watch-order assignment, and a wholesale
/// member-set replace that absorbs any overlapping stored series. Callers
/// (<c>SeriesService</c>) decide when to build and which fetch budget to
/// spend — this class only knows how.</summary>
public class SeriesGraphBuilder(
    AnimeTrackerDbContext db,
    IMetadataRefreshService refreshService,
    IRelationResolver relationResolver,
    ILogger<SeriesGraphBuilder> logger)
{
    // A runaway-component safety ceiling, not a working limit: set high
    // enough that no real franchise reaches it, so reaching it means the
    // traversal has gone wrong and the truncation notice is meaningful
    // (design.md decision 1).
    public const int MemberCap = 400;
    public const int VisitFetchBudget = 8;
    public const int RebuildFetchBudget = 20;

    // A separate budget spent only on resolving the media type of an `other`
    // edge's uncached far end (design.md D5c) — never shared with the member
    // fetch budget above, so a franchise with many `other` edges to
    // commercials can't starve the fetches real, story-related members need
    // just to be displayed at all.
    public const int VisitProbeBudget = 4;
    public const int RebuildProbeBudget = 10;

    // Bump this to the ship date whenever ClassifyMainLineChain's rules
    // change: SeriesService.NeedsBuild treats every series built before this
    // timestamp as needing a rebuild, so a classification correction reaches
    // already-stored series on their next read instead of requiring the user
    // to find and rebuild each one by hand (design.md decision 3).
    public static readonly DateTimeOffset ClassificationRevisedAt = new(2026, 8, 25, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Builds and persists the series reachable from
    /// <paramref name="seedAnimeId"/>, spending at most <paramref name="fetchBudget"/>
    /// live MAL fetches on members with no cached row at all. Returns null
    /// when the seed's component contains no other member — that's "not part
    /// of a series", not a one-member series. <paramref name="expandLeanMembers"/>
    /// additionally spends budget re-fetching already-cached members with zero
    /// outgoing relations (see <see cref="TraverseAsync"/>) — callers pass
    /// this on every build, not just an explicit rebuild, because a lean
    /// member with unfetched relations is exactly what fragments a franchise
    /// or hands the main line to the wrong chain (decision 2); the smaller
    /// visit-triggered budget just means it self-heals over a few visits
    /// instead of all at once. <paramref name="probeBudget"/> is spent only
    /// on resolving the media type of an `other` edge's uncached far end
    /// (design.md D5c) — see <see cref="TraverseAsync"/>.</summary>
    public async Task<SeriesEntity?> BuildAsync(
        int seedAnimeId, int fetchBudget, int probeBudget, bool expandLeanMembers, CancellationToken ct = default)
    {
        var (members, isPartial, isTruncated) = await TraverseAsync(seedAnimeId, fetchBudget, probeBudget, expandLeanMembers, ct);

        if (members.Count <= 1)
            return null;

        var (mainLineIds, rootAnimeId, orderByAnimeId) = Classify(members);

        return await PersistAsync(members, mainLineIds, orderByAnimeId, rootAnimeId, isPartial, isTruncated, ct);
    }

    // --- Traversal (2.2, 2.3, 2.4) ---

    private async Task<(List<AnimeMetadata> Members, bool IsPartial, bool IsTruncated)> TraverseAsync(
        int seedAnimeId, int fetchBudget, int probeBudget, bool expandLeanMembers, CancellationToken ct)
    {
        var visited = new HashSet<int> { seedAnimeId };
        var queue = new Queue<int>();
        queue.Enqueue(seedAnimeId);

        var members = new List<AnimeMetadata>();
        var fetchesUsed = 0;
        var probesUsed = 0;
        var isPartial = false;
        var isTruncated = false;

        while (queue.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var animeId = queue.Dequeue();

            var metadata = await LoadWithRelationsAsync(animeId, ct);
            if (metadata is null)
            {
                // No cached row at all: this member can't be shown without a
                // fetch. Spend the budget first here, per design.md decision 7 —
                // a lean row (handled below, once metadata is non-null) never
                // costs a fetch, only a missing row does.
                if (fetchesUsed >= fetchBudget)
                {
                    isPartial = true;
                    continue;
                }

                try
                {
                    await refreshService.RefreshOneAsync(animeId, ct);
                    fetchesUsed++;
                    metadata = await LoadWithRelationsAsync(animeId, ct);
                }
                catch (AnimeMetadataNotFoundException)
                {
                    // Genuinely invalid/removed on MAL — retrying won't help,
                    // so this doesn't mark the series partial.
                    logger.LogInformation("Series build: anime {AnimeId} was not found on MAL; skipping.", animeId);
                    continue;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Series build: failed to fetch anime {AnimeId}; will retry on next build.", animeId);
                    isPartial = true;
                    continue;
                }

                if (metadata is null)
                {
                    isPartial = true;
                    continue;
                }
            }
            else if (expandLeanMembers && metadata.RelatedAnime.Count == 0 && fetchesUsed < fetchBudget)
            {
                // This member already has a row but zero outgoing relations —
                // a lean upsert (season/top-anime browsing) that was never
                // full-fetched. Left alone, it contributes no edges to the
                // sequel/prequel subgraph main-line classification depends on
                // (decision 2), which can fragment a franchise into multiple
                // stored series, drop a real season entirely (nothing else in
                // the graph points back at it), or hand the main line to an
                // unrelated chain (e.g. a run of recap movies) simply because
                // those happened to be full-fetched and this wasn't. Worth
                // spending a fetch here on every build, not just a rebuild,
                // rather than waiting on the user to have separately opened
                // this anime's own detail page or clicked Rebuild.
                try
                {
                    await refreshService.RefreshOneAsync(animeId, ct);
                    fetchesUsed++;
                    metadata = await LoadWithRelationsAsync(animeId, ct) ?? metadata;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Series rebuild: failed to expand lean member {AnimeId}; using cached data.", animeId);
                }
            }

            members.Add(metadata);

            // A lean row's own RelatedAnime is empty (never populated by a
            // lean upsert), so it naturally contributes no outgoing edges here
            // without any special-casing — it's still admitted as a member
            // above, just not expanded from its own side, unless
            // expandLeanMembers just upgraded it above.
            var storyOutgoingIds = metadata.RelatedAnime
                .Where(r => SeriesRelations.IsTraversable(r.RelationType))
                .Select(r => r.RelatedAnimeId);

            // Companion-media `other` edges (design.md decision 1, widened to
            // `pv` by polish-rewatch-more-and-filters design.md D5a): one
            // batched lookup of the cached MediaType of this node's
            // `other`-relation neighbours, not a per-edge query. A neighbour
            // with no cached row at all falls to the probe pass below rather
            // than being silently treated as not traversable — otherwise an
            // uncached `music`/`pv` far end could never be recognised at all
            // (design.md D5c).
            var otherRelationIds = metadata.RelatedAnime
                .Where(r => r.RelationType == "other")
                .Select(r => r.RelatedAnimeId)
                .Distinct()
                .ToList();

            var otherNeighbourMediaTypes = otherRelationIds.Count > 0
                ? await db.AnimeMetadata.AsNoTracking()
                    .Where(a => otherRelationIds.Contains(a.Id))
                    .Select(a => new { a.Id, a.MediaType })
                    .ToDictionaryAsync(a => a.Id, a => a.MediaType, ct)
                : [];

            var companionOutgoingIds = otherRelationIds
                .Where(id => otherNeighbourMediaTypes.TryGetValue(id, out var neighbourMediaType) &&
                    SeriesRelations.IsTraversableOtherEdge(metadata.MediaType, neighbourMediaType))
                .ToList();

            // Probe pass (design.md D5c, tasks 4.3/4.4): an `other` far end
            // with no cached row at all can never be recognised as companion
            // media without a fetch, and never spending one leaves the rule
            // permanently inert for a franchise whose promos/theme songs
            // happen to be uncached — a closed loop. A probe caches the row
            // regardless of the verdict, so an edge is probed at most once
            // ever, across all builds — this is what makes a budget this
            // small workable. Ids already visited are skipped: already a
            // member (or already decided against) through some other path,
            // so probing again would spend budget for no gain.
            var uncachedOtherIds = otherRelationIds.Where(id => !otherNeighbourMediaTypes.ContainsKey(id) && !visited.Contains(id));
            foreach (var farEndId in uncachedOtherIds)
            {
                if (probesUsed >= probeBudget)
                {
                    // Unprobed `other` far ends remain: the existing
                    // "partial rebuilds on next visit" mechanism finishes the
                    // job, a few probes at a time, without a background job.
                    isPartial = true;
                    continue;
                }

                try
                {
                    await refreshService.RefreshOneAsync(farEndId, ct);
                    probesUsed++;
                    var probedMediaType = await db.AnimeMetadata.AsNoTracking()
                        .Where(a => a.Id == farEndId)
                        .Select(a => a.MediaType)
                        .FirstOrDefaultAsync(ct);
                    if (SeriesRelations.IsTraversableOtherEdge(metadata.MediaType, probedMediaType))
                        companionOutgoingIds.Add(farEndId); // admitted using the row the probe already produced
                }
                catch (AnimeMetadataNotFoundException)
                {
                    // Genuinely invalid/removed on MAL — retrying won't help,
                    // so this doesn't mark the series partial (mirrors the
                    // member fetch path above).
                    logger.LogInformation("Series build: probed anime {AnimeId} was not found on MAL; skipping.", farEndId);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Series build: failed to probe anime {AnimeId}; will retry on next build.", farEndId);
                    isPartial = true;
                }
            }

            var outgoingIds = storyOutgoingIds.Concat(companionOutgoingIds);

            var incomingIds = await db.AnimeRelatedAnime.AsNoTracking()
                .Where(r => r.RelatedAnimeId == animeId &&
                    (SeriesRelations.TraversalSet.Contains(r.RelationType) ||
                        (r.RelationType == "other" &&
                            SeriesRelations.CompanionMediaTypes.Contains(r.Anime.MediaType ?? "") !=
                            SeriesRelations.CompanionMediaTypes.Contains(metadata.MediaType ?? ""))))
                .Select(r => r.AnimeId)
                .ToListAsync(ct);

            // AniList adjudication can contradict an edge MAL stores one-sided
            // (relation-confidence spec) — that edge stays stored and visible
            // in the More overlay, but is never traversed into a series. Uses
            // the same resolver the detail page ranks over, so the "Series"
            // link can never disagree with the page it leads to.
            var contradictedFarEndIds = (await relationResolver.GetEdgesAsync(metadata, ct))
                .Where(e => e.Confidence == RelationConfidence.Contradicted)
                .Select(e => e.AnimeId)
                .ToHashSet();

            foreach (var neighbourId in outgoingIds.Concat(incomingIds).Distinct())
            {
                if (contradictedFarEndIds.Contains(neighbourId))
                    continue;

                if (visited.Contains(neighbourId))
                    continue;

                if (visited.Count >= MemberCap)
                {
                    isTruncated = true;
                    continue;
                }

                visited.Add(neighbourId);
                queue.Enqueue(neighbourId);
            }
        }

        return (members, isPartial, isTruncated);
    }

    private Task<AnimeMetadata?> LoadWithRelationsAsync(int animeId, CancellationToken ct) =>
        db.AnimeMetadata.Include(a => a.RelatedAnime).FirstOrDefaultAsync(a => a.Id == animeId, ct);

    // --- Main-line classification, watch order, root (2.5, 2.6, 2.7) ---

    private static (HashSet<int> MainLineIds, int RootAnimeId, Dictionary<int, int> OrderByAnimeId) Classify(
        List<AnimeMetadata> members)
    {
        var memberById = members.ToDictionary(m => m.Id);
        var mainLineIds = ClassifyMainLineChain(members, memberById);

        var mainLineMembers = members.Where(m => mainLineIds.Contains(m.Id)).ToList();
        var mainLineOrdered = TopologicalMainLineOrder(mainLineMembers, mainLineIds, memberById);
        var orderByAnimeId = new Dictionary<int, int>();
        for (var i = 0; i < mainLineOrdered.Count; i++)
            orderByAnimeId[mainLineOrdered[i].Id] = i;

        var extrasByGroup = members
            .Where(m => !mainLineIds.Contains(m.Id))
            .OrderBy(m => SeriesMediaTypeOrder.GroupOf(m.MediaType))
            .ThenBy(OrderKey)
            .GroupBy(m => SeriesMediaTypeOrder.GroupOf(m.MediaType));
        foreach (var group in extrasByGroup)
        {
            var i = 0;
            foreach (var member in group)
                orderByAnimeId[member.Id] = i++;
        }

        return (mainLineIds, mainLineOrdered[0].Id, orderByAnimeId);
    }

    /// <summary>The sequel/prequel chain ranked highest by its count of
    /// main-line-eligible members — ties broken by the chain's earliest-aired
    /// eligible member, falling back to its earliest member of any kind when
    /// it has none — reduced to just those eligible members. A member is
    /// ineligible when its media type is `special`/`music`, it's a recap
    /// (<see cref="SeriesRelations.FindRecapIds"/>), or it's side content of
    /// another member (<see cref="SeriesRelations.FindSideContentIds"/>). The chain graph itself still
    /// includes every member regardless of eligibility, so an ineligible
    /// entry that bridges two seasons keeps them in one chain without ever
    /// being main line itself (design.md decision 2). Falls back to the
    /// unfiltered chain when reduction empties it out, so a specials-only or
    /// side-content-only franchise still has a main line to render.
    ///
    /// Ranking by eligible count rather than raw chain size is what resolves
    /// a franchise with no sequel/prequel edges at all: when every member
    /// links only by parent_story/side_story — a show plus its promotional
    /// shorts, say — every member is its own one-node chain, so raw size
    /// ties them all at one and an earliest-aired tie-break would hand the
    /// main line to whichever short happened to air first. Eligible-count
    /// ranking instead scores the show's chain 1 and every promo chain 0, so
    /// the show wins regardless of air date.
    ///
    /// Chain candidates are restricted to ones containing at least one
    /// eligible `tv` member when any exist. A long-running single-entry show
    /// (one TV series plus many movies/specials, e.g. One Piece) has no
    /// sequel/prequel edges on the TV entry itself — nothing MAL would call a
    /// "season" of it exists as a separate entry — so every node, including
    /// the TV entry, starts out as its own singleton chain. Left
    /// unrestricted, a handful of side-story movies/specials that happen to
    /// sequel-chain to *each other* (unrelated to the flagship show) can
    /// out-count that singleton and hijack the main line entirely. Requiring
    /// an eligible `tv` member is a no-op for the ordinary case (a real
    /// season chain is `tv` and eligible by construction) and falls back to
    /// the unrestricted set when nothing in the component is eligible `tv` at
    /// all (a movie-only franchise).</summary>
    private static HashSet<int> ClassifyMainLineChain(List<AnimeMetadata> members, Dictionary<int, AnimeMetadata> memberById)
    {
        var adjacency = memberById.Keys.ToDictionary(id => id, _ => new HashSet<int>());
        foreach (var member in members)
        {
            foreach (var relation in member.RelatedAnime)
            {
                if (relation.RelationType is not ("sequel" or "prequel"))
                    continue;
                if (!memberById.ContainsKey(relation.RelatedAnimeId))
                    continue;

                adjacency[member.Id].Add(relation.RelatedAnimeId);
                adjacency[relation.RelatedAnimeId].Add(member.Id);
            }
        }

        var candidateIds = memberById.Keys.ToHashSet();
        var ownEdges = members
            .SelectMany(m => m.RelatedAnime.Select(r => (OwnerId: m.Id, r.RelatedAnimeId, r.RelationType)))
            .ToList();
        var ineligibleIds = SeriesRelations.FindRecapIds(ownEdges, candidateIds);
        ineligibleIds.UnionWith(SeriesRelations.FindSideContentIds(ownEdges, candidateIds));
        foreach (var member in members)
        {
            // `pv` (promotional video) joins `special`/`music` here
            // (polish-rewatch-more-and-filters design.md D5b): a promo is
            // never a chapter of the story, however MAL relates it — even in
            // a franchise whose real entries carry no sequel/prequel edges at
            // all, where a chain of promos could otherwise out-rank the show.
            if (member.MediaType is "special" or "music" or "pv")
                ineligibleIds.Add(member.Id);
        }
        bool IsEligible(int id) => !ineligibleIds.Contains(id);

        var chains = FindConnectedComponents(memberById.Keys, adjacency);
        var tvChains = chains.Where(chain => chain.Any(id => IsEligible(id) && memberById[id].MediaType == "tv")).ToList();
        var candidateChains = tvChains.Count > 0 ? tvChains : chains;
        var winningChain = candidateChains
            .OrderByDescending(chain => chain.Count(IsEligible))
            .ThenBy(chain => ChainTieBreakKey(chain, memberById, IsEligible))
            .First();

        var reduced = winningChain.Where(IsEligible).ToHashSet();
        return reduced.Count > 0 ? reduced : winningChain.ToHashSet();
    }

    private static (int HasNoAiredDate, int AiredDayNumber, int AnimeId) ChainTieBreakKey(
        List<int> chain, Dictionary<int, AnimeMetadata> memberById, Func<int, bool> isEligible)
    {
        var eligibleIds = chain.Where(isEligible).ToList();
        var candidateIds = eligibleIds.Count > 0 ? eligibleIds : chain;
        return candidateIds.Min(id => OrderKey(memberById[id]));
    }

    private static List<List<int>> FindConnectedComponents(IEnumerable<int> nodeIds, Dictionary<int, HashSet<int>> adjacency)
    {
        var visited = new HashSet<int>();
        var components = new List<List<int>>();

        foreach (var start in nodeIds)
        {
            if (!visited.Add(start))
                continue;

            var component = new List<int> { start };
            var queue = new Queue<int>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var neighbour in adjacency[current])
                {
                    if (visited.Add(neighbour))
                    {
                        component.Add(neighbour);
                        queue.Enqueue(neighbour);
                    }
                }
            }

            components.Add(component);
        }

        return components;
    }

    // Aired-from ascending, nulls last, MAL id as tiebreak (design.md decision 3).
    private static (int HasNoAiredDate, int AiredDayNumber, int AnimeId) OrderKey(AnimeMetadata anime) =>
        anime.AiredFrom is { } date ? (0, date.DayNumber, anime.Id) : (1, 0, anime.Id);

    /// <summary>Main-line order as a stable topological sort (Kahn's
    /// algorithm) over `sequel`/`prequel` edges among main-line members,
    /// rather than a plain air-date sort — the main line is story order, not
    /// release order (design.md decision 9). The ready set is a priority
    /// queue keyed by <see cref="OrderKey"/>, so: a chain edge constrains
    /// order absolutely; members unconstrained relative to each other fall
    /// back to air date; a member with no chain edge at all is placed purely
    /// by air date, interleaved rather than appended. `sequel`/`prequel` are
    /// each other's mirror, so `A --sequel--> B` and `B --prequel--> A` are
    /// the same constraint counted once, not two.
    ///
    /// MAL relation data can contain a `sequel`/`prequel` cycle; this must
    /// not throw on one. When the queue empties with nodes remaining (a
    /// cycle), the remainder is emitted in <see cref="OrderKey"/> order.</summary>
    private static List<AnimeMetadata> TopologicalMainLineOrder(
        List<AnimeMetadata> mainLineMembers, HashSet<int> mainLineIds, Dictionary<int, AnimeMetadata> memberById)
    {
        var successors = mainLineIds.ToDictionary(id => id, _ => new HashSet<int>());
        var inDegree = mainLineIds.ToDictionary(id => id, _ => 0);

        foreach (var member in mainLineMembers)
        {
            foreach (var relation in member.RelatedAnime)
            {
                if (!mainLineIds.Contains(relation.RelatedAnimeId))
                    continue;

                int predecessorId, successorId;
                switch (relation.RelationType)
                {
                    case "sequel": // member --sequel--> related: related follows member
                        predecessorId = member.Id;
                        successorId = relation.RelatedAnimeId;
                        break;
                    case "prequel": // member --prequel--> related: related precedes member
                        predecessorId = relation.RelatedAnimeId;
                        successorId = member.Id;
                        break;
                    default:
                        continue;
                }

                if (successors[predecessorId].Add(successorId))
                    inDegree[successorId]++;
            }
        }

        var remainingInDegree = new Dictionary<int, int>(inDegree);
        var ready = new PriorityQueue<int, (int, int, int)>();
        foreach (var id in mainLineIds)
        {
            if (remainingInDegree[id] == 0)
                ready.Enqueue(id, OrderKey(memberById[id]));
        }

        var order = new List<int>();
        while (ready.Count > 0)
        {
            var id = ready.Dequeue();
            order.Add(id);

            foreach (var successorId in successors[id])
            {
                if (--remainingInDegree[successorId] == 0)
                    ready.Enqueue(successorId, OrderKey(memberById[successorId]));
            }
        }

        if (order.Count < mainLineIds.Count)
        {
            var emitted = order.ToHashSet();
            var remainder = mainLineIds.Where(id => !emitted.Contains(id)).OrderBy(id => OrderKey(memberById[id]));
            order.AddRange(remainder);
        }

        return order.Select(id => memberById[id]).ToList();
    }

    // --- Persistence (2.8) ---

    /// <summary>Finds any stored <see cref="Series"/> rows overlapping the new
    /// component, keeps the one with the largest overlap (preserving its Id),
    /// deletes the others, and replaces the kept row's member set wholesale —
    /// all in the one transaction <see cref="AnimeTrackerDbContext.SaveChangesAsync"/>
    /// opens implicitly (design.md decision 6).</summary>
    private async Task<SeriesEntity> PersistAsync(
        List<AnimeMetadata> members,
        HashSet<int> mainLineIds,
        Dictionary<int, int> orderByAnimeId,
        int rootAnimeId,
        bool isPartial,
        bool isTruncated,
        CancellationToken ct)
    {
        var memberIds = members.Select(m => m.Id).ToList();

        var overlapCounts = await db.SeriesMembers.AsNoTracking()
            .Where(sm => memberIds.Contains(sm.AnimeId))
            .GroupBy(sm => sm.SeriesId)
            .Select(g => new { SeriesId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        SeriesEntity target;
        if (overlapCounts.Count > 0)
        {
            var bestSeriesId = overlapCounts.OrderByDescending(x => x.Count).ThenBy(x => x.SeriesId).First().SeriesId;
            target = await db.Series.FirstAsync(s => s.Id == bestSeriesId, ct);

            var staleSeriesIds = overlapCounts.Select(x => x.SeriesId).Where(id => id != bestSeriesId).ToList();
            if (staleSeriesIds.Count > 0)
            {
                var staleSeries = await db.Series.Where(s => staleSeriesIds.Contains(s.Id)).ToListAsync(ct);
                db.Series.RemoveRange(staleSeries); // cascade-deletes their SeriesMember rows too
            }

            var obsoleteMembers = await db.SeriesMembers
                .Where(sm => sm.SeriesId == bestSeriesId && !memberIds.Contains(sm.AnimeId))
                .ToListAsync(ct);
            db.SeriesMembers.RemoveRange(obsoleteMembers);
        }
        else
        {
            target = new SeriesEntity();
            db.Series.Add(target);
        }

        // Rows that already exist for a member (whether under target or an
        // absorbed stale series) are updated in place rather than deleted and
        // recreated, so a member moving between series is a plain FK update.
        // This is also why FavouriteRank survives a rebuild for free: it's
        // simply never touched here for a surviving member, and a member that
        // leaves the series takes its (now-unused) rank with it through the
        // row it already owned (design.md decision 11).
        var existingByAnimeId = (await db.SeriesMembers
                .Where(sm => memberIds.Contains(sm.AnimeId))
                .ToListAsync(ct))
            .ToDictionary(m => m.AnimeId);

        foreach (var member in members)
        {
            var isMainLine = mainLineIds.Contains(member.Id);
            var order = orderByAnimeId[member.Id];

            if (existingByAnimeId.TryGetValue(member.Id, out var existing))
            {
                existing.SeriesId = target.Id;
                existing.IsMainLine = isMainLine;
                existing.Order = order;
            }
            else
            {
                target.Members.Add(new SeriesMember { AnimeId = member.Id, IsMainLine = isMainLine, Order = order });
            }
        }

        target.RootAnimeId = rootAnimeId;
        target.BuiltAt = DateTimeOffset.UtcNow;
        target.IsPartial = isPartial;
        target.IsTruncated = isTruncated;

        await db.SaveChangesAsync(ct);
        return target;
    }
}
