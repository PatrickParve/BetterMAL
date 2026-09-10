namespace AnimeTracker.Api.Services.Transfer;

/// <summary>Runs an accepted import when the trigger hands one over
/// (design.md D1). Runs on the host's stopping token, never the request's —
/// closing the tab does not stop an import.</summary>
public class TransferImportBackgroundService(
    IServiceScopeFactory scopeFactory,
    ITransferImportTrigger trigger,
    ITransferImportProgressTracker progress,
    ILogger<TransferImportBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            TransferFile file;
            try
            {
                file = await trigger.WaitAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                using var scope = scopeFactory.CreateScope();
                var runner = scope.ServiceProvider.GetRequiredService<TransferImportRunner>();
                var report = await runner.RunAsync(file, progress, stoppingToken);
                progress.Complete(report);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Import failed.");
                progress.Fail("Nothing from the file was applied.");
            }
            finally
            {
                trigger.Release();
            }
        }
    }
}
