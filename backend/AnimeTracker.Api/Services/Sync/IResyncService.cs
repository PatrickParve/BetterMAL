namespace AnimeTracker.Api.Services.Sync;

public interface IResyncService
{
    Task RunAsync(CancellationToken ct);
}
