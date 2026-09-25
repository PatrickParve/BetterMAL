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

            // fix-announcements-lost-to-series-build D1, D6: whether the anime
            // was never fully fetched is read from what each discovery
            // recorded when its edge appeared, and is not derivable here at
            // all. The series rebuild that the same detection queues fetches
            // a far end the system has no row for within seconds — ten
            // minutes before this pass — and stamps LastSyncedAt, so reading
            // that field now reports the rebuild, not the past. A discovery
            // recorded before the column existed (null) has no verdict to
            // read and falls back to today's check of the current record.
            // Any row saying "new" is enough (D6): a later edge to the same
            // anime that was not news does not undo one that was, and
            // AnimeUpdateRecorder's once-per-anime dedupe prevents a
            // duplicate card.
            var needsFetch = anime is null || anime.LastSyncedAt == default;
            var neverFullyFetched = group.Any(d => d.RelatedAnimeHadFullDetail == false
                || (d.RelatedAnimeHadFullDetail is null && needsFetch));

            // design.md D5, kept read-time: my list is only ever changed by
            // me, never by the system's own background work, and an anime I
            // have added since the discovery is one I already know about.
            // hasEntry ignores status: a Dropped entry still proves the
            // anime isn't a new show.
            var hasEntry = await db.UserAnimeEntries.AsNoTracking().AnyAsync(e => e.AnimeId == animeId, ct);

            if (!neverFullyFetched || hasEntry)
            {
                // Already fully fetched when the edge appeared, or already a
                // list entry in any status: not an announcement candidate.
                // Mark it resolved and spend no MAL call.
                foreach (var discovery in group)
                    discovery.ProcessedAt = now;
                continue;
            }

            // Announceable. The airing gate below needs a status to read, so
            // an absent or lean row (no airing status) is fetched first; a
            // lean row's null AiringStatus would otherwise fail that gate
            // forever. Where the row already carries full detail — usually
            // because the series rebuild cached the anime moments after the
            // discovery (fix-announcements-lost-to-series-build D7) — there
            // is nothing to fetch, so the common announcement costs no MAL
            // call, and none of the failure paths below can apply to it.
            if (needsFetch)
            {
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
            }

            // Only reachable as null after a fetch, since needsFetch is false
            // exactly when a row was already there to read.
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
