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

        // My-list only: Season/Top-Anime browsing populates AnimeMetadata too,
        // but those rows are refreshed solely via the lean, visit-triggered
        // path (never this nightly job).
        var all = await db.AnimeMetadata.Where(a => a.UserEntry != null).ToListAsync(ct);
        var due = all
            .Where(a => IsStale(a, now, today))
            .OrderBy(a => a.LastScoreSyncedAt ?? DateTimeOffset.MinValue)
            .Take(batchSize)
            .ToList();

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
        var anime = await db.AnimeMetadata.FirstOrDefaultAsync(a => a.Id == animeId, ct)
            ?? throw new AnimeMetadataNotFoundException(animeId);

        var details = await malClient.GetAnimeDetailsAsync(animeId, ct: ct);
        details.ApplyTo(anime, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    private static bool IsStale(AnimeMetadata anime, DateTimeOffset now, DateOnly today)
    {
        var threshold = anime.AiringStatus switch
        {
            "currently_airing" => AiringThreshold,
            "not_yet_aired" => WeeklyThreshold,
            "finished_airing" when anime.AiredTo is { } airedTo && airedTo >= today.AddDays(-RecentlyFinishedWindowDays) => RecentlyFinishedThreshold,
            "finished_airing" when anime.AiredTo is { } airedTo && airedTo >= today.AddDays(-OneYearWindowDays) => WeeklyThreshold,
            _ => MonthlyThreshold,
        };

        return anime.LastScoreSyncedAt is not { } last || now - last >= threshold;
    }
}
