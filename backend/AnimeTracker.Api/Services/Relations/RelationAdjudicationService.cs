using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing.AniList;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Relations;

public class RelationAdjudicationService(
    AnimeTrackerDbContext db,
    IAniListClient aniList,
    AniListRelationStore relationStore,
    ILogger<RelationAdjudicationService> logger) : IRelationAdjudicationService
{
    // Bounds how many never-fetched candidates get their edges inspected per
    // pass, so one slow tick can't turn into an unbounded scan of the whole
    // my-list. A candidate not reached this pass is simply reconsidered next
    // tick — nothing here is lost by deferring it.
    private const int ExplorationCap = 200;

    public async Task<int> RunBatchAsync(int batchSize, CancellationToken ct = default)
    {
        var candidateIds = await FindCandidatesAsync(batchSize, ct);
        if (candidateIds.Count == 0)
            return 0;

        IReadOnlyDictionary<int, AniListRelationsLookup> lookups;
        try
        {
            lookups = await aniList.GetRelationsBatchAsync(candidateIds, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AniList relations batch lookup failed for {Count} anime; will retry next pass.", candidateIds.Count);
            return 0;
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var animeId in candidateIds)
        {
            var sync = await db.AnimeAiringSyncs.FirstOrDefaultAsync(s => s.AnimeId == animeId, ct);
            if (sync is null)
            {
                sync = new AnimeAiringSync { AnimeId = animeId };
                db.AnimeAiringSyncs.Add(sync);
            }

            if (lookups.TryGetValue(animeId, out var lookup))
            {
                await relationStore.ReplaceAsync(animeId, lookup.Relations, ct);
                sync.AniListId = lookup.AniListId;
            }
            else
            {
                // Not in the response: AniList doesn't know this MAL id —
                // looked up and confirmed absent, same as the single-lookup path.
                sync.AniListId = null;
            }

            sync.RelationsFetchedAt = now;
        }

        await db.SaveChangesAsync(ct);
        return candidateIds.Count;
    }

    /// <summary>My-list anime whose AniList relations have never been fetched
    /// (by this job or by an airing lookup that happened to cover them) and
    /// that hold at least one Unconfirmed edge — the far end has been
    /// full-fetched and doesn't reciprocate. Confirmed and Unknown edges are
    /// never a reason to look this anime up.</summary>
    private async Task<List<int>> FindCandidatesAsync(int limit, CancellationToken ct)
    {
        var unfetchedIds = await db.AnimeMetadata.AsNoTracking()
            .Where(a => a.UserEntry != null)
            .Where(a => !db.AnimeAiringSyncs.Any(s => s.AnimeId == a.Id && s.RelationsFetchedAt != null))
            .Select(a => a.Id)
            .ToListAsync(ct);

        var candidates = new List<int>();
        foreach (var animeId in unfetchedIds.Take(ExplorationCap))
        {
            if (candidates.Count >= limit)
                break;
            ct.ThrowIfCancellationRequested();

            if (await HasUnconfirmedEdgeAsync(animeId, ct))
                candidates.Add(animeId);
        }

        return candidates;
    }

    private async Task<bool> HasUnconfirmedEdgeAsync(int animeId, CancellationToken ct)
    {
        var outgoing = await db.AnimeRelatedAnime.AsNoTracking()
            .Where(r => r.AnimeId == animeId)
            .Select(r => new { r.RelatedAnimeId, r.RelationType })
            .ToListAsync(ct);
        var incoming = await db.AnimeRelatedAnime.AsNoTracking()
            .Where(r => r.RelatedAnimeId == animeId)
            .Select(r => new { OwnerId = r.AnimeId, r.RelationType })
            .ToListAsync(ct);

        if (outgoing.Count == 0 && incoming.Count == 0)
            return false;

        var farEndIds = outgoing.Select(o => o.RelatedAnimeId).Concat(incoming.Select(i => i.OwnerId)).Distinct().ToList();
        var farEndFetched = await db.AnimeMetadata.AsNoTracking()
            .Where(a => farEndIds.Contains(a.Id))
            .Select(a => new { a.Id, a.LastSyncedAt })
            .ToDictionaryAsync(a => a.Id, a => a.LastSyncedAt != default, ct);

        // An outgoing edge is Unconfirmed when it has a confident inverse,
        // the far end has been full-fetched, and the far end doesn't store
        // that inverse back.
        foreach (var edge in outgoing)
        {
            var invertedType = RelationInverse.Invert(edge.RelationType);
            if (invertedType is null)
                continue;

            var confirmed = incoming.Any(i => i.OwnerId == edge.RelatedAnimeId && i.RelationType == invertedType);
            if (!confirmed && farEndFetched.GetValueOrDefault(edge.RelatedAnimeId))
                return true;
        }

        // A reverse-derived edge (no matching outgoing edge to the same far
        // end — otherwise it would have deduped into the outgoing case above)
        // is Unconfirmed when this anime itself has been full-fetched and
        // simply doesn't store the reciprocal edge — which, by construction
        // here, it doesn't.
        var selfFetched = await db.AnimeMetadata.AsNoTracking()
            .Where(a => a.Id == animeId)
            .Select(a => a.LastSyncedAt != default)
            .FirstOrDefaultAsync(ct);

        if (selfFetched)
        {
            foreach (var edge in incoming)
            {
                if (outgoing.Any(o => o.RelatedAnimeId == edge.OwnerId))
                    continue;
                if (RelationInverse.Invert(edge.RelationType) is not null)
                    return true;
            }
        }

        return false;
    }
}
