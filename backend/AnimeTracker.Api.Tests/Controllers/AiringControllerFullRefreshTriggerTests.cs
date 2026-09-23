using AnimeTracker.Api.Controllers;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Jobs;
using AnimeTracker.Api.Services.Scheduling;
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
        var controller = new AiringController(new UnusedAiringScheduleService(), trigger, progress, new FakeBroadcastLocalTimeConverter());

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
        var controller = new AiringController(new UnusedAiringScheduleService(), trigger, progress, new FakeBroadcastLocalTimeConverter());

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

    // A small fake: only GetLocalDate is exercised by AiringController, but
    // the interface still needs every member implemented.
    private sealed class FakeBroadcastLocalTimeConverter : IBroadcastLocalTimeConverter
    {
        public DateOnly GetStartOfWeek(DateOnly referenceDate) => referenceDate;
        public DateOnly GetLocalDate(DateTimeOffset instantUtc) => DateOnly.FromDateTime(instantUtc.UtcDateTime);
        public TimeOnly GetLocalTime(DateTimeOffset instantUtc) => TimeOnly.FromDateTime(instantUtc.UtcDateTime);
        public DateTimeOffset LocalMidnightUtc(DateOnly localDate) => new(localDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        public (DayOfWeek LocalDayOfWeek, TimeOnly LocalTime) ConvertBroadcastSlot(DayOfWeek jstDayOfWeek, TimeOnly jstTime, DateTimeOffset referenceUtc) =>
            (jstDayOfWeek, jstTime);
    }
}
