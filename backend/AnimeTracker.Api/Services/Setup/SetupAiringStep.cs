using AnimeTracker.Api.Data;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Scheduling;

namespace AnimeTracker.Api.Services.Setup;

/// <summary>Setup's fourth step: AniList airing dates for every list anime with no airing-fetched
/// mark, most useful first (design D10; spec "Airing dates are fetched for every list anime,
/// most useful first"). It starts as soon as the list has been read and runs alongside the
/// MyAnimeList steps: AniList has its own pacing and its own outages.
/// <list type="bullet">
/// <item><description><b>Order</b> is the four tiers of <see cref="AiringPriority"/>
/// (<see cref="SetupOrdering.AiringOrder"/>). A batch is the next 25 anime that are due, and may
/// span tiers, since the lookup is per id.</description></item>
/// <item><description><b>One write path</b>: <see cref="IEpisodeScheduleRefreshService.RefreshBatchAsync"/>,
/// which stores exactly what fetching each anime alone would. An anime it reports as failed goes
/// on the retry ladder; the anime it saved carry their own mark, which is what takes them off the
/// list.</description></item>
/// <item><description><b>After Home opens</b> the same queue keeps draining what is left, until
/// nothing is left or waiting, then stops. Nothing here holds Home back: an AniList that is down
/// only pauses this queue.</description></item>
/// </list></summary>
internal sealed class SetupAiringStep(SetupWorkerContext context, AniListServiceHealth health, RetryLadder ladder)
{
    /// <summary>The lookup batch's size, AniList's own: 25 MyAnimeList ids per request, with
    /// relations.</summary>
    internal const int BatchSize = 25;

    public Task RunAsync(CancellationToken ct) =>
        new SetupQueue(SetupStep.Airing, health, ladder, context)
            .RunAsync(GetTargetsAsync, ProcessAsync, BatchSize, ct);

    private async Task<IReadOnlyList<int>> GetTargetsAsync(CancellationToken ct)
    {
        using var scope = context.ScopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AnimeTrackerDbContext>();
        var library = await SetupLibrary.LoadAsync(db, ct);
        var today = scope.ServiceProvider.GetRequiredService<IBroadcastLocalTimeConverter>().GetLocalDate(context.Clock());

        return SetupOrdering.AiringOrder(library.Anime.Where(a => !library.Marked.Contains(a.AnimeId)), context.State.ListOrder, today);
    }

    private async Task ProcessAsync(IReadOnlyList<int> animeIds, CancellationToken ct)
    {
        var reported = new HashSet<int>();
        try
        {
            using var scope = context.ScopeFactory.CreateScope();
            var refreshService = scope.ServiceProvider.GetRequiredService<IEpisodeScheduleRefreshService>();

            await refreshService.RefreshBatchAsync(
                animeIds,
                new RefreshBatchOptions(OnAnimeDone: (animeId, outcome) =>
                {
                    reported.Add(animeId);
                    if (outcome == AiringRefreshOutcome.Failed)
                        ladder.RecordFailure(animeId, context.Clock());
                    else
                        ladder.Clear(animeId);

                    // An anime saved is progress the Home check can act on at once.
                    context.Progress.Pulse();
                }),
                ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // The service reports a failed anime rather than throwing, so this is what is
            // left: something around it (a scope, the database). Whatever it didn't get to
            // wait its turn.
            context.Logger.LogWarning(ex, "Setup: fetching airing dates for {Count} anime failed; they will be tried again.", animeIds.Count);
        }

        foreach (var animeId in animeIds.Where(id => !reported.Contains(id)))
            ladder.RecordFailure(animeId, context.Clock());
    }
}
