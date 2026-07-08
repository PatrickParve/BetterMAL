using AnimeTracker.Api.Services.Dashboard;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Controllers;

[ApiController]
public class DashboardController(IMainDashboardService dashboardService) : ControllerBase
{
    /// <summary>Main page data: currently watching, airing today, and current
    /// season, all read from cached Postgres data.</summary>
    [HttpGet("api/dashboard")]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var dashboard = await dashboardService.GetDashboardAsync(ct);
        return Ok(dashboard);
    }
}
