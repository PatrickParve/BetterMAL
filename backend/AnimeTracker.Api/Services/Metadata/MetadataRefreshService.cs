using System.Linq.Expressions;
using System.Net;
using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Mal.Dto;
using AnimeTracker.Api.Services.Updates;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Metadata;

public class MetadataRefreshService(
    AnimeTrackerDbContext db,
    IMalClient malClient,
    IAnimeMetadataChangeDetector changeDetector,
    ILogger<MetadataRefreshService> logger) : IMetadataRefreshService
{
    public async Task<int> RefreshStaleBatchAsync(int batchSize, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        // My-list, plus the narrow adjacent carve-out (design.md D10; spec
        // "Scheduled refresh of unaired list-adjacent anime"): an unaired
        // anime with no list entry of its own but a same-story relation to a
        // non-Dropped one. Season/Top-Anime browsing populates AnimeMetadata
        // too, but those rows are refreshed solely via the lean,
        // visit-triggered path (never this nightly job). RelatedAnime must be
        // Included before ApplyTo replaces it below — same tracked-snapshot
        // requirement as RefreshOneAsync, or EF has nothing to diff against
        // and re-inserts rows that already exist instead of deleting stale
        // ones. Ordering by LastSyncedAt ascending naturally puts
        // never-fetched rows (default, i.e. the earliest possible value)
        // first. isAdjacent's own parameter becomes the combined predicate's
        // parameter, so the two clauses share one `a` with no substitution
        // needed.
        var isAdjacent = AdjacentAnimeSet.IsAdjacent(db);
        var candidateFilter = Expression.Lambda<Func<AnimeMetadata, bool>>(
            Expression.OrElse(
                Expression.NotEqual(
                    Expression.Property(isAdjacent.Parameters[0], nameof(AnimeMetadata.UserEntry)),
                    Expression.Constant(null, typeof(UserAnimeEntry))),
                isAdjacent.Body),
            isAdjacent.Parameters[0]);

        var due = await db.AnimeMetadata
            .Include(a => a.RelatedAnime)
            .Where(candidateFilter)
            .Where(RefreshTiers.IsDue(now))
            .OrderBy(a => a.LastSyncedAt)
            .Take(batchSize)
            .ToListAsync(ct);

        var refreshed = 0;
        foreach (var anime in due)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                // Full-detail fetch, not score-only: MAL rate-limits per
                // request, so a single field and the full record cost the
                // same one call. This is what lets relations, airing status,
                // episode counts and ranks refresh on these tiers too,
                // instead of freezing at whatever the anime's first fetch saw.
                // The with-pictures field set stays my-list-only (task 5.4):
                // the candidate set now also includes adjacent anime (design.md
                // D10), which have no list entry and so nothing that reads
                // PictureUrls.
                var fields = anime.UserEntry != null ? new[] { MalClient.FullDetailWithPicturesAnimeFields } : null;
                var details = await malClient.GetAnimeDetailsAsync(anime.Id, fields: fields, ct: ct);
                var before = changeDetector.Snapshot(anime);
                details.ApplyTo(anime, now);
                await changeDetector.RecordAsync(anime, before, now, ct);
                refreshed++;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to refresh anime {AnimeId}; will retry next pass.", anime.Id);
            }
        }

        if (refreshed > 0)
            await db.SaveChangesAsync(ct);

        return refreshed;
    }

    public async Task RefreshOneAsync(int animeId, CancellationToken ct = default)
    {
        // Fetch from MAL first: a genuinely invalid id throws here, before we
        // touch the DB, so we never insert a garbage row. Upsert so this also
        // caches an anime that has no row yet (a sequel link or an un-interacted
        // search result the detail page is opening for the first time).
        // The row may not exist yet, so eligibility is checked against the
        // list-entry table directly rather than the (possibly absent) cached row.
        var isMyListAnime = await db.UserAnimeEntries.AnyAsync(e => e.AnimeId == animeId, ct);
        var fields = isMyListAnime ? new[] { MalClient.FullDetailWithPicturesAnimeFields } : null;

        MalAnimeNode details;
        try
        {
            details = await malClient.GetAnimeDetailsAsync(animeId, fields: fields, ct: ct);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            throw new AnimeMetadataNotFoundException(animeId);
        }
        var now = DateTimeOffset.UtcNow;

        // Related-anime must be loaded before ApplyTo replaces the collection —
        // without a tracked snapshot, EF has nothing to diff against and would
        // try to re-insert rows that already exist instead of deleting stale ones.
        var anime = await db.AnimeMetadata.Include(a => a.RelatedAnime).FirstOrDefaultAsync(a => a.Id == animeId, ct);
        if (anime is null)
        {
            // No cached row at all: the whole relation set is new to us, not
            // news — an initial import (or a sequel link opened for the first
            // time) never emits discovery events.
            db.AnimeMetadata.Add(details.ToAnimeMetadata(now));
        }
        else
        {
            var before = changeDetector.Snapshot(anime);
            details.ApplyTo(anime, now);
            await changeDetector.RecordAsync(anime, before, now, ct);
        }

        await db.SaveChangesAsync(ct);
    }
}
