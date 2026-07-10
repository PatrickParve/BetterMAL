using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing.AniList;
using AnimeTracker.Api.Services.Scheduling;

namespace AnimeTracker.Api.Services.Airing;

public class EpisodeScheduleRefreshService(
    IUserAnimeEntryRepository entryRepository,
    IAniListClient aniList,
    IEpisodeScheduleCache cache,
    IBroadcastLocalTimeConverter converter,
    ILogger<EpisodeScheduleRefreshService> logger) : IEpisodeScheduleRefreshService
{
    // Each show costs two AniList calls (id lookup + schedule). AniList's limit
    // is 30 req/min, so ~4.5s between shows (≈13 shows/min → 26 req/min) stays
    // under it, with the client's 429 back-off as a safety net.
    private static readonly TimeSpan PerRequestDelay = TimeSpan.FromMilliseconds(4500);

    // A show that ended recently still needs per-episode data so the last few
    // weeks it aired render break-accurately when the user pages back.
    private const int RecentlyFinishedDays = 60;

    public async Task RefreshAsync(CancellationToken ct)
    {
        var today = converter.GetLocalDate(DateTimeOffset.UtcNow);
        var entries = await entryRepository.GetAllAsync(ct);
        var targets = entries
            .Select(entry => entry.Anime)
            .Where(anime => ShouldTrack(anime, today))
            .DistinctBy(anime => anime.Id)
            .ToList();

        var withData = 0;
        foreach (var anime in targets)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var schedule = await aniList.GetAiringScheduleAsync(anime.Id, ct);
                cache.Set(anime.Id, schedule);
                if (schedule.Count > 0)
                    withData++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "AniList schedule fetch failed for anime {AnimeId} ({Title}).", anime.Id, anime.Title);
            }

            try
            {
                await Task.Delay(PerRequestDelay, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation(
            "AniList schedule refresh complete: {WithData}/{Total} tracked shows have per-episode data.",
            withData, targets.Count);
    }

    private static bool ShouldTrack(AnimeMetadata anime, DateOnly today) =>
        anime.AiringStatus is "currently_airing" or "not_yet_aired"
        || (anime.AiringStatus == "finished_airing"
            && anime.AiredTo is { } airedTo
            && airedTo >= today.AddDays(-RecentlyFinishedDays));
}
