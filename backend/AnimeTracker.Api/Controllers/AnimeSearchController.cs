using AnimeTracker.Api.Services.Search;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Controllers;

[ApiController]
public class AnimeSearchController(IAnimeSearchService searchService) : ControllerBase
{
    private const int DefaultLimit = 5;
    // As many rows as the merged search has candidates to give: MAL's live search
    // is asked for 60 (AnimeSearchService.LiveSearchCandidates).
    private const int MaxLimit = 60;

    /// <summary>Navbar type-ahead search: merges the local cache with a live
    /// MAL search and ranks prefix matches by popularity. Always returns at
    /// most 5 rows, of which up to 2 may be matched series (kind "series")
    /// pinned ahead of the anime rows.
    ///
    /// Answered in two stages (design.md D1): the default (or any
    /// unrecognised) <paramref name="stage"/> is the merged local + live
    /// ranking, unchanged from before; <c>stage=local</c> is the cache-only
    /// stage, ranked and shaped identically but with no MAL call in the path.
    /// Both return the same row shape.
    ///
    /// Two optional parameters serve a caller that wants anime to choose from
    /// (add-guess-the-anime design.md D7), and change nothing when left out:
    /// <c>series=false</c> lists no series rows, though the series build a
    /// merged search schedules still happens, and <c>limit</c> raises the row
    /// count from 5, up to <see cref="MaxLimit"/>, for a list that scrolls.</summary>
    [HttpGet("api/anime/search")]
    public async Task<IActionResult> Search(
        [FromQuery] string? q, [FromQuery] string? stage, [FromQuery] bool series = true,
        [FromQuery] int limit = DefaultLimit, CancellationToken ct = default)
    {
        var results = await searchService.SearchAsync(
            q ?? "", Math.Clamp(limit, 1, MaxLimit), includeLive: stage != "local", includeSeries: series, ct);
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
