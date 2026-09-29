using AnimeTracker.Api.Services.Jobs;
using AnimeTracker.Api.Services.Setup;

namespace AnimeTracker.Api.Services.Airing;

/// <summary>Runs the settings page's manual "Refresh all airing dates" and
/// "Force all airing dates" actions when signaled — re-fetches AniList airing
/// data for every my-list anime except, unless forced, those whose airing
/// history is complete (see
/// IEpisodeScheduleRefreshService.GetFullRefreshTargetsAsync). Both are one job,
/// batched, and both look every unknown anime up again: a refresh started by hand
/// ignores the 90-day wait. Manual-only, like SeriesBulkBuildBackgroundService:
/// no auto-signal on startup, since the automatic hourly tick already keeps data
/// fresh — this is for "something looks wrong, force a full re-fetch" and there's
/// nothing to resume on a restart.</summary>
public class AiringFullRefreshBackgroundService(
    IServiceScopeFactory scopeFactory,
    IAiringFullRefreshTrigger trigger,
    AiringFullRefreshProgress progress,
    SetupGate setupGate,
    ILogger<AiringFullRefreshBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // The API refuses the button until setup has finished, so nothing is
        // queued before then; this keeps the job from running early whatever
        // signals it (add-first-run-setup design D2).
        await setupGate.WhenFinished.WaitAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            bool force;
            try
            {
                force = await trigger.WaitAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                using var scope = scopeFactory.CreateScope();
                var refreshService = scope.ServiceProvider.GetRequiredService<IEpisodeScheduleRefreshService>();
                var targets = await refreshService.GetFullRefreshTargetsAsync(force, stoppingToken);
                progress.SetTotal(targets.Count);
                var processed = 0;
                var result = await refreshService.RefreshBatchAsync(
                    targets,
                    new RefreshBatchOptions(RelookupAbsent: true, OnAnimeDone: (_, _) => progress.ReportProgress(++processed)),
                    stoppingToken);
                if (result.Failed > 0)
                    progress.Fail($"{result.Failed} of {targets.Count} anime couldn't be refreshed from AniList.");
                else
                    progress.Complete();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Manual airing full refresh failed.");
                progress.Fail(JobFailure.Describe(ex, "AniList"));
            }
        }
    }
}
