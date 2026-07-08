using AnimeTracker.Api.Services.Library;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Controllers;

[ApiController]
public class MyListController(IMyListService myListService) : ControllerBase
{
    /// <summary>Full my-list read model, straight from Postgres — grouping by
    /// status, status filtering, sorting, and rank numbers are all client-side
    /// concerns against this single fetch.</summary>
    [HttpGet("api/my-list")]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var items = await myListService.GetMyListAsync(ct);
        return Ok(items);
    }
}
