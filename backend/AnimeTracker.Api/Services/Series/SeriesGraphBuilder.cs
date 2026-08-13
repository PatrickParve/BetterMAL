using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Metadata;
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
    ILogger<SeriesGraphBuilder> logger)
{
    // A runaway-component safety ceiling, not a working limit: set high
    // enough that no real franchise reaches it, so reaching it means the
    // traversal has gone wrong and the truncation notice is meaningful
    // (design.md decision 1).
    public const int MemberCap = 400;
    public const int VisitFetchBudget = 8;
    public const int RebuildFetchBudget = 20;

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
    /// instead of all at once.</summary>
    public async Task<SeriesEntity?> BuildAsync(
        int seedAnimeId, int fetchBudget, bool expandLeanMembers, CancellationToken ct = default)
    {
        var (members, isPartial, isTruncated) = await TraverseAsync(seedAnimeId, fetchBudget, expandLeanMembers, ct);

        if (members.Count <= 1)
            return null;

        var (mainLineIds, rootAnimeId, orderByAnimeId) = Classify(members);

        return await PersistAsync(members, mainLineIds, orderByAnimeId, rootAnimeId, isPartial, isTruncated, ct);
    }

    // --- Traversal (2.2, 2.3, 2.4) ---

    private async Task<(List<AnimeMetadata> Members, bool IsPartial, bool IsTruncated)> TraverseAsync(
        int seedAnimeId, int fetchBudget, bool expandLeanMembers, CancellationToken ct)
    {
        var visited = new HashSet<int> { seedAnimeId };
        var queue = new Queue<int>();
        queue.Enqueue(seedAnimeId);

        var members = new List<AnimeMetadata>();
        var fetchesUsed = 0;
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
            var outgoingIds = metadata.RelatedAnime
                .Where(r => SeriesRelations.IsTraversable(r.RelationType))
                .Select(r => r.RelatedAnimeId);

            var incomingIds = await db.AnimeRelatedAnime.AsNoTracking()
                .Where(r => r.RelatedAnimeId == animeId && SeriesRelations.TraversalSet.Contains(r.RelationType))
                .Select(r => r.AnimeId)
                .ToListAsync(ct);

            foreach (var neighbourId in outgoingIds.Concat(incomingIds).Distinct())
            {
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

        var mainLineOrdered = members.Where(m => mainLineIds.Contains(m.Id)).OrderBy(OrderKey).ToList();
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

    /// <summary>The largest sequel/prequel chain among the members (ties broken
    /// by earliest-aired member), minus special/music entries and recaps.
    /// Falls back to the unfiltered chain when that filter empties it out, so
    /// a specials-only franchise still has a main line to render (design.md
    /// decision 2 and the degenerate case in decision 4/task 2.7).
    ///
    /// Chain candidates are restricted to ones containing at least one `tv`
    /// member when any exist. A long-running single-entry show (one TV
    /// series plus many movies/specials, e.g. One Piece) has no sequel/prequel
    /// edges on the TV entry itself — nothing MAL would call a "season" of
    /// it exists as a separate entry — so every node, including the TV entry,
    /// starts out as its own singleton chain. Left unrestricted, a handful of
    /// side-story movies/specials that happen to sequel-chain to *each other*
    /// (unrelated to the flagship show) can out-size that singleton and hijack
    /// the main line entirely. Requiring a `tv` member is a no-op for the
    /// ordinary case (a real season chain is `tv` by construction) and falls
    /// back to the unrestricted set when nothing in the component is `tv` at
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

        var recapIds = FindRecapIds(members, memberById);

        var chains = FindConnectedComponents(memberById.Keys, adjacency);
        var tvChains = chains.Where(chain => chain.Any(id => memberById[id].MediaType == "tv")).ToList();
        var candidateChains = tvChains.Count > 0 ? tvChains : chains;
        var largestChain = candidateChains
            .OrderByDescending(chain => chain.Count)
            .ThenBy(chain => chain.Min(id => OrderKey(memberById[id])))
            .First();

        var filtered = largestChain
            .Where(id => memberById[id].MediaType is not ("special" or "music") && !recapIds.Contains(id))
            .ToHashSet();

        return filtered.Count > 0 ? filtered : largestChain.ToHashSet();
    }

    /// <summary>Members MAL tags as a recap/condensed retelling of another
    /// member — the `summary`/`full_story` pair (`A --summary--> B` means B
    /// recaps A; `B --full_story--> A` says the same from B's side). MAL
    /// routinely also gives these a `sequel`/`prequel` edge to the season
    /// they bridge into (e.g. a "commemorative special" recapping season 1
    /// that itself carries `sequel: season 2`), which would otherwise pull it
    /// into <see cref="ClassifyMainLineChain"/>'s sequel/prequel subgraph and
    /// — since it's typically typed `tv_special`, not `special` — survive the
    /// media-type filter. The explicit summary/full_story tag is a stronger,
    /// more direct signal than media type that this entry is not new story
    /// content, so it's excluded regardless of what else links it in.</summary>
    private static HashSet<int> FindRecapIds(List<AnimeMetadata> members, Dictionary<int, AnimeMetadata> memberById)
    {
        var recapIds = new HashSet<int>();
        foreach (var member in members)
        {
            foreach (var relation in member.RelatedAnime)
            {
                if (!memberById.ContainsKey(relation.RelatedAnimeId))
                    continue;

                switch (relation.RelationType)
                {
                    case "full_story":
                        recapIds.Add(member.Id); // this member is the recap of relation.RelatedAnimeId
                        break;
                    case "summary":
                        recapIds.Add(relation.RelatedAnimeId); // the related member is the recap of this one
                        break;
                }
            }
        }

        return recapIds;
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
