using AnimeTracker.Api.Services.Search;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Controllers;

[ApiController]
public class AnimeSearchController(IAnimeSearchService searchService) : ControllerBase
{
    /// <summary>Navbar type-ahead search: local cache first, live MAL fallback
    /// when nothing matches locally. Always returns at most 5 matches.</summary>
    [HttpGet("api/anime/search")]
    public async Task<IActionResult> Search([FromQuery] string? q, CancellationToken ct)
    {
        var results = await searchService.SearchAsync(q ?? "", limit: 5, ct);
        return Ok(results);
    }
}
