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
    public async Task RefreshStaleBatchAsync(int batchSize, int? skipAnimeId, MalCallTally tally, CancellationToken ct = default)
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
        // ones. Ordering by RefreshTiers.LastAttemptAt ascending naturally
        // puts never-fetched rows with no failed attempt (default, i.e. the
        // earliest possible value) first. isAdjacent's own parameter becomes
        // the combined predicate's parameter, so the two clauses share one
        // `a` with no substitution needed.
        var isAdjacent = AdjacentAnimeSet.IsAdjacent(db);
        var candidateFilter = Expression.Lambda<Func<AnimeMetadata, bool>>(
            Expression.OrElse(
                Expression.NotEqual(
                    Expression.Property(isAdjacent.Parameters[0], nameof(AnimeMetadata.UserEntry)),
                    Expression.Constant(null, typeof(UserAnimeEntry))),
                isAdjacent.Body),
            isAdjacent.Parameters[0]);

        IQueryable<AnimeMetadata> candidates = db.AnimeMetadata
            .Include(a => a.RelatedAnime)
            .Where(candidateFilter)
            .Where(RefreshTiers.IsDue(now));

        // The anime whose call ended the previous pass (design D6): skipped
        // here, before Take, so the rest of this pass's allowance still goes
        // to other work instead of being spent re-trying it immediately.
        if (skipAnimeId is { } skip)
            candidates = candidates.Where(a => a.Id != skip);

        var due = await candidates
            .OrderBy(RefreshTiers.LastAttemptAt)
            .Take(batchSize)
            .ToListAsync(ct);

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
                tally.RecordAttempt();
                var details = await malClient.GetAnimeDetailsAsync(anime.Id, fields: fields, ct: ct);
                var before = changeDetector.Snapshot(anime);
                details.ApplyTo(anime, now);
                await changeDetector.RecordAsync(anime, before, now, ct);
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
                    anime.LastRefreshFailedAt = now;
                    logger.LogWarning(ex, "MAL has no anime {AnimeId}; it will wait out its tier before being tried again.", anime.Id);
                }
                else if (kind == MalCallFailureKind.Outage)
                {
                    logger.LogWarning(ex, "MAL appears unavailable while refreshing anime {AnimeId}; ending this pass.", anime.Id);
                    tally.RecordUnavailable(anime.Id);
                    break;
                }
                else
                {
                    logger.LogWarning(ex, "Failed to refresh anime {AnimeId}; will retry next pass.", anime.Id);
                }
            }
        }

        // Saved whenever any candidate was loaded, not only when one
        // succeeded — a 404 mark on an otherwise-all-failing pass has to be
        // saved too, and EF makes no database round trip when nothing on any
        // tracked entity actually changed.
        if (due.Count > 0)
            await db.SaveChangesAsync(ct);
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
