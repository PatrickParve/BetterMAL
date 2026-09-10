using AnimeTracker.Api.Controllers;
using AnimeTracker.Api.Services.Transfer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Tests.Controllers;

// TransferController (design.md D1, tasks.md 5.7): a plain GET that hands
// the service's bytes back as a file download, and forwards the request's
// own User-Agent — never one supplied by the caller.
public class TransferControllerTests
{
    private class RecordingExportService : IExportService
    {
        public string? ReceivedUserAgent { get; private set; }

        public Task<(byte[] Bytes, string FileName)> ExportAsync(string? userAgent, CancellationToken ct = default)
        {
            ReceivedUserAgent = userAgent;
            return Task.FromResult<(byte[] Bytes, string FileName)>(([1, 2, 3], "bettermal-abcd1234-20260910-182204.json"));
        }
    }

    private static TransferController CreateController(RecordingExportService service, string? userAgent)
    {
        var httpContext = new DefaultHttpContext();
        if (userAgent is not null)
            httpContext.Request.Headers.UserAgent = userAgent;

        return new TransferController(service)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
        };
    }

    [Fact]
    public async Task ExportReturnsAFileResultWithTheServicesContentTypeAndFileName()
    {
        var service = new RecordingExportService();
        var controller = CreateController(service, "AnAgent/1.0");

        var result = await controller.Export(CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/json", file.ContentType);
        Assert.Equal("bettermal-abcd1234-20260910-182204.json", file.FileDownloadName);
        Assert.Equal(new byte[] { 1, 2, 3 }, file.FileContents);
    }

    [Fact]
    public async Task ExportPassesTheRequestsUserAgentToTheService()
    {
        var service = new RecordingExportService();
        var controller = CreateController(service, "AnAgent/1.0");

        await controller.Export(CancellationToken.None);

        Assert.Equal("AnAgent/1.0", service.ReceivedUserAgent);
    }
}
