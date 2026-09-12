using AnimeTracker.Api.Controllers;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Jobs;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Tests.Controllers;

// AiringController.TriggerFullRefresh (background-jobs "A job is started
// once"/design.md D2), mirroring SeriesControllerBulkBuildTests: two presses
// both report Running, and only the first queues a signal.
public class AiringControllerFullRefreshTriggerTests
{
    [Fact]
    public void TwoPressesGiveRunningBothTimes()
    {
        var trigger = new AiringFullRefreshTrigger();
        var progress = new AiringFullRefreshProgress();
        var controller = new AiringController(new UnusedAiringScheduleService(), trigger, progress);

        Assert.Equal(JobPhase.NotStarted, progress.Snapshot.Phase);

        var first = Assert.IsType<AcceptedResult>(controller.TriggerFullRefresh());
        Assert.Equal(JobPhase.Running, progress.Snapshot.Phase);
        Assert.Equal("Running", Assert.IsType<JobDto>(first.Value).Phase);

        var second = Assert.IsType<AcceptedResult>(controller.TriggerFullRefresh());
        Assert.Equal(JobPhase.Running, progress.Snapshot.Phase);
        Assert.Equal("Running", Assert.IsType<JobDto>(second.Value).Phase);
    }

    [Fact]
    public async Task ExactlyOneSignalIsObservableAcrossTwoPresses()
    {
        var trigger = new AiringFullRefreshTrigger();
        var progress = new AiringFullRefreshProgress();
        var controller = new AiringController(new UnusedAiringScheduleService(), trigger, progress);

        controller.TriggerFullRefresh();
        controller.TriggerFullRefresh();

        using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2)))
            await trigger.WaitAsync(cts.Token);

        using var secondCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await Assert.ThrowsAsync<OperationCanceledException>(() => trigger.WaitAsync(secondCts.Token));
    }

    private sealed class UnusedAiringScheduleService : IAiringScheduleService
    {
        public Task<AiringWeekDto> GetWeekAsync(DateOnly? weekReferenceDate, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }
}
