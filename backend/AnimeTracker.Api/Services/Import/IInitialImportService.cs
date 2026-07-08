namespace AnimeTracker.Api.Services.Import;

public interface IInitialImportService
{
    Task RunAsync(CancellationToken ct);
}
