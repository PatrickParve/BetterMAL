using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Services.Infrastructure;
using AnimeTracker.Api.Services.Metadata;
using AnimeTracker.Api.Services.Setup;

namespace AnimeTracker.Api.Services.Airing;

/// <summary>Drains INewEntryRefreshTrigger's queue: an anime that has just
/// entered my list, by any path, gets its data fetched right away rather than
/// waiting for the next hourly tick or the metadata job's next 10-minute pass —
/// mirrors ImportTrigger's signal-plus-dedicated-loop pattern.
/// <para>On each wake it takes every id queued so far and, for those still in my
/// list, fetches MyAnimeList's full detail first (per anime, only when the row
/// has never had one) and AniList's episode dates second (once for the whole
/// set). MyAnimeList goes first because the airing refresh works out its recheck
/// state from the row's airing status, and prefers MyAnimeList's episode total.</para></summary>
public class NewEntryRefreshBackgroundService(
    IServiceScopeFactory scopeFactory,
    INewEntryRefreshTrigger trigger,
    SetupGate setupGate,
    ILogger<NewEntryRefreshBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Setup's details and airing steps fetch every list anime; a fetch from
        // here would compete with them, so the queue is drained once setup has
        // finished (add-first-run-setup design D2).
        await setupGate.WhenFinished.WaitAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            IReadOnlyList<int> animeIds;
            try
            {
                animeIds = await trigger.WaitAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                await RefreshAsync(animeIds, stoppingToken);
            }
            // Only our own shutdown ends the loop; an HttpClient timeout is a
            // TaskCanceledException too, and fails just these anime.
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "New-entry refresh failed for {Count} anime.", animeIds.Count);
            }
        }
    }

    private async Task RefreshAsync(IReadOnlyList<int> animeIds, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var services = scope.ServiceProvider;
        var entries = services.GetRequiredService<IUserAnimeEntryRepository>();

        // An entry removed since it was queued has nothing left to fetch for,
        // on either service.
        var inList = new List<int>();
        foreach (var animeId in animeIds)
        {
            ct.ThrowIfCancellationRequested();
            if (await entries.GetByAnimeIdAsync(animeId, ct) is not { } entry)
                continue;

            inList.Add(animeId);
            if (entry.Anime.LastSyncedAt == default)
                await FetchFullDetailAsync(animeId, entries, services, ct);
        }

        if (inList.Count == 0)
            return;

        var airing = services.GetRequiredService<IEpisodeScheduleRefreshService>();
        try
        {
            if (inList.Count == 1)
                await airing.RefreshOneAsync(inList[0], ct: ct);
            else
                await airing.RefreshBatchAsync(inList, ct: ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Triggered airing refresh failed for {Count} anime ({AnimeIds}).", inList.Count, string.Join(", ", inList));
        }
    }

    // A failure is logged and ends only this anime's MyAnimeList step: its
    // AniList step still runs, and the tiered refresh retries the detail later.
    private async Task FetchFullDetailAsync(int animeId, IUserAnimeEntryRepository entries, IServiceProvider services, CancellationToken ct)
    {
        try
        {
            // The detail page's visit fetch takes this same key, so an anime
            // added from its own page is fetched once; a waiter re-checks after
            // the gate in case that fetch landed meanwhile.
            using (await services.GetRequiredService<RefreshGate>().LockAsync($"anime:{animeId}", ct))
            {
                if (await entries.GetByAnimeIdAsync(animeId, ct) is not { } entry || entry.Anime.LastSyncedAt != default)
                    return;

                await services.GetRequiredService<IMetadataRefreshService>().RefreshOneAsync(animeId, ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Full-detail fetch for new list entry {AnimeId} failed; its airing refresh still runs.", animeId);
        }
    }
}
