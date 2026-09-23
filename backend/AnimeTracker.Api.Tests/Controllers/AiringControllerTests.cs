using AnimeTracker.Api.Controllers;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Jobs;
using AnimeTracker.Api.Services.Scheduling;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Tests.Controllers;

// AiringController.GetWeek (design D8, tasks.md 2.4): a week outside
// AiringWeekRange is refused with a 400 before the schedule is read; a week
// inside it, or an absent one, reaches the service as usual.
public class AiringControllerTests
{
    [Theory]
    [InlineData(1916, 12, 31)]
    [InlineData(9999, 12, 31)]
    public async Task GetWeek_ARequestedWeekOutsideTheRangeIsRejectedWithoutReachingTheService(int year, int month, int day)
    {
        var service = new RecordingAiringScheduleService();
        var controller = MakeController(service);

        var result = await controller.GetWeek(new DateOnly(year, month, day), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task GetWeek_AFarFutureWeekIsRejectedWithoutReachingTheService()
    {
        var service = new RecordingAiringScheduleService();
        var controller = MakeController(service);

        var result = await controller.GetWeek(new DateOnly(DateTime.UtcNow.Year + 3, 1, 1), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task GetWeek_TheArchiveFloorIsPassedThroughToTheService()
    {
        var service = new RecordingAiringScheduleService();
        var controller = MakeController(service);

        var result = await controller.GetWeek(new DateOnly(1917, 1, 1), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(new DateOnly(1917, 1, 1), Assert.Single(service.Requests));
    }

    [Fact]
    public async Task GetWeek_ANullWeekIsNeverCheckedAndReachesTheService()
    {
        var service = new RecordingAiringScheduleService();
        var controller = MakeController(service);

        var result = await controller.GetWeek(null, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Null(Assert.Single(service.Requests));
    }

    private static AiringController MakeController(IAiringScheduleService service) =>
        new(service, new AiringFullRefreshTrigger(), new AiringFullRefreshProgress(), new FakeBroadcastLocalTimeConverter());

    private sealed class RecordingAiringScheduleService : IAiringScheduleService
    {
        public List<DateOnly?> Requests { get; } = [];

        public Task<AiringWeekDto> GetWeekAsync(DateOnly? weekReferenceDate, CancellationToken ct = default)
        {
            Requests.Add(weekReferenceDate);
            var weekStart = weekReferenceDate ?? new DateOnly(2026, 9, 21);
            return Task.FromResult(new AiringWeekDto(weekStart, weekStart.AddDays(6), []));
        }
    }

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
