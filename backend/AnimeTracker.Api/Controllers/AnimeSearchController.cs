using AnimeTracker.Api.Services.Search;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Controllers;

[ApiController]
public class AnimeSearchController(IAnimeSearchService searchService) : ControllerBase
{
    /// <summary>Navbar type-ahead search: merges the local cache with a live
    /// MAL search and ranks prefix matches by popularity. Always returns at
    /// most 5 matches.</summary>
    [HttpGet("api/anime/search")]
    public async Task<IActionResult> Search([FromQuery] string? q, CancellationToken ct)
    {
        var results = await searchService.SearchAsync(q ?? "", limit: 5, ct);
        return Ok(results);
    }

    /// <summary>Paginated, sortable full search page — sourced from MAL's own
    /// search order ("relevance") by default.</summary>
    [HttpGet("api/anime/search/page")]
    public async Task<IActionResult> SearchPage([FromQuery] string? q, [FromQuery] string sort = "relevance",
        [FromQuery] int offset = 0, [FromQuery] int limit = 50, CancellationToken ct = default)
    {
        var page = await searchService.SearchPageAsync(q ?? "", sort, Math.Max(offset, 0), Math.Clamp(limit, 1, 50), ct);
        return Ok(page);
    }
}
