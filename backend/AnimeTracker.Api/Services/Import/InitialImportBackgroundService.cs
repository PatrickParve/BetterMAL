using AnimeTracker.Api.Services.Mal.Auth;

namespace AnimeTracker.Api.Services.Import;

/// <summary>Runs the initial import when signaled — by the OAuth callback on
/// first authorization, or by itself on startup if a token already exists (so
/// an import interrupted by an app/PC restart resumes automatically).</summary>
public class InitialImportBackgroundService(
    IServiceScopeFactory scopeFactory,
    IImportTrigger trigger,
    ILogger<InitialImportBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await SignalIfAlreadyAuthorizedAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await trigger.WaitAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                using var scope = scopeFactory.CreateScope();
                var importService = scope.ServiceProvider.GetRequiredService<IInitialImportService>();
                await importService.RunAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Initial import run failed.");
            }
        }
    }

    private async Task SignalIfAlreadyAuthorizedAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var tokenStore = scope.ServiceProvider.GetRequiredService<IMalTokenStore>();
        if (await tokenStore.GetAsync(ct) is not null)
            trigger.Signal();
    }
}
