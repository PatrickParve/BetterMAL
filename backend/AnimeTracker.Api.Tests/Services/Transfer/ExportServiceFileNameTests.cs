using AnimeTracker.Api.Services.Transfer;

namespace AnimeTracker.Api.Tests.Services.Transfer;

// device-transfer's file-name rule (design.md D6, tasks.md 5.6): the first
// 8 hex digits of the device id, plus exportedAt in UTC.
public class ExportServiceFileNameTests
{
    [Fact]
    public void BuildFileNameCarriesTheShortDeviceIdAndTheExportMoment()
    {
        var deviceId = Guid.Parse("7f3c9a12-0000-0000-0000-000000000000");
        var exportedAt = new DateTime(2026, 9, 10, 18, 22, 4, DateTimeKind.Utc);

        var fileName = ExportService.BuildFileName(deviceId, exportedAt);

        Assert.Equal("bettermal-7f3c9a12-20260910-182204.json", fileName);
    }
}
