using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Mal;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Metadata;

public class MetadataRefreshService(
    AnimeTrackerDbContext db,
    IMalClient malClient,
    ILogger<MetadataRefreshService> logger) : IMetadataRefreshService
{
    private static readonly string[] ScoreOnlyFields = ["mean"];

    // Four fixed staleness tiers (design.md decision, finalized): currently
    // airing refreshes daily; finished within the last 60 days refreshes every
    // 3 days; finished 60 days-1 year ago (or not yet aired) refreshes weekly;
    // finished 1+ years ago refreshes monthly.
    private static readonly TimeSpan AiringThreshold = TimeSpan.FromDays(1);
    private static readonly TimeSpan RecentlyFinishedThreshold = TimeSpan.FromDays(3);
    private static readonly TimeSpan WeeklyThreshold = TimeSpan.FromDays(7);
    private static readonly TimeSpan MonthlyThreshold = TimeSpan.FromDays(30);
    private const int RecentlyFinishedWindowDays = 60;
    private const int OneYearWindowDays = 365;

    public async Task<int> RefreshStaleBatchAsync(int batchSize, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var recentlyFinishedWindowStart = today.AddDays(-RecentlyFinishedWindowDays);
        var oneYearWindowStart = today.AddDays(-OneYearWindowDays);
        var airingCutoff = now - AiringThreshold;
        var recentlyFinishedCutoff = now - RecentlyFinishedThreshold;
        var weeklyCutoff = now - WeeklyThreshold;
        var monthlyCutoff = now - MonthlyThreshold;

        // My-list only: Season/Top-Anime browsing populates AnimeMetadata too,
        // but those rows are refreshed solely via the lean, visit-triggered
        // path (never this nightly job). The staleness check (mirrors
        // IsStale's four tiers) is pushed into the query itself rather than
        // loading every my-list row into memory to filter client-side.
        var due = await db.AnimeMetadata
            .Where(a => a.UserEntry != null)
            .Where(a =>
                a.LastScoreSyncedAt == null ||
                (a.AiringStatus == "currently_airing" && a.LastScoreSyncedAt <= airingCutoff) ||
                (a.AiringStatus == "not_yet_aired" && a.LastScoreSyncedAt <= weeklyCutoff) ||
                (a.AiringStatus == "finished_airing" && a.AiredTo != null && a.AiredTo >= recentlyFinishedWindowStart &&
                    a.LastScoreSyncedAt <= recentlyFinishedCutoff) ||
                (a.AiringStatus == "finished_airing" && a.AiredTo != null && a.AiredTo < recentlyFinishedWindowStart &&
                    a.AiredTo >= oneYearWindowStart && a.LastScoreSyncedAt <= weeklyCutoff) ||
                (a.AiringStatus != "currently_airing" && a.AiringStatus != "not_yet_aired" &&
                    !(a.AiringStatus == "finished_airing" && a.AiredTo != null && a.AiredTo >= oneYearWindowStart) &&
                    a.LastScoreSyncedAt <= monthlyCutoff))
            .OrderBy(a => a.LastScoreSyncedAt ?? DateTimeOffset.MinValue)
            .Take(batchSize)
            .ToListAsync(ct);

        var refreshed = 0;
        foreach (var anime in due)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var details = await malClient.GetAnimeDetailsAsync(anime.Id, ScoreOnlyFields, ct);
                anime.MalScore = details.Mean;
                anime.LastScoreSyncedAt = now;
                refreshed++;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to refresh score for anime {AnimeId}; will retry next pass.", anime.Id);
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
        var details = await malClient.GetAnimeDetailsAsync(animeId, ct: ct);
        var now = DateTimeOffset.UtcNow;

        var anime = await db.AnimeMetadata.FirstOrDefaultAsync(a => a.Id == animeId, ct);
        if (anime is null)
            db.AnimeMetadata.Add(details.ToAnimeMetadata(now));
        else
            details.ApplyTo(anime, now);

        await db.SaveChangesAsync(ct);
    }
}
