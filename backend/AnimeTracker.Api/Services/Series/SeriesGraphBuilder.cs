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
/// (design.md's series-page decisions). A build traverses a seed's story
/// component (a bounded BFS over <see cref="SeriesRelations.TraversalSet"/>),
/// then its version neighbours one hop out
/// (rebuild-series-by-story-component design.md decisions D1-D3; see
/// <see cref="TraverseAsync"/>), followed by main-line classification,
/// watch-order assignment, and a wholesale member-set replace that absorbs
/// any overlapping stored series. Callers (<c>SeriesService</c>) decide when
/// to build and which fetch budget to spend — this class only knows
/// how.</summary>
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

    // Bump this to the ship date whenever a build's classification of its
    // members changes — main line (ClassifyMainLineChain) or extra relation
    // group (ResolveExtraGroups) alike: SeriesService.NeedsBuild treats every
    // series built before this timestamp as needing a rebuild, so a
    // classification correction reaches already-stored series on their next
    // read instead of requiring the user to find and rebuild each one by
    // hand (design.md decision 3).
    public static readonly DateTimeOffset ClassificationRevisedAt = new(2026, 9, 5, 0, 0, 0, TimeSpan.Zero);

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
        var (members, edges, isPartial, isTruncated, versionNeighbours) =
            await TraverseAsync(seedAnimeId, fetchBudget, probeBudget, expandLeanMembers, ct);

        // A single-node story component with no version neighbour at all
        // isn't part of a series. One with a version neighbour is a series
        // of exactly two, even though neither side has a story chain of its
        // own — the "lone alternative version" case (Clannad Movie, and any
        // pair of standalone works linked only by a version relation) — so
        // it must still build and persist, or ResolveExtraGroups never gets
        // a chance to run and the pairing stays unclassified forever.
        if (members.Count == 0 || (members.Count == 1 && versionNeighbours.Count == 0))
            return null;

        // Story components are disjoint (design.md D1), so a build now
        // derives and persists exactly one series — the traversed component
        // plus its classified version neighbours (design.md D2) — rather
        // than partitioning members across several version-edge anchors.
        var coreMemberIds = members.Select(m => m.Id).ToHashSet();
        var memberById = members.ToDictionary(m => m.Id);
        var versionNeighbourKindByAnimeId = new Dictionary<int, MembershipKind>();
        foreach (var neighbour in versionNeighbours)
        {
            memberById[neighbour.AnimeId] = neighbour.Anime;
            versionNeighbourKindByAnimeId[neighbour.AnimeId] = neighbour.MembershipKind;
        }

        var telling = ClassifyTelling(coreMemberIds, versionNeighbourKindByAnimeId, memberById, edges);
        return await PersistAsync(telling, isPartial, isTruncated, ct);
    }

    // --- Traversal (2.2, 2.3, 2.4; extended to alternative_setting and
    // per-edge relation recording by split-series-by-version tasks 3.1-3.2;
    // narrowed to a two-phase, story-component-only traversal by
    // rebuild-series-by-story-component design.md decisions D1-D3, tasks
    // 4.1-4.3) ---

    /// <summary>Traverses the story component <paramref name="seedAnimeId"/>
    /// belongs to, plus that component's version neighbours (design.md
    /// decision D3). Phase 1 (<see cref="TraverseStoryComponentAsync"/>)
    /// exhausts the seed's own story component first — breadth-first,
    /// spending the whole fetch/probe budget there — before anything else is
    /// considered. Phase 2 then follows every member's version relations one
    /// hop, in both directions, to find and classify
    /// (<see cref="DiscoverVersionNeighboursAsync"/>, <see cref="SeriesVersionNeighbours"/>,
    /// design.md decision D2) that component's version neighbours, spending
    /// no further budget. A version neighbour is never expanded through —
    /// nothing reachable only from one is ever admitted, which is what keeps
    /// a foreign telling's own entries off this build.
    ///
    /// When phase 1 alone yields a single anime, that lone anime's own
    /// version neighbours are checked before giving up on it: if it has any,
    /// the seed and every neighbour are compared by the size of each one's
    /// own story component (<see cref="PickLargestStoryComponentAsync"/>),
    /// and — only when a neighbour's is strictly the largest, or wins that
    /// comparison's episode/id tie-break — the whole traversal is redone
    /// once more seeded from it, so the original seed is folded back into
    /// the result as that component's own version neighbour. This is what
    /// makes opening a lone alternative version (e.g. Clannad Movie) land on
    /// the franchise it belongs to, rather than the "not part of a series"
    /// <see cref="BuildAsync"/> would otherwise report for a lone member.
    /// The seed's own inclusion in that comparison is what keeps a pair of
    /// standalone works linked only by a version relation — neither with a
    /// story chain of its own — resolving to the same one of the two
    /// regardless of which side was opened first, rather than always
    /// flipping onto whichever one happens to be "the other" one. With no
    /// version neighbour either, the lone
    /// anime is returned exactly as phase 1 found it.</summary>
    internal async Task<(List<AnimeMetadata> Members, List<RelationEdge> Edges, bool IsPartial, bool IsTruncated, List<SeriesVersionNeighbours.Neighbour> VersionNeighbours)> TraverseAsync(
        int seedAnimeId, int fetchBudget, int probeBudget, bool expandLeanMembers, CancellationToken ct)
    {
        var (members, edges, isPartial, isTruncated) =
            await TraverseStoryComponentAsync(seedAnimeId, fetchBudget, probeBudget, expandLeanMembers, ct);

        if (members.Count == 1)
        {
            var (soleMemberCandidates, _) = await DiscoverVersionNeighboursAsync(members, ct);
            if (soleMemberCandidates.Count > 0)
            {
                // The seed is a candidate too — its own component is this
                // same lone node — so a pair of standalone works linked only
                // by a version relation (neither with a story chain of its
                // own) resolves the same way regardless of which side of the
                // pair was opened first, rather than always flipping onto
                // whichever one happens to be "the other" one.
                var seedAsCandidate = new SeriesVersionNeighbours.Neighbour
                {
                    AnimeId = seedAnimeId,
                    Anime = members[0],
                    MembershipKind = MembershipKind.Core,
                };
                var reseedAnimeId = await PickLargestStoryComponentAsync(
                    soleMemberCandidates.Prepend(seedAsCandidate).ToList(), ct);
                if (reseedAnimeId != seedAnimeId)
                {
                    (members, edges, isPartial, isTruncated) =
                        await TraverseStoryComponentAsync(reseedAnimeId, fetchBudget, probeBudget, expandLeanMembers, ct);
                }
            }
        }

        if (members.Count == 0)
            return (members, edges, isPartial, isTruncated, []);

        var (versionNeighbours, neighbourEdges) = await DiscoverVersionNeighboursAsync(members, ct);
        edges.AddRange(neighbourEdges);

        return (members, edges, isPartial, isTruncated, versionNeighbours);
    }

    /// <summary>Of <paramref name="candidates"/> — every version neighbour of
    /// a lone seed, plus the seed itself (<see cref="TraverseAsync"/>'s
    /// seed-resolution paragraph) — the one whose own story component is
    /// largest. Each candidate is sized by running phase 1 on it with a zero
    /// fetch/probe budget: still a real traversal over every cached
    /// relation, just unable to grow the component past what's already
    /// cached, so sizing costs nothing (design.md decision D3's "no fetch"
    /// carried over from neighbour classification to the candidate that wins
    /// it). Ties break on the higher total episode count — the fuller work
    /// is the more useful default main line when two standalone works are
    /// alternatives of each other and neither has a story chain — then on
    /// the lower MAL id, so the result doesn't depend on classification
    /// order: Clannad and After Story are each the other's whole component
    /// and settle there, and a pair like a one-episode pilot and its
    /// seven-episode remake settles on the remake regardless of which one
    /// was opened first.</summary>
    private async Task<int> PickLargestStoryComponentAsync(List<SeriesVersionNeighbours.Neighbour> candidates, CancellationToken ct)
    {
        var sized = new List<(int AnimeId, int ComponentSize, int TotalEpisodes)>();
        foreach (var candidate in candidates)
        {
            var (sizingMembers, _, _, _) = await TraverseStoryComponentAsync(
                candidate.AnimeId, fetchBudget: 0, probeBudget: 0, expandLeanMembers: false, ct);
            sized.Add((candidate.AnimeId, sizingMembers.Count, candidate.Anime.TotalEpisodes ?? 0));
        }

        return sized
            .OrderByDescending(s => s.ComponentSize)
            .ThenByDescending(s => s.TotalEpisodes)
            .ThenBy(s => s.AnimeId)
            .First().AnimeId;
    }

    /// <summary>Finds and classifies <paramref name="componentMembers"/>'
    /// version neighbours (design.md decisions D2/D3): every anime linked to
    /// a member by a version relation, in either direction, that isn't
    /// itself one of <paramref name="componentMembers"/>. Followed one hop
    /// only — a candidate's own further relations are read just far enough
    /// to classify it (<see cref="SeriesVersionNeighbours.Classify"/>), never
    /// to keep expanding outward. Costs no fetch or probe: a candidate with
    /// no cached metadata row is silently excluded, exactly as
    /// <see cref="SeriesVersionNeighbours"/> already excludes one. Returns
    /// each admitted neighbour's edge(s) to the component alongside it — in
    /// whichever direction they're declared — for the relation-group
    /// resolution a later change reads them from.</summary>
    private async Task<(List<SeriesVersionNeighbours.Neighbour> Neighbours, List<RelationEdge> Edges)> DiscoverVersionNeighboursAsync(
        List<AnimeMetadata> componentMembers, CancellationToken ct)
    {
        var memberIds = componentMembers.Select(m => m.Id).ToHashSet();

        var outgoingEdges = componentMembers
            .SelectMany(m => m.RelatedAnime
                .Where(r => SeriesRelations.VersionRelations.Contains(r.RelationType) && !memberIds.Contains(r.RelatedAnimeId))
                .Select(r => new RelationEdge(m.Id, r.RelatedAnimeId, r.RelationType)))
            .ToList();

        // The mirror of phase 1's own incoming-edge query, over the version
        // relations instead of the story ones: a candidate whose only stored
        // edge is declared from its own side, not the member's, is still one
        // of this component's version neighbours (design.md D3: "in both
        // directions").
        var incomingEdges = await db.AnimeRelatedAnime.AsNoTracking()
            .Where(r => memberIds.Contains(r.RelatedAnimeId) &&
                SeriesRelations.VersionRelations.Contains(r.RelationType) &&
                !memberIds.Contains(r.AnimeId))
            .Select(r => new RelationEdge(r.AnimeId, r.RelatedAnimeId, r.RelationType))
            .ToListAsync(ct);

        var candidateIds = outgoingEdges.Select(e => e.RelatedAnimeId)
            .Concat(incomingEdges.Select(e => e.OwnerId))
            .Distinct()
            .ToList();
        if (candidateIds.Count == 0)
            return ([], []);

        var cachedNeighbours = await db.AnimeMetadata.AsNoTracking()
            .Include(a => a.RelatedAnime)
            .Where(a => candidateIds.Contains(a.Id))
            .ToListAsync(ct);

        var neighbours = SeriesVersionNeighbours.Classify(componentMembers, cachedNeighbours);
        var admittedIds = neighbours.Select(n => n.AnimeId).ToHashSet();

        var edges = outgoingEdges.Where(e => admittedIds.Contains(e.RelatedAnimeId))
            .Concat(incomingEdges.Where(e => admittedIds.Contains(e.OwnerId)))
            .ToList();

        return (neighbours, edges);
    }

    /// <summary>Phase 1 (design.md decision D3): a bounded BFS from
    /// <paramref name="seedAnimeId"/> over story relations alone — version
    /// relations neither grow nor are grown by this traversal (design.md
    /// decision D1); <see cref="TraverseAsync"/> follows them separately, one
    /// hop, once this phase is done. Unchanged from before this capability
    /// (rebuild-series-by-story-component task 4.1): the fetch/probe
    /// budgets, lean-member expansion, contradicted-edge handling and member
    /// cap below are exactly what they were when this was the whole of
    /// traversal.
    ///
    /// <c>Edges</c> lists every relation, exactly as declared by its
    /// owning end, that connects two members of the traversed component —
    /// story relations and a traversed companion <c>other</c> edge (recorded
    /// as <c>"other"</c>) alike. Traversal is breadth-first from
    /// <paramref name="seedAnimeId"/> (task 3.3), so the telling the build
    /// was seeded from completes — and every one of its edges is recorded —
    /// before budget is spent on distant tellings. Recorded once per owning
    /// member as it's dequeued, from data already in hand, rather than
    /// re-queried afterwards (task 3.2); edges whose far end never became a
    /// member (contradicted, or dropped by a cap/fetch failure) are filtered
    /// out before returning.</summary>
    private async Task<(List<AnimeMetadata> Members, List<RelationEdge> Edges, bool IsPartial, bool IsTruncated)> TraverseStoryComponentAsync(
        int seedAnimeId, int fetchBudget, int probeBudget, bool expandLeanMembers, CancellationToken ct)
    {
        var visited = new HashSet<int> { seedAnimeId };
        var queue = new Queue<int>();
        queue.Enqueue(seedAnimeId);

        var members = new List<AnimeMetadata>();
        var edges = new List<RelationEdge>();
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
            var storyOutgoingEdges = metadata.RelatedAnime
                .Where(r => SeriesRelations.IsTraversable(r.RelationType))
                .Select(r => new RelationEdge(animeId, r.RelatedAnimeId, r.RelationType))
                .ToList();
            var storyOutgoingIds = storyOutgoingEdges.Select(e => e.RelatedAnimeId);

            // Recorded for grouping (fix-alternative-version-grouping
            // design.md D1) but never traversed — see the comment below,
            // where these are folded into edges without their far ends ever
            // reaching outgoingIds.
            var versionOutgoingEdges = metadata.RelatedAnime
                .Where(r => SeriesRelations.VersionRelations.Contains(r.RelationType))
                .Select(r => new RelationEdge(animeId, r.RelatedAnimeId, r.RelationType));

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

            // Only this member's own declared relations are recorded as
            // edges — an edge stored the other way round is covered when its
            // owner is dequeued and processed in its own turn, so every edge
            // in the component is captured exactly once regardless of which
            // end declares it (mirrors SeriesRelations.FindRecapIds/
            // FindSideContentIds's own reliance on that symmetry).
            //
            // versionOutgoingEdges rides along in this same foreach — an
            // AniList-contradicted version edge is filtered exactly as a
            // story edge is — but is deliberately absent from outgoingIds
            // above: it is recorded for ResolveExtraGroups to read, never
            // followed, so a version relation neither grows nor splits the
            // story component (fix-alternative-version-grouping design.md
            // D1). The tail filter below
            // (`edges.Where(e => memberIds.Contains(e.RelatedAnimeId))`) is
            // what then partitions these against
            // DiscoverVersionNeighboursAsync's member→neighbour version
            // edges: a version edge recorded here whose far end never became
            // a member is dropped there, while DiscoverVersionNeighboursAsync
            // records that exact edge itself, under its own
            // `!memberIds.Contains(...)` guard — so one version edge is
            // either member→member (kept here) or member→neighbour (kept
            // there), never both.
            var companionOutgoingEdges = companionOutgoingIds.Select(id => new RelationEdge(animeId, id, "other"));
            foreach (var edge in storyOutgoingEdges.Concat(companionOutgoingEdges).Concat(versionOutgoingEdges))
            {
                if (!contradictedFarEndIds.Contains(edge.RelatedAnimeId))
                    edges.Add(edge);
            }

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

        // Drop edges whose far end never actually became a member — dropped
        // by the member cap, a fetch that failed, or an id that was 404 on
        // MAL — so callers never see an edge pointing at a non-member.
        var memberIds = members.Select(m => m.Id).ToHashSet();
        var finalEdges = edges.Where(e => memberIds.Contains(e.RelatedAnimeId)).ToList();

        return (members, finalEdges, isPartial, isTruncated);
    }

    private Task<AnimeMetadata?> LoadWithRelationsAsync(int animeId, CancellationToken ct) =>
        db.AnimeMetadata.Include(a => a.RelatedAnime).FirstOrDefaultAsync(a => a.Id == animeId, ct);

    // --- Per-telling classification, extras grouping, watch order, root
    // (2.5-2.7; run per telling rather than once per build by
    // split-series-by-version tasks 5.1-5.3) ---

    /// <summary>One build's persisted shape (rebuild-series-by-story-component
    /// design.md D2/D4, tasks 6.1-6.3): its story-component members
    /// (<see cref="CoreMemberIds"/>) plus its version-neighbour members, each
    /// with the <see cref="MembershipKind"/> it was classified with; which of
    /// the component's members are main line; the root; each member's
    /// position within the main line or within its extras relation group;
    /// each extra's relation group; and each main-line member's version slot
    /// and branch. <see cref="FullMemberIds"/> is what actually gets a
    /// <see cref="SeriesMember"/> row — every membership kind is stored,
    /// counted and ordered alike; only <see cref="CoreMemberIds"/> is ever a
    /// candidate for the main line or for identity-matching against a stored
    /// series (design.md D9).
    ///
    /// <see cref="VersionSlotKeyByAnimeId"/>/<see cref="BranchHeadAnimeIdByAnimeId"/>
    /// (design.md D4, tasks 5.2-5.3) are sparse — populated only for a
    /// main-line member that's an alternative of a version slot or a member
    /// of one's branch — mirroring the nullable
    /// <see cref="SeriesMember.VersionSlotKey"/>/<see cref="SeriesMember.BranchHeadAnimeId"/>
    /// columns they feed.</summary>
    private sealed class TellingBuild
    {
        public required HashSet<int> CoreMemberIds { get; init; }
        public required Dictionary<int, MembershipKind> VersionNeighbourKindByAnimeId { get; init; }
        public required HashSet<int> MainLineIds { get; init; }
        public required int RootAnimeId { get; init; }
        public required Dictionary<int, int> OrderByAnimeId { get; init; }
        public required Dictionary<int, RelationGroup> RelationGroupByAnimeId { get; init; }
        public required Dictionary<int, int> VersionSlotKeyByAnimeId { get; init; }
        public required Dictionary<int, int> BranchHeadAnimeIdByAnimeId { get; init; }

        public IEnumerable<int> FullMemberIds => CoreMemberIds.Concat(VersionNeighbourKindByAnimeId.Keys);

        public MembershipKind KindOf(int animeId) =>
            VersionNeighbourKindByAnimeId.GetValueOrDefault(animeId, MembershipKind.Core);
    }

    /// <summary>Classifies the seed's series in isolation: main line over its
    /// story-component members alone — <see cref="ClassifyMainLineChain"/>
    /// only ever sees <paramref name="coreMemberIds"/>, so a version
    /// neighbour can never become main line by construction (task 5.1); each
    /// main-line member's version slot and branch (<see cref="SeriesVersionSlots"/>,
    /// task 5.2), folded into the watch order by contracting each slot to one
    /// shared position (task 5.3); each extra's relation group — core extras
    /// and version-neighbour extras alike — excluding version neighbours from
    /// the inheritance walk (task 5.5); and each member's position —
    /// main-line watch order, or position within its extras relation
    /// group.</summary>
    private static TellingBuild ClassifyTelling(
        HashSet<int> coreMemberIds, Dictionary<int, MembershipKind> versionNeighbourKindByAnimeId,
        Dictionary<int, AnimeMetadata> memberById, List<RelationEdge> edges)
    {
        var coreMembers = coreMemberIds.Select(id => memberById[id]).ToList();
        var coreMemberById = coreMembers.ToDictionary(m => m.Id);

        var mainLineIds = ClassifyMainLineChain(coreMembers, coreMemberById);

        var mainLineMembers = coreMembers.Where(m => mainLineIds.Contains(m.Id)).ToList();
        var slots = SeriesVersionSlots.Resolve(mainLineMembers, mainLineIds, coreMemberById);
        var mainLineOrdered = TopologicalMainLineOrder(mainLineMembers, mainLineIds, coreMemberById, slots);
        var orderByAnimeId = new Dictionary<int, int>();
        foreach (var (anime, order) in mainLineOrdered)
            orderByAnimeId[anime.Id] = order;

        var versionSlotKeyByAnimeId = new Dictionary<int, int>();
        var branchHeadAnimeIdByAnimeId = new Dictionary<int, int>();
        foreach (var slot in slots)
        {
            foreach (var alternativeId in slot.AlternativeIds)
                versionSlotKeyByAnimeId[alternativeId] = slot.SlotKey;

            foreach (var (branchHeadAnimeId, branchMemberIds) in slot.BranchMemberIdsByAlternativeId)
                foreach (var memberId in branchMemberIds)
                    branchHeadAnimeIdByAnimeId[memberId] = branchHeadAnimeId;
        }

        // The root is simply the watch order's first entry: story components
        // are disjoint (design.md D1), so — unlike the old multi-telling
        // build this replaces — a build never derives a sibling telling for
        // an earliest shared member to collide roots with.
        var rootAnimeId = mainLineOrdered[0].Anime.Id;

        var versionNeighbourIds = versionNeighbourKindByAnimeId.Keys.ToHashSet();
        var tellingIds = coreMemberIds.Concat(versionNeighbourIds).ToHashSet();
        var relationGroupByAnimeId = ResolveExtraGroups(tellingIds, mainLineIds, versionNeighbourIds, edges);

        var tellingMembers = tellingIds.Select(id => memberById[id]).ToList();
        var extrasByGroup = tellingMembers
            .Where(m => !mainLineIds.Contains(m.Id))
            .OrderBy(m => SeriesRelationGroupOrder.GroupOf(relationGroupByAnimeId[m.Id]))
            .ThenBy(OrderKey)
            .GroupBy(m => relationGroupByAnimeId[m.Id]);
        foreach (var group in extrasByGroup)
        {
            var i = 0;
            foreach (var member in group)
                orderByAnimeId[member.Id] = i++;
        }

        return new TellingBuild
        {
            CoreMemberIds = coreMemberIds,
            VersionNeighbourKindByAnimeId = versionNeighbourKindByAnimeId,
            MainLineIds = mainLineIds,
            RootAnimeId = rootAnimeId,
            OrderByAnimeId = orderByAnimeId,
            RelationGroupByAnimeId = relationGroupByAnimeId,
            VersionSlotKeyByAnimeId = versionSlotKeyByAnimeId,
            BranchHeadAnimeIdByAnimeId = branchHeadAnimeIdByAnimeId,
        };
    }

    /// <summary>Resolves each of a telling's extras to the <see cref="RelationGroup"/>
    /// it displays under, as four tiers in order
    /// (fix-alternative-version-grouping design.md D2, spec's "Main line and
    /// extras" extras-grouping rule):
    ///
    /// 1. The highest-precedence relation, in either direction, to any
    /// main-line member of this telling (<see cref="SeriesRelations.HighestPrecedenceGroup"/>)
    /// — unchanged from before this tiering.
    /// 2. Failing that, a version relation (<see cref="SeriesRelations.VersionRelations"/>),
    /// in either direction, to *any* other member of this telling — main
    /// line or extra alike — by <see cref="SeriesRelations.HighestPrecedenceVersionGroup"/>.
    /// Confined to version relations rather than any relation to any member:
    /// tier 3 already gives a better answer for the rest of them — an extra
    /// whose only relation is a plain story relation to another extra should
    /// read as whatever that extra reads as, not flatly as that relation's
    /// own group. A version relation is the one kind where the relation
    /// itself names the correct group regardless of what it points at, so it
    /// gets its own tier ahead of inheritance.
    /// 3. Failing both, inherited breadth-first from the nearest
    /// tier-1-resolved extra — a multi-source BFS seeded with only the
    /// extras tier 1 resolved, ties broken by the lower MAL id (design.md
    /// Risks/Trade-offs), travelling story/companion edges only.
    /// 4. Failing all three, Other.
    ///
    /// Scoped to edges whose both ends belong to this telling, so a shared
    /// member's edge into a neighbouring telling never leaks a foreign
    /// group in here.
    ///
    /// Neither a version edge nor a tier-2-resolved extra takes part in the
    /// tier-3 walk — as adjacency or as a seed. Excluding version edges from
    /// adjacency is what keeps a story extra from inheriting "Alternative
    /// version" merely for sitting next to one; excluding tier-2-resolved
    /// extras as seeds is what keeps a version relation from being passed on
    /// by a second hand, the same reason <paramref name="versionNeighbourIds"/>
    /// (rebuild-series-by-story-component design.md D2, task 5.5) are
    /// excluded too — one already resolves its own group directly, in tier 1
    /// or tier 2, from its own version relation to some member, so it never
    /// needs to inherit and must never pass that group on to whatever else
    /// it happens to sit next to.</summary>
    // Internal rather than private: tested directly (task 5.6) against
    // synthetic version-neighbour ids, and driven by the real ones
    // ClassifyTelling now threads through from BuildAsync's traversal
    // (task 6.1).
    internal static Dictionary<int, RelationGroup> ResolveExtraGroups(
        HashSet<int> tellingIds, HashSet<int> mainLineIds, HashSet<int> versionNeighbourIds, List<RelationEdge> edges)
    {
        var extraIds = tellingIds.Except(mainLineIds).ToHashSet();
        if (extraIds.Count == 0)
            return [];

        var scopedEdges = edges.Where(e => tellingIds.Contains(e.OwnerId) && tellingIds.Contains(e.RelatedAnimeId)).ToList();

        // Tier 1: a relation, in either direction, to any main-line member.
        var groupByExtraId = new Dictionary<int, RelationGroup>();
        foreach (var extraId in extraIds)
        {
            var edgesToMainLine = scopedEdges
                .Where(e => (e.OwnerId == extraId && mainLineIds.Contains(e.RelatedAnimeId)) ||
                    (e.RelatedAnimeId == extraId && mainLineIds.Contains(e.OwnerId)))
                .Select(e => (e.RelationType, ExtraIsOwner: e.OwnerId == extraId))
                .ToList();

            if (edgesToMainLine.Count > 0)
                groupByExtraId[extraId] = SeriesRelations.HighestPrecedenceGroup(edgesToMainLine);
        }

        // Captured before tier 2 adds any entries: the tier-3 walk below
        // seeds from exactly what tier 1 resolved, minus version neighbours
        // — never from what tier 2 resolves too.
        var tier1ResolvedIds = groupByExtraId.Keys.ToHashSet();

        // Tier 2 (design.md D2, spec rule 2): an extra tier 1 left
        // unresolved, but that carries a version relation to any other
        // member of the telling. This is the pass a version neighbour
        // exists for — one whose version relation names a non-main-line
        // member matches nothing in tier 1 and, correctly, never seeds or is
        // reached by the tier-3 walk, so without this tier it would fall
        // straight through to Other.
        foreach (var extraId in extraIds)
        {
            if (groupByExtraId.ContainsKey(extraId))
                continue;

            var edgesToAnyMember = scopedEdges
                .Where(e => e.OwnerId == extraId || e.RelatedAnimeId == extraId)
                .Select(e => (e.RelationType, ExtraIsOwner: e.OwnerId == extraId))
                .ToList();

            if (SeriesRelations.HighestPrecedenceVersionGroup(edgesToAnyMember) is { } versionGroup)
                groupByExtraId[extraId] = versionGroup;
        }

        // Tier 3: the walk travels story/companion edges only — a version
        // edge is excluded from its adjacency (design.md D3) so it can never
        // carry a group between two extras the way a story edge does.
        var nonVersionScopedEdges = scopedEdges.Where(e => !SeriesRelations.VersionRelations.Contains(e.RelationType)).ToList();

        var queue = new Queue<int>(tier1ResolvedIds.Where(id => !versionNeighbourIds.Contains(id)).OrderBy(id => id));
        while (queue.Count > 0)
        {
            var currentId = queue.Dequeue();
            var currentGroup = groupByExtraId[currentId];

            var neighbourIds = nonVersionScopedEdges
                .Where(e => e.OwnerId == currentId || e.RelatedAnimeId == currentId)
                .Select(e => e.OwnerId == currentId ? e.RelatedAnimeId : e.OwnerId)
                .Where(id => extraIds.Contains(id) && !groupByExtraId.ContainsKey(id) && !versionNeighbourIds.Contains(id))
                .Distinct()
                .OrderBy(id => id);

            foreach (var neighbourId in neighbourIds)
            {
                groupByExtraId[neighbourId] = currentGroup;
                queue.Enqueue(neighbourId);
            }
        }

        // Tier 4: failing all three, Other.
        foreach (var extraId in extraIds)
            groupByExtraId.TryAdd(extraId, RelationGroup.Other);

        return groupByExtraId;
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
    private static HashSet<int> ClassifyMainLineChain(
        List<AnimeMetadata> members, Dictionary<int, AnimeMetadata> memberById)
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

        // pv (promotional video) joins special/music here
        // (polish-rewatch-more-and-filters design.md D5b): a promo is never a
        // chapter of the story, however MAL relates it — even in a franchise
        // whose real entries carry no sequel/prequel edges at all, where a
        // chain of promos could otherwise out-rank the show. Extracted to
        // SeriesMainLineEligibility so a version neighbour's own eligibility
        // (SeriesVersionNeighbours) can be decided with this same rule.
        // No forced-ineligible set is layered on top any more
        // (rebuild-series-by-story-component design.md D1, task 5.1): a
        // version neighbour is outside this component entirely — it's an
        // extra by construction, never a candidate for this chain in the
        // first place — so there's nothing left for a boundary-member carve
        // out to exclude.
        var ineligibleIds = SeriesMainLineEligibility.FindIneligibleIds(members);
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

    // Internal rather than private: SeriesVersionSlots (task 5.2) reuses this
    // exact generic BFS to find version slots — connected components of size
    // >= 2 over version edges among main-line members — rather than
    // duplicating it.
    internal static List<List<int>> FindConnectedComponents(IEnumerable<int> nodeIds, Dictionary<int, HashSet<int>> adjacency)
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
    // Internal rather than private: SeriesService's read-time projection
    // (split-series-by-version task 7.3) reuses this exact key to interleave
    // related entries with stored extras within a relation group.
    internal static (int HasNoAiredDate, int AiredDayNumber, int AnimeId) OrderKey(AnimeMetadata anime) =>
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
    /// <paramref name="slots"/> (rebuild-series-by-story-component design.md
    /// D4, task 5.3) — every alternative of a version slot is contracted to
    /// one node before the sort runs, keyed on the slot's <c>SlotKey</c>, so
    /// every alternative shares one position in the result and a branch
    /// entry's own edges (redirected onto the contracted node) place it
    /// around the slot rather than after whichever single alternative it
    /// happened to edge to. An edge between two alternatives of the same slot
    /// is dropped rather than becoming a self-loop on the contracted node.
    /// Branch members are never contracted — they keep their own edges and
    /// their own position, exactly like a trunk member — only the alternatives
    /// themselves collapse into the slot's one position; a slot's tie-break
    /// key is its lowest-id alternative's own <see cref="OrderKey"/>, per the
    /// definition of <c>SlotKey</c>.
    ///
    /// MAL relation data can contain a `sequel`/`prequel` cycle; this must
    /// not throw on one, including a cycle contraction itself introduces (two
    /// slots each reachable from the other only through one another once
    /// their alternatives collapse). When the queue empties with nodes
    /// remaining (a cycle), the remainder is emitted in <see cref="OrderKey"/>
    /// order — unchanged from before contraction.</summary>
    private static List<(AnimeMetadata Anime, int Order)> TopologicalMainLineOrder(
        List<AnimeMetadata> mainLineMembers, HashSet<int> mainLineIds, Dictionary<int, AnimeMetadata> memberById,
        List<SeriesVersionSlots.VersionSlot> slots)
    {
        var slotKeyByAlternativeId = new Dictionary<int, int>();
        foreach (var slot in slots)
            foreach (var alternativeId in slot.AlternativeIds)
                slotKeyByAlternativeId[alternativeId] = slot.SlotKey;

        int Representative(int animeId) => slotKeyByAlternativeId.GetValueOrDefault(animeId, animeId);

        var representativeIds = mainLineIds.Select(Representative).ToHashSet();
        var successors = representativeIds.ToDictionary(id => id, _ => new HashSet<int>());
        var inDegree = representativeIds.ToDictionary(id => id, _ => 0);

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

                var predecessorRepresentative = Representative(predecessorId);
                var successorRepresentative = Representative(successorId);
                if (predecessorRepresentative == successorRepresentative)
                    continue; // both ends are alternatives of the same slot — no edge between the contracted node and itself

                if (successors[predecessorRepresentative].Add(successorRepresentative))
                    inDegree[successorRepresentative]++;
            }
        }

        (int, int, int) RepresentativeOrderKey(int representativeId) => OrderKey(memberById[representativeId]);

        var remainingInDegree = new Dictionary<int, int>(inDegree);
        var ready = new PriorityQueue<int, (int, int, int)>();
        foreach (var id in representativeIds)
        {
            if (remainingInDegree[id] == 0)
                ready.Enqueue(id, RepresentativeOrderKey(id));
        }

        var order = new List<int>();
        while (ready.Count > 0)
        {
            var id = ready.Dequeue();
            order.Add(id);

            foreach (var successorId in successors[id])
            {
                if (--remainingInDegree[successorId] == 0)
                    ready.Enqueue(successorId, RepresentativeOrderKey(successorId));
            }
        }

        if (order.Count < representativeIds.Count)
        {
            var emitted = order.ToHashSet();
            var remainder = representativeIds.Where(id => !emitted.Contains(id)).OrderBy(RepresentativeOrderKey);
            order.AddRange(remainder);
        }

        // Expand each position back to its member(s): a slot's alternatives
        // all share this position's Order; anything else was already its own
        // representative, so it expands to exactly itself.
        var alternativeIdsBySlotKey = slots.ToDictionary(s => s.SlotKey, s => s.AlternativeIds);
        var result = new List<(AnimeMetadata Anime, int Order)>();
        for (var position = 0; position < order.Count; position++)
        {
            var representativeId = order[position];
            var idsAtPosition = alternativeIdsBySlotKey.TryGetValue(representativeId, out var alternativeIds)
                ? alternativeIds
                : [representativeId];

            foreach (var id in idsAtPosition.OrderBy(id => OrderKey(memberById[id])))
                result.Add((memberById[id], position));
        }

        return result;
    }

    // --- Persistence (2.8; rewritten for multiple tellings per build by
    // split-series-by-version tasks 6.1-6.5; collapsed back to a single
    // series per build by rebuild-series-by-story-component design.md D3,
    // D8, D9, tasks 6.1-6.3) ---

    /// <summary>Persists the one series a build derives. Series identity
    /// across the rebuild is resolved by <see cref="MatchToStoredSeries"/>
    /// (design.md D9, task 6.2): the stored series overlapping this build's
    /// core members the most keeps its members and its chosen title and
    /// picture; every other stored series that overlaps on core members is
    /// stale and is deleted, surrendering any chosen title/picture a
    /// still-unset survivor lacks. A stored series that only shares a
    /// version neighbour with this build is never matched or deleted — it
    /// simply isn't a candidate in <see cref="MatchToStoredSeries"/>'s eyes.
    /// The identifier the survivor ends up under is whatever its root
    /// implies (design.md D1, D3) — the same value it already had unless
    /// this rebuild moved the root, in which case
    /// <see cref="PersistReRootedAsync"/> takes over and the whole
    /// persist happens across two transacted saves instead of the single
    /// implicit one below.</summary>
    private async Task<SeriesEntity> PersistAsync(TellingBuild telling, bool isPartial, bool isTruncated, CancellationToken ct)
    {
        var fullIds = telling.FullMemberIds.ToList();

        // Scoped to this build's full member set (core plus version
        // neighbours), with no SeriesId filter, so a version neighbour's
        // memberships in *other* series come back too — exactly what
        // ResolveFoldedPrimaryAsync (design.md D8) needs to see whether one
        // of them already holds a Core membership or a competing primary
        // fold, without a further query per member.
        var existingMemberships = await db.SeriesMembers.AsNoTracking()
            .Where(sm => fullIds.Contains(sm.AnimeId))
            .ToListAsync(ct);
        var priorRowsByAnimeId = existingMemberships
            .GroupBy(sm => sm.AnimeId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Every pre-migration row defaulted to Core (design.md Migration Plan
        // step 1), so a stored series from before this capability is seen
        // here exactly as undifferentiated as it actually was.
        var coreIdsByStoredSeriesId = existingMemberships
            .Where(sm => sm.MembershipKind == nameof(MembershipKind.Core))
            .GroupBy(sm => sm.SeriesId)
            .ToDictionary(g => g.Key, g => g.Select(sm => sm.AnimeId).ToHashSet());

        var (matchedSeriesId, staleSeriesIds) = MatchToStoredSeries(telling.CoreMemberIds, coreIdsByStoredSeriesId);
        var staleSeriesIdSet = staleSeriesIds.ToHashSet();
        var staleSeries = staleSeriesIds.Count > 0
            ? await db.Series.Where(s => staleSeriesIds.Contains(s.Id)).ToListAsync(ct)
            : [];

        SeriesEntity target;
        if (matchedSeriesId is { } matchedId)
        {
            target = await db.Series.FirstAsync(s => s.Id == matchedId, ct);
        }
        else
        {
            target = new SeriesEntity { Id = telling.RootAnimeId };
            db.Series.Add(target);
        }

        // Fill in a missing chosen title/picture from whichever stale
        // (to-be-deleted, absorbed) stored series has the largest overlap —
        // the survivor's own choice always wins (design.md D9, mirroring the
        // single-target absorption this generalises). Done before the
        // re-root check below, since a re-root carries these two fields
        // across onto the fresh row it inserts (design.md D4, task 3.3).
        if (staleSeries.Count > 0 && (target.SelectedTitle is null || target.SelectedPictureUrl is null))
        {
            var byOverlapDesc = staleSeries
                .OrderByDescending(s => telling.CoreMemberIds.Count(coreIdsByStoredSeriesId[s.Id].Contains))
                .ToList();
            target.SelectedTitle ??= byOverlapDesc.FirstOrDefault(s => s.SelectedTitle is not null)?.SelectedTitle;
            target.SelectedPictureUrl ??= byOverlapDesc.FirstOrDefault(s => s.SelectedPictureUrl is not null)?.SelectedPictureUrl;
        }

        // A matched series whose root moved can't be renumbered in place —
        // SeriesId is the primary key now, and EF refuses to modify a key
        // column on a tracked entity (design.md D3 case 3; see
        // PersistReRootedAsync for why and how).
        if (matchedSeriesId is { } matchedSeriesIdValue && target.Id != telling.RootAnimeId)
        {
            return await PersistReRootedAsync(
                telling, target, staleSeries, matchedSeriesIdValue, staleSeriesIdSet, priorRowsByAnimeId, fullIds,
                isPartial, isTruncated, ct);
        }

        target.BuiltAt = DateTimeOffset.UtcNow;
        target.IsPartial = isPartial;
        target.IsTruncated = isTruncated;

        if (staleSeries.Count > 0)
            db.Series.RemoveRange(staleSeries); // cascade-deletes their SeriesMember rows too

        List<SeriesMember> existingForTelling;
        if (matchedSeriesId is { } keptSeriesId)
        {
            existingForTelling = await db.SeriesMembers
                .Where(sm => sm.SeriesId == keptSeriesId && fullIds.Contains(sm.AnimeId))
                .ToListAsync(ct);

            // A member no longer part of the kept series (the rebuild
            // dropped it) is removed.
            var obsolete = await db.SeriesMembers
                .Where(sm => sm.SeriesId == keptSeriesId && !fullIds.Contains(sm.AnimeId))
                .ToListAsync(ct);
            db.SeriesMembers.RemoveRange(obsolete);
        }
        else
        {
            existingForTelling = [];
        }

        var existingByAnimeId = existingForTelling.ToDictionary(sm => sm.AnimeId);

        foreach (var animeId in fullIds)
        {
            // A row already at (target.Id, animeId) is updated in place. A
            // row that exists only under a *different* series can't be
            // reassigned by an in-place SeriesId update now that SeriesId is
            // part of the composite key — EF refuses to modify a key column
            // on a tracked entity — so that case is a fresh insert instead.
            if (existingByAnimeId.TryGetValue(animeId, out var existing))
            {
                var updated = await BuildMemberAsync(animeId, telling, matchedSeriesId, staleSeriesIdSet, priorRowsByAnimeId, ct);
                existing.IsMainLine = updated.IsMainLine;
                existing.Order = updated.Order;
                existing.RelationGroup = updated.RelationGroup;
                existing.IsPrimary = updated.IsPrimary;
                existing.MembershipKind = updated.MembershipKind;
                existing.VersionSlotKey = updated.VersionSlotKey;
                existing.BranchHeadAnimeId = updated.BranchHeadAnimeId;
            }
            else
            {
                target.Members.Add(await BuildMemberAsync(animeId, telling, matchedSeriesId, staleSeriesIdSet, priorRowsByAnimeId, ct));
            }
        }

        await db.SaveChangesAsync(ct);
        return target;
    }

    /// <summary>The re-root path (design.md D3 case 3, D4): a matched
    /// series' identifier is derived from its root, and a root can move
    /// between rebuilds — including as a side effect of absorbing another
    /// series, since case 3 is the merge case as much as the
    /// "MAL revealed an older prequel" case. EF refuses to modify a key on a
    /// tracked entity (the comment above the member-persistence loop already
    /// says so for <see cref="SeriesMember"/>; <c>Series.Id</c> is now in the
    /// same position), so the matched row can't simply be renumbered: it —
    /// and anything stale it absorbs — is deleted and that deletion is saved
    /// first, and only then is a fresh row inserted at the new root id.
    /// That ordering isn't cosmetic: the id the new row takes is frequently
    /// held by the very row(s) just deleted (design.md D5 — the new root is
    /// always in <c>fullIds</c>, so any stored series holding it as a Core
    /// member is either <paramref name="matchedSeries"/> or one of
    /// <paramref name="staleSeries"/>; a series holding it only as a version
    /// neighbour was never a candidate to begin with, since a root is always
    /// main-line and so Core in its own series). Both saves are wrapped in
    /// an explicit transaction — the only place this class opens one — so a
    /// crash between them can't lose <paramref name="matchedSeries"/>'s
    /// chosen title and picture, the one thing on the row that can't be
    /// re-derived, even though the franchise briefly has no stored series
    /// between the two saves (self-healing: the next read rebuilds it).</summary>
    private async Task<SeriesEntity> PersistReRootedAsync(
        TellingBuild telling, SeriesEntity matchedSeries, List<SeriesEntity> staleSeries, int matchedSeriesId,
        HashSet<int> staleSeriesIds, Dictionary<int, List<SeriesMember>> priorRowsByAnimeId, List<int> fullIds,
        bool isPartial, bool isTruncated, CancellationToken ct)
    {
        var selectedTitle = matchedSeries.SelectedTitle;
        var selectedPictureUrl = matchedSeries.SelectedPictureUrl;

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        db.Series.Remove(matchedSeries); // cascade-deletes its SeriesMember rows too
        if (staleSeries.Count > 0)
            db.Series.RemoveRange(staleSeries); // cascade-deletes their SeriesMember rows too
        await db.SaveChangesAsync(ct);

        // The id taken here is free by construction (design.md D5, above):
        // whichever stored series held it as a Core member was just deleted.
        var target = new SeriesEntity
        {
            Id = telling.RootAnimeId,
            SelectedTitle = selectedTitle,
            SelectedPictureUrl = selectedPictureUrl,
            BuiltAt = DateTimeOffset.UtcNow,
            IsPartial = isPartial,
            IsTruncated = isTruncated,
        };
        db.Series.Add(target);

        foreach (var animeId in fullIds)
        {
            // The old matched series id, unchanged from the non-re-root
            // path: priorRowsByAnimeId is a pre-read AsNoTracking snapshot
            // taken before either save above, and this is what still
            // excludes its rows from ResolveFoldedPrimaryAsync's
            // rowsElsewhere (design.md D6, task 3.7).
            target.Members.Add(await BuildMemberAsync(animeId, telling, matchedSeriesId, staleSeriesIds, priorRowsByAnimeId, ct));
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return target;
    }

    /// <summary>Builds one <see cref="SeriesMember"/> row for
    /// <paramref name="animeId"/> from <paramref name="telling"/>, shared by
    /// the in-place and re-root persist paths so the two can't drift apart
    /// on what a membership row looks like. Core is always primary; a
    /// NeighbourTelling never is — that anime's own series is its home; a
    /// FoldedVersion is primary only when nothing else already claims Core,
    /// resolved between two folds by the larger component then the lower
    /// root (design.md D8, task 6.3).</summary>
    private async Task<SeriesMember> BuildMemberAsync(
        int animeId, TellingBuild telling, int? currentSeriesId, HashSet<int> staleSeriesIds,
        Dictionary<int, List<SeriesMember>> priorRowsByAnimeId, CancellationToken ct)
    {
        var kind = telling.KindOf(animeId);
        var isPrimary = kind switch
        {
            MembershipKind.Core => true,
            MembershipKind.NeighbourTelling => false,
            _ => await ResolveFoldedPrimaryAsync(animeId, telling, currentSeriesId, staleSeriesIds, priorRowsByAnimeId, ct),
        };

        return new SeriesMember
        {
            AnimeId = animeId,
            IsMainLine = telling.MainLineIds.Contains(animeId),
            Order = telling.OrderByAnimeId[animeId],
            RelationGroup = telling.RelationGroupByAnimeId.TryGetValue(animeId, out var group) ? group.ToString() : null,
            IsPrimary = isPrimary,
            MembershipKind = kind.ToString(),
            VersionSlotKey = telling.VersionSlotKeyByAnimeId.TryGetValue(animeId, out var slotKey) ? slotKey : (int?)null,
            BranchHeadAnimeId = telling.BranchHeadAnimeIdByAnimeId.TryGetValue(animeId, out var branchHead) ? branchHead : (int?)null,
        };
    }

    /// <summary>The single-component match (design.md D9, task 6.2): the
    /// stored series overlapping <paramref name="coreMemberIds"/> the most,
    /// counted over each candidate's own Core members only, keeps its
    /// identity; every other stored series with any such overlap is stale. A
    /// stored series with no Core overlap at all — including one that shares
    /// only a version neighbour with this build — is invisible here, which is
    /// what keeps a build from ever matching or deleting on a
    /// version-neighbour overlap.</summary>
    private static (int? MatchedSeriesId, List<int> StaleSeriesIds) MatchToStoredSeries(
        HashSet<int> coreMemberIds, Dictionary<int, HashSet<int>> coreIdsByStoredSeriesId)
    {
        var overlaps = coreIdsByStoredSeriesId
            .Select(kvp => (SeriesId: kvp.Key, Overlap: coreMemberIds.Count(kvp.Value.Contains)))
            .Where(x => x.Overlap > 0)
            .ToList();

        if (overlaps.Count == 0)
            return (null, []);

        var matchedSeriesId = overlaps
            .OrderByDescending(x => x.Overlap)
            .ThenBy(x => x.SeriesId)
            .First()
            .SeriesId;

        var staleSeriesIds = overlaps.Select(x => x.SeriesId).Where(id => id != matchedSeriesId).ToList();
        return (matchedSeriesId, staleSeriesIds);
    }

    /// <summary>Whether a <see cref="MembershipKind.FoldedVersion"/> membership
    /// of <paramref name="animeId"/> in the series being built now should be
    /// primary (design.md D8, task 6.3). Never, if any of its other existing
    /// memberships — elsewhere meaning neither this series nor one about to
    /// be absorbed into it — is Core: that anime belongs to a real story
    /// component elsewhere, which always outranks a fold. Otherwise the
    /// larger of the series competing for it wins — this build's own core
    /// member count against whichever other stored series currently holds
    /// the primary fold, falling back to the lower root MAL id on a tie —
    /// and, if this build wins it, that other stored row is demoted in place
    /// so the invariant (exactly one primary membership) holds without
    /// waiting on that other series' own next rebuild.</summary>
    private async Task<bool> ResolveFoldedPrimaryAsync(
        int animeId, TellingBuild telling, int? currentSeriesId, HashSet<int> staleSeriesIds,
        Dictionary<int, List<SeriesMember>> priorRowsByAnimeId, CancellationToken ct)
    {
        if (!priorRowsByAnimeId.TryGetValue(animeId, out var priorRows))
            return true; // no existing membership anywhere: nothing to lose to

        var rowsElsewhere = priorRows.Where(r => r.SeriesId != currentSeriesId && !staleSeriesIds.Contains(r.SeriesId)).ToList();

        if (rowsElsewhere.Any(r => r.MembershipKind == nameof(MembershipKind.Core)))
            return false;

        var competingFold = rowsElsewhere.FirstOrDefault(r => r.MembershipKind == nameof(MembershipKind.FoldedVersion) && r.IsPrimary);
        if (competingFold is null)
            return true; // no other fold currently claims primary

        var competingCoreCount = await db.SeriesMembers.CountAsync(
            sm => sm.SeriesId == competingFold.SeriesId && sm.MembershipKind == nameof(MembershipKind.Core), ct);

        bool thisWins;
        if (telling.CoreMemberIds.Count != competingCoreCount)
        {
            thisWins = telling.CoreMemberIds.Count > competingCoreCount;
        }
        else
        {
            // The tie-break still reads "the lower root MAL id wins" — the
            // competing series' id *is* its root now, so no lookup is
            // needed (design.md D6, task 3.8). This query is unaffected by
            // the re-root path's early delete: competingFold.SeriesId is
            // always a series rowsElsewhere has already excluded from both
            // the current series and the stale set, so it names a row
            // nothing in this build has just deleted.
            thisWins = telling.RootAnimeId < competingFold.SeriesId;
        }

        if (thisWins)
        {
            // Demote the other stored row in place — otherwise both this
            // membership and that one would be primary at once until that
            // other series happens to rebuild.
            var tracked = await db.SeriesMembers.FirstAsync(
                sm => sm.SeriesId == competingFold.SeriesId && sm.AnimeId == animeId, ct);
            tracked.IsPrimary = false;
        }

        return thisWins;
    }
}
