using System.Text;
using AnimeTracker.Api.Controllers;
using AnimeTracker.Api.Services.Jobs;
using AnimeTracker.Api.Services.Transfer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Tests.Controllers;

// TransferController (design.md D1, tasks.md 5.7, 9.1, 9.3): a plain GET
// that hands the service's bytes back as a file download and forwards the
// request's own User-Agent; POST api/transfer/import, which reads the raw
// body and turns a refusal into 400/409 with `{ "error": … }`; and the
// import status read.
public class TransferControllerTests
{
    private sealed class RecordingExportService : IExportService
    {
        public string? ReceivedUserAgent { get; private set; }

        public Task<(byte[] Bytes, string FileName)> ExportAsync(string? userAgent, CancellationToken ct = default)
        {
            ReceivedUserAgent = userAgent;
            return Task.FromResult<(byte[] Bytes, string FileName)>(([1, 2, 3], "bettermal-abcd1234-20260910-182204.json"));
        }
    }

    private sealed class FakeTransferImportService : ITransferImportService
    {
        public byte[]? ReceivedBytes { get; private set; }
        public Exception? ThrowOnAccept { get; set; }
        public TransferImportStatusSnapshot Result { get; set; } =
            new(TransferImportPhase.Running, 0, 0, "Other Device", DateTimeOffset.UtcNow, null, null);

        public Task<TransferImportStatusSnapshot> AcceptAsync(byte[] bytes, CancellationToken ct = default)
        {
            ReceivedBytes = bytes;
            return ThrowOnAccept is not null ? throw ThrowOnAccept : Task.FromResult(Result);
        }
    }

    private sealed class FakeTransferImportProgressTracker(TransferImportStatusSnapshot snapshot) : ITransferImportProgressTracker
    {
        public TransferImportStatusSnapshot Snapshot { get; } = snapshot;
        public void MarkPending(string? deviceName, DateTimeOffset exportedAt) => throw new NotImplementedException();
        public void Start(int total) => throw new NotImplementedException();
        public void AddToTotal(int n) => throw new NotImplementedException();
        public void ReportProgress(int done) => throw new NotImplementedException();
        public void Complete(TransferImportReport report) => throw new NotImplementedException();
        public void Fail(string reason) => throw new NotImplementedException();
        public void MarkOutcomeSeen(DateTimeOffset finishedAt) => throw new NotImplementedException();
        public void Dismiss(DateTimeOffset finishedAt) => throw new NotImplementedException();

        public JobSnapshot ToJobSnapshot()
        {
            var phase = Snapshot.Phase switch
            {
                TransferImportPhase.NotStarted => JobPhase.NotStarted,
                TransferImportPhase.Running => JobPhase.Running,
                TransferImportPhase.Complete => JobPhase.Complete,
                TransferImportPhase.Failed => JobPhase.Failed,
                _ => throw new ArgumentOutOfRangeException(nameof(Snapshot), Snapshot.Phase, null),
            };
            return new JobSnapshot(phase, Snapshot.Done, Snapshot.Total, Snapshot.Error, null, null);
        }
    }

    private static TransferController CreateController(
        RecordingExportService? exportService = null, string? userAgent = null,
        ITransferImportService? importService = null, ITransferImportProgressTracker? importProgress = null,
        string? requestBody = null)
    {
        var httpContext = new DefaultHttpContext();
        if (userAgent is not null)
            httpContext.Request.Headers.UserAgent = userAgent;
        if (requestBody is not null)
            httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(requestBody));

        return new TransferController(
            exportService ?? new RecordingExportService(),
            importService ?? new FakeTransferImportService(),
            importProgress ?? new FakeTransferImportProgressTracker(new TransferImportStatusSnapshot(TransferImportPhase.NotStarted, 0, 0, null, null, null, null)))
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
        };
    }

    // --- Export (unchanged by this task) ---

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

    // --- Import (tasks.md 9.1, 9.3) ---

    [Fact]
    public async Task ImportReturns202ReadingRunning()
    {
        var importService = new FakeTransferImportService
        {
            Result = new TransferImportStatusSnapshot(TransferImportPhase.Running, 0, 0, "Other Device", DateTimeOffset.UtcNow, null, null),
        };
        var controller = CreateController(importService: importService, requestBody: "{}");

        var result = await controller.Import(CancellationToken.None);

        var accepted = Assert.IsType<AcceptedResult>(result);
        dynamic dto = accepted.Value!;
        Assert.Equal("Running", (string)dto.phase);
        Assert.Equal("Other Device", (string)dto.deviceName);
    }

    [Fact]
    public async Task ImportPassesTheRawBodyToTheService()
    {
        var importService = new FakeTransferImportService();
        var controller = CreateController(importService: importService, requestBody: "{\"formatVersion\":1}");

        await controller.Import(CancellationToken.None);

        Assert.Equal("{\"formatVersion\":1}", Encoding.UTF8.GetString(importService.ReceivedBytes!));
    }

    [Fact]
    public async Task ARefusedFileReturns400WithAnErrorBody()
    {
        var importService = new FakeTransferImportService { ThrowOnAccept = new TransferFileRefusedException("The file is damaged.") };
        var controller = CreateController(importService: importService, requestBody: "not json");

        var result = await controller.Import(CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        dynamic dto = badRequest.Value!;
        Assert.Equal("The file is damaged.", (string)dto.error);
    }

    [Fact]
    public async Task ABlockedImportReturns409WithAnErrorBody()
    {
        var importService = new FakeTransferImportService { ThrowOnAccept = new TransferImportBlockedException("An import is already running.") };
        var controller = CreateController(importService: importService, requestBody: "{}");

        var result = await controller.Import(CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(409, conflict.StatusCode);
        dynamic dto = conflict.Value!;
        Assert.Equal("An import is already running.", (string)dto.error);
    }

    [Fact]
    public void GetImportStatusReturnsTheStatusShape()
    {
        var report = new TransferImportReport([], [], [], []);
        var snapshot = new TransferImportStatusSnapshot(TransferImportPhase.Complete, 5, 5, "Other Device", DateTimeOffset.UtcNow, report, null);
        var controller = CreateController(importProgress: new FakeTransferImportProgressTracker(snapshot));

        var result = controller.GetImportStatus();

        var ok = Assert.IsType<OkObjectResult>(result);
        dynamic dto = ok.Value!;
        Assert.Equal("Complete", (string)dto.phase);
        Assert.Equal(5, (int)dto.done);
        Assert.Equal(5, (int)dto.total);
        Assert.Equal("Other Device", (string)dto.deviceName);
        Assert.NotNull(dto.report);
    }

    [Fact]
    public void GetImportStatusReportsWhenTheRunEndedAndWhetherItWasClosed()
    {
        var finishedAt = DateTimeOffset.UtcNow;
        var snapshot = new TransferImportStatusSnapshot(
            TransferImportPhase.Complete, 5, 5, "Other Device", DateTimeOffset.UtcNow, new TransferImportReport([], [], [], []), null,
            FinishedAt: finishedAt, Dismissed: true);
        var controller = CreateController(importProgress: new FakeTransferImportProgressTracker(snapshot));

        var ok = Assert.IsType<OkObjectResult>(controller.GetImportStatus());

        dynamic dto = ok.Value!;
        Assert.Equal(finishedAt, (DateTimeOffset)dto.finishedAt);
        Assert.True((bool)dto.dismissed);
    }

    // --- Closing the outcome (simplify-settings-and-first-fetch-states 4.3, 4.4) ---

    private static TransferImportProgressTracker CompletedTracker()
    {
        var tracker = new TransferImportProgressTracker();
        tracker.MarkPending("Other Device", DateTimeOffset.UtcNow);
        tracker.Complete(new TransferImportReport([], [], [], []));
        return tracker;
    }

    [Fact]
    public void DismissClosesTheRunItNamesAndReturnsTheCurrentStatus()
    {
        var tracker = CompletedTracker();
        var finishedAt = tracker.Snapshot.FinishedAt!.Value;
        var controller = CreateController(importProgress: tracker);

        var result = controller.DismissImport(new TransferImportDismissRequest(finishedAt));

        var ok = Assert.IsType<OkObjectResult>(result);
        dynamic dto = ok.Value!;
        Assert.Equal("Complete", (string)dto.phase);
        Assert.True((bool)dto.dismissed);
        Assert.Equal(finishedAt, (DateTimeOffset)dto.finishedAt);
        Assert.True(tracker.Snapshot.OutcomeSeen);
    }

    [Fact]
    public void AStaleDismissReturnsTheStatusUnchanged()
    {
        var tracker = CompletedTracker();
        var controller = CreateController(importProgress: tracker);

        var result = controller.DismissImport(new TransferImportDismissRequest(DateTimeOffset.UtcNow.AddMinutes(-5)));

        var ok = Assert.IsType<OkObjectResult>(result);
        dynamic dto = ok.Value!;
        Assert.Equal("Complete", (string)dto.phase);
        Assert.False((bool)dto.dismissed);
        Assert.False(tracker.Snapshot.OutcomeSeen);
    }
}
