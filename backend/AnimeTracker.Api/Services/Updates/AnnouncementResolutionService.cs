using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Metadata;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Updates;

public class AnnouncementResolutionService(
    AnimeTrackerDbContext db,
    IMetadataRefreshService metadataRefresh,
    IAnimeUpdateRecorder updateRecorder,
    ILogger<AnnouncementResolutionService> logger) : IAnnouncementResolutionService
{
    public async Task ResolveAsync(int maxAnime, int? skipAnimeId, MalCallTally tally, CancellationToken ct = default)
    {
        if (maxAnime <= 0)
            return;

        // Oldest first, one row per discovery: an anime discovered weeks ago
        // and still unresolved should not be starved by a fresh discovery
        // landing right behind it. Grouping by RelatedAnimeId below means two
        // discoveries naming the same anime cost one MAL call, and taking
        // maxAnime rows bounds this pass to at most maxAnime distinct anime.
        IQueryable<RelationDiscovery> pending = db.RelationDiscoveries
            .Where(d => d.ProcessedAt == null);

        // The anime whose call ended the previous pass (design D6): skipped
        // here, before Take, so the rest of this pass's allowance still goes
        // to other discoveries instead of being spent re-trying it immediately.
        if (skipAnimeId is { } skip)
            pending = pending.Where(d => d.RelatedAnimeId != skip);

        var discoveries = await pending
            .OrderBy(d => d.DiscoveredAt)
            .Take(maxAnime)
            .ToListAsync(ct);

        if (discoveries.Count == 0)
            return;

        var now = DateTimeOffset.UtcNow;

        foreach (var group in discoveries.GroupBy(d => d.RelatedAnimeId))
        {
            var animeId = group.Key;
            var anime = await db.AnimeMetadata.AsNoTracking().FirstOrDefaultAsync(a => a.Id == animeId, ct);

            // design.md D5: captured before any resolving fetch below, and in
            // this order, because that fetch stamps LastSyncedAt — asked
            // afterwards, "had we ever fully fetched this?" is unanswerable
            // and every anime looks already-known, the same trap
            // AnimeMetadataSnapshot.HadFullDetail already exists to avoid on
            // the metadata-write path. hasEntry ignores status: a Dropped
            // entry still proves the anime isn't a new show.
            var hadFullDetail = anime is not null && anime.LastSyncedAt != default;
            var hasEntry = await db.UserAnimeEntries.AsNoTracking().AnyAsync(e => e.AnimeId == animeId, ct);

            if (hadFullDetail || hasEntry)
            {
                // Already fully fetched, or already a list entry in any
                // status: not an announcement candidate. Mark it resolved
                // and spend no MAL call.
                foreach (var discovery in group)
                    discovery.ProcessedAt = now;
                continue;
            }

            // Announceable: the row is absent or lean, so it always needs a
            // fetch for the airing gate below to have a status to read.
            // Previously the fetch ran only when the row was entirely
            // absent, so a lean row's null AiringStatus silently failed that
            // gate forever.
            try
            {
                tally.RecordAttempt();
                await metadataRefresh.RefreshOneAsync(animeId, ct);
                tally.RecordSuccess();
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // An if chain, not a switch: a `break` inside a switch's arm
                // would only leave the switch, not this foreach.
                var kind = MalCallFailure.Classify(ex);
                if (kind == MalCallFailureKind.NotFound)
                {
                    foreach (var discovery in group)
                        discovery.ProcessedAt = now;
                    logger.LogWarning(ex, "MAL has no anime {AnimeId}; its discovery is resolved without an announcement.", animeId);
                }
                else if (kind == MalCallFailureKind.Outage)
                {
                    logger.LogWarning(ex, "MAL appears unavailable while resolving anime {AnimeId}; ending this pass.", animeId);
                    tally.RecordUnavailable(animeId);
                    break;
                }
                else
                {
                    logger.LogWarning(ex, "Failed to resolve newly-related anime {AnimeId}; will retry next pass.", animeId);
                }
                continue; // leave this group's discoveries unprocessed, unless the NotFound branch above just resolved them
            }

            anime = await db.AnimeMetadata.AsNoTracking().FirstOrDefaultAsync(a => a.Id == animeId, ct);
            if (anime is null)
            {
                logger.LogWarning("Anime {AnimeId} still has no cached record after a successful resolution fetch; will retry next pass.", animeId);
                continue;
            }

            // Gated at write time (design.md D6): only an anime that has not
            // finished airing is announced. MAL adds missing edges to
            // long-finished anime routinely, and those are a data correction
            // reaching us, not news. Combined with the never-fully-fetched-
            // and-no-entry check above, an announcement now means something
            // the system had never seen before appeared in the relations of
            // an anime on the user's list.
            if (anime.AiringStatus is "not_yet_aired" or "currently_airing")
                await updateRecorder.RecordAsync(anime, AnimeUpdateKinds.Announced, default, now, ct);

            foreach (var discovery in group)
                discovery.ProcessedAt = now;
        }

        await db.SaveChangesAsync(ct);
    }
}
