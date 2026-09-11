using AnimeTracker.Api.Controllers;
using AnimeTracker.Api.Services.ListBackup;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Tests.Controllers;

// ListBackupController (design.md D1, tasks.md 3.3): a plain GET that hands
// the service's bytes back as a file download and forwards the request's
// own User-Agent, the same shape TransferControllerTests asserts for Export.
public class ListBackupControllerTests
{
    private sealed class RecordingListBackupService : IListBackupService
    {
        public string? ReceivedUserAgent { get; private set; }

        public Task<(byte[] Bytes, string FileName)> ExportAsync(string? userAgent, CancellationToken ct = default)
        {
            ReceivedUserAgent = userAgent;
            return Task.FromResult<(byte[] Bytes, string FileName)>(([1, 2, 3], "bettermal-list-abcd1234-20260910-182204.json"));
        }
    }

    private static ListBackupController CreateController(RecordingListBackupService service, string? userAgent = null)
    {
        var httpContext = new DefaultHttpContext();
        if (userAgent is not null)
            httpContext.Request.Headers.UserAgent = userAgent;

        return new ListBackupController(service)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
        };
    }

    [Fact]
    public async Task ExportReturnsAFileResultWithTheServicesContentTypeAndFileName()
    {
        var service = new RecordingListBackupService();
        var controller = CreateController(service, "AnAgent/1.0");

        var result = await controller.Export(CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/json", file.ContentType);
        Assert.Equal("bettermal-list-abcd1234-20260910-182204.json", file.FileDownloadName);
        Assert.Equal(new byte[] { 1, 2, 3 }, file.FileContents);
    }

    [Fact]
    public async Task ExportPassesTheRequestsUserAgentToTheService()
    {
        var service = new RecordingListBackupService();
        var controller = CreateController(service, "AnAgent/1.0");

        await controller.Export(CancellationToken.None);

        Assert.Equal("AnAgent/1.0", service.ReceivedUserAgent);
    }
}
