using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Dashboard;
using AnimeTracker.Api.Services.Entries;
using AnimeTracker.Api.Services.Infrastructure;
using AnimeTracker.Api.Services.Metadata;
using AnimeTracker.Api.Services.Relations;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Detail;

// Keyed by related-anime id — the media type and English title an
// AnimeMetadata row (if any) holds for it, so RelatedAnimeDto.FromEntity can
// fall back to both from the same cache lookup.
public record RelatedMetadata(string? MediaType, string? EnglishTitle);

public class AnimeDetailService(
    IAnimeMetadataRepository metadataRepository,
    IMetadataRefreshService refreshService,
    IEpisodeScheduleService scheduleService,
    IAiringWatchStatusService airingWatchStatusService,
    IRelationResolver relationResolver,
    AnimeTrackerDbContext db,
    RefreshGate refreshGate,
    ILogger<AnimeDetailService> logger) : IAnimeDetailService
{
    public async Task<AnimeDetailDto> GetDetailAsync(int animeId, CancellationToken ct = default)
    {
        var anime = await metadataRepository.GetByIdAsync(animeId, ct);
        var refreshFailed = false;

        // Live-fetch full detail from MAL (RefreshOneAsync upserts) whenever
        // the row is missing or past its own staleness tier — the same TTL
        // check the nightly job uses (RefreshTiers), so the two paths cannot
        // disagree about what counts as fresh. After the fetch, later visits
        // within the tier are plain cache hits. Single-flight via
        // RefreshGate: a waiter re-checks this same predicate after acquiring
        // the gate, so it sees the winner's fetch and skips a second MAL
        // fetch instead of racing it.
        if (NeedsFullDetailFetch(anime))
        {
            using (await refreshGate.LockAsync($"anime:{animeId}", ct))
            {
                anime = await metadataRepository.GetByIdAsync(animeId, ct);
                if (NeedsFullDetailFetch(anime))
                {
                    try
                    {
                        await refreshService.RefreshOneAsync(animeId, ct);
                        anime = await metadataRepository.GetByIdAsync(animeId, ct) ?? anime;
                    }
                    catch (Exception ex)
                    {
                        // Cached data (if any) is still served below; LastSyncedAt
                        // is untouched by a fetch that threw, so NeedsFullDetailFetch
                        // is true again on the next visit and this retries naturally.
                        logger.LogWarning(ex, "Failed to live-fetch full detail for anime {AnimeId}; serving cached data if any.", animeId);
                        refreshFailed = true;
                    }
                }
            }
        }

        if (anime is null)
            throw new AnimeMetadataNotFoundException(animeId);

        var now = DateTimeOffset.UtcNow;
        var episodesAired = await scheduleService.EpisodesAiredAsOfAsync(anime, now, ct);

        // design.md D6: free here — the aired count above is exactly what
        // settling needs, and this page renders only the one entry. The
        // reverse nav isn't Included by GetByIdAsync, so it's set explicitly
        // rather than relying on no-tracking query fixup for it.
        if (anime.UserEntry is { } entry && episodesAired is { } aired)
        {
            entry.Anime = anime;
            await airingWatchStatusService.SettleAsync([entry], new Dictionary<int, int> { [animeId] = aired }, ct);
        }

        var nextEpisode = ToEta(await scheduleService.NextAiringInstantAsync(anime, now, ct), now);
        var aniListId = await db.AnimeAiringSyncs.AsNoTracking()
            .Where(s => s.AnimeId == animeId)
            .Select(s => s.AniListId)
            .FirstOrDefaultAsync(ct);

        // Full-detail fetches store each relation's media type directly
        // (AnimeRelatedAnime.MediaType, from related_anime{node{media_type}});
        // this cache lookup is only a fallback for relation rows written
        // before that column existed. The English title has no such direct
        // storage, so every relation depends on this lookup for it.
        var relatedMetadataByAnimeId = await GetRelatedMetadataAsync(anime.RelatedAnime.Select(r => r.RelatedAnimeId), ct);
        // Filtered to the primary membership (split-series-by-version task
        // 8.2): the "Series" link always opens this anime's primary series
        // (via the read endpoint's own IsPrimary resolution), so InSeries
        // must agree about which membership counts.
        var inSeries = await db.SeriesMembers.AsNoTracking().AnyAsync(m => m.AnimeId == animeId && m.IsPrimary, ct);
        var relations = await relationResolver.ResolveAsync(anime, ct);

        // Evaluated after the live-fetch block above, so a fetch that just
        // ran (and therefore just stamped PicturesSyncedAt) clears the flag
        // in the same read rather than asking the client to backfill data
        // that already arrived (spec anime-detail "A fresh fetch carries the
        // pictures").
        var picturesFetchPending = anime.UserEntry is not null && anime.PicturesSyncedAt is null;

        return AnimeDetailDto.FromEntity(anime, episodesAired, nextEpisode, aniListId, relatedMetadataByAnimeId, inSeries, refreshFailed, relations, picturesFetchPending);
    }

    private static bool NeedsFullDetailFetch(AnimeMetadata? anime) =>
        anime is null
        || anime.LastSyncedAt == default
        || DateTimeOffset.UtcNow - anime.LastSyncedAt > RefreshTiers.TtlFor(anime);

    private async Task<Dictionary<int, RelatedMetadata>> GetRelatedMetadataAsync(IEnumerable<int> animeIds, CancellationToken ct)
    {
        var ids = animeIds.Distinct().ToList();
        return await db.AnimeMetadata.AsNoTracking()
            .Where(m => ids.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, m => new RelatedMetadata(m.MediaType, m.EnglishTitle), ct);
    }

    private static NextEpisodeEtaDto? ToEta(DateTimeOffset? nextInstant, DateTimeOffset now)
    {
        if (nextInstant is not { } instant)
            return null;

        var remaining = instant - now;
        return new NextEpisodeEtaDto(remaining.Days, remaining.Hours);
    }
}
