using AnimeTracker.Api.Services.Setup;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Controllers;

/// <summary>First-run setup's own endpoints. They are the only <c>/api</c> routes,
/// beside the MAL connection's and the health check, that answer while setup
/// runs (<c>SetupRequestGate</c>, add-first-run-setup design D4).</summary>
[ApiController]
public class SetupController(SetupStatusService statusService, ISetupCoordinator coordinator) : ControllerBase
{
    /// <summary>Where setup stands: which screen to show, each step's counts and
    /// estimate, what is waiting or limited, and the anime left out for good. Makes no
    /// outside call, so the screen can poll it every second (design D17).</summary>
    [HttpGet("api/setup/status")]
    public async Task<IActionResult> Status(CancellationToken ct) => Ok(await statusService.GetAsync(ct));

    /// <summary>Retry now: everything waiting to retry, and both services' paused
    /// queues, become due at once. A throttle wait is left alone. Like every mutating
    /// call it needs the <c>X-Requested-With</c> header
    /// (<c>CrossSiteRequestGuard</c>).</summary>
    [HttpPost("api/setup/retry-now")]
    public IActionResult RetryNow()
    {
        coordinator.RetryNow();
        return NoContent();
    }
}
