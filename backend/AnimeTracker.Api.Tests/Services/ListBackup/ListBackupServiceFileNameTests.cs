using AnimeTracker.Api.Services.ListBackup;

namespace AnimeTracker.Api.Tests.Services.ListBackup;

// list-backup's file-name rule (design.md D7, tasks.md 3.2): the `list`
// segment, the first 8 hex digits of the device id, and the backup moment.
public class ListBackupServiceFileNameTests
{
    [Fact]
    public void BuildFileNameCarriesTheListSegmentTheShortDeviceIdAndTheBackupMoment()
    {
        var deviceId = Guid.Parse("7f3c9a12-0000-0000-0000-000000000000");
        var exportedAt = new DateTime(2026, 9, 11, 9, 15, 30, DateTimeKind.Utc);

        var fileName = ListBackupService.BuildFileName(deviceId, exportedAt);

        Assert.Equal("bettermal-list-7f3c9a12-20260911-091530.json", fileName);
    }
}
