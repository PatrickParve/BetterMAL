using AnimeTracker.Api.Services.Search;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Controllers;

[ApiController]
public class AnimeSearchController(IAnimeSearchService searchService) : ControllerBase
{
    /// <summary>Navbar type-ahead search: merges the local cache with a live
    /// MAL search and ranks prefix matches by popularity. Always returns at
    /// most 5 rows, of which up to 2 may be matched series (kind "series")
    /// pinned ahead of the anime rows.
    ///
    /// Answered in two stages (design.md D1): the default (or any
    /// unrecognised) <paramref name="stage"/> is the merged local + live
    /// ranking, unchanged from before; <c>stage=local</c> is the cache-only
    /// stage, ranked and shaped identically but with no MAL call in the path.
    /// Both return the same row shape.</summary>
    [HttpGet("api/anime/search")]
    public async Task<IActionResult> Search([FromQuery] string? q, [FromQuery] string? stage, CancellationToken ct)
    {
        var results = await searchService.SearchAsync(q ?? "", limit: 5, includeLive: stage != "local", ct);
        return Ok(results);
    }

    /// <summary>Paginated, sortable full search page — sourced from MAL's own
    /// search order ("relevance") by default. Under the relevance sort, the
    /// response also carries up to 3 matched series separately from the
    /// anime items; other sorts carry none.</summary>
    [HttpGet("api/anime/search/page")]
    public async Task<IActionResult> SearchPage([FromQuery] string? q, [FromQuery] string sort = "relevance",
        [FromQuery] int offset = 0, [FromQuery] int limit = 50, CancellationToken ct = default)
    {
        var page = await searchService.SearchPageAsync(q ?? "", sort, Math.Max(offset, 0), Math.Clamp(limit, 1, 60), ct);
        return Ok(page);
    }
}
