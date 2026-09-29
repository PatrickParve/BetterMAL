using AnimeTracker.Api.Data;
using AnimeTracker.Api.Services.Series;

namespace AnimeTracker.Api.Services.Setup;

/// <summary>Setup's third step: build a series for every list anime that isn't covered yet
/// (design D9; spec "Building series covers every list anime"). It starts after the details
/// step, in the details order, and builds with no fetch or probe budget
/// (<see cref="ISeriesService.BuildForSetupAsync"/>).
/// <list type="bullet">
/// <item><description><b>Targets</b> are the list anime that aren't skipped for good, aren't
/// covered by a primary membership in an up-to-date, non-partial series, and weren't found to
/// belong to no series. They are read again after every build, so one build that settled a
/// whole franchise takes its other list anime off the list without building them.</description></item>
/// <item><description><b>A lone anime</b> (<see cref="SetupBuildOutcome.NoSeries"/>) is remembered
/// in memory as settled; <b>a partial series</b> (a member fetch or probe failed) goes on the retry
/// ladder and comes back in its place.</description></item>
/// </list></summary>
internal sealed class SetupSeriesStep(SetupWorkerContext context, MalServiceHealth health, RetryLadder ladder)
{
    public Task RunAsync(CancellationToken ct) =>
        new SetupQueue(SetupStep.Series, health, ladder, context)
            .RunAsync(GetTargetsAsync, ProcessAsync, batchSize: 1, ct);

    private async Task<IReadOnlyList<int>> GetTargetsAsync(CancellationToken ct)
    {
        using var scope = context.ScopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AnimeTrackerDbContext>();
        var library = await SetupLibrary.LoadAsync(db, ct);
        var noSeries = context.State.Snapshot().NoSeriesAnimeIds;

        return SetupOrdering.DetailsOrder(
            library.Anime.Where(a => !a.NotOnMal && !library.Covered.Contains(a.AnimeId) && !noSeries.Contains(a.AnimeId)),
            context.State.ListOrder);
    }

    private async Task ProcessAsync(IReadOnlyList<int> animeIds, CancellationToken ct)
    {
        var animeId = animeIds[0];
        try
        {
            using var scope = context.ScopeFactory.CreateScope();
            var seriesService = scope.ServiceProvider.GetRequiredService<ISeriesService>();

            switch (await seriesService.BuildForSetupAsync(animeId, ct))
            {
                case SetupBuildOutcome.Settled:
                    ladder.Clear(animeId);
                    break;
                case SetupBuildOutcome.NoSeries:
                    context.State.AddNoSeries(animeId);
                    ladder.Clear(animeId);
                    break;
                default:
                    context.Logger.LogWarning(
                        "Setup: the series of anime {AnimeId} is partial because a member failed to fetch; it will be built again.", animeId);
                    ladder.RecordFailure(animeId, context.Clock());
                    break;
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            context.Logger.LogWarning(ex, "Setup: building the series of anime {AnimeId} failed; it will be tried again.", animeId);
            ladder.RecordFailure(animeId, context.Clock());
        }
    }
}
