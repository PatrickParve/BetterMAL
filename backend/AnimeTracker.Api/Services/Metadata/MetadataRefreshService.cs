using System.Net;
using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Mal.Dto;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Metadata;

public class MetadataRefreshService(
    AnimeTrackerDbContext db,
    IMalClient malClient,
    ILogger<MetadataRefreshService> logger) : IMetadataRefreshService
{
    public async Task<int> RefreshStaleBatchAsync(int batchSize, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        // My-list only: Season/Top-Anime browsing populates AnimeMetadata too,
        // but those rows are refreshed solely via the lean, visit-triggered
        // path (never this nightly job). RelatedAnime must be Included before
        // ApplyTo replaces it below — same tracked-snapshot requirement as
        // RefreshOneAsync, or EF has nothing to diff against and re-inserts
        // rows that already exist instead of deleting stale ones. Ordering by
        // LastSyncedAt ascending naturally puts never-fetched rows (default,
        // i.e. the earliest possible value) first.
        var due = await db.AnimeMetadata
            .Include(a => a.RelatedAnime)
            .Where(a => a.UserEntry != null)
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
                var details = await malClient.GetAnimeDetailsAsync(anime.Id, ct: ct);
                var before = SnapshotRelations(anime);
                details.ApplyTo(anime, now);
                RecordDiscoveries(anime, before, now);
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
        MalAnimeNode details;
        try
        {
            details = await malClient.GetAnimeDetailsAsync(animeId, ct: ct);
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
            var before = SnapshotRelations(anime);
            details.ApplyTo(anime, now);
            RecordDiscoveries(anime, before, now);
        }

        await db.SaveChangesAsync(ct);
    }

    private static HashSet<(int RelatedAnimeId, string RelationType)> SnapshotRelations(AnimeMetadata anime) =>
        anime.RelatedAnime.Select(r => (r.RelatedAnimeId, r.RelationType)).ToHashSet();

    /// <summary>Writes one <see cref="RelationDiscovery"/> per edge present in
    /// <paramref name="anime"/>'s relation set after <c>ApplyTo</c> that wasn't
    /// in <paramref name="before"/> — the snapshot taken from the tracked
    /// collection just before <c>ApplyTo</c> replaced it. Removed and
    /// unchanged edges are deliberately not events (design.md decision 10).</summary>
    private void RecordDiscoveries(
        AnimeMetadata anime, HashSet<(int RelatedAnimeId, string RelationType)> before, DateTimeOffset now)
    {
        foreach (var edge in anime.RelatedAnime)
        {
            if (before.Contains((edge.RelatedAnimeId, edge.RelationType)))
                continue;

            db.RelationDiscoveries.Add(new RelationDiscovery
            {
                AnimeId = anime.Id,
                RelatedAnimeId = edge.RelatedAnimeId,
                RelationType = edge.RelationType,
                DiscoveredAt = now,
            });
        }
    }
}
