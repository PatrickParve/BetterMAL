using AnimeTracker.Api.Services.Search;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Controllers;

/// <summary>The search page's OP/ED mode (spec <c>theme-search</c>). One read-only route over
/// the stored catalogue of openings and endings: it asks neither AnimeThemes nor MyAnimeList
/// anything, starts no background work and writes nothing.</summary>
[ApiController]
public class ThemeSearchController(IThemeSearchService searchService) : ControllerBase
{
    private const int DefaultLimit = 5;

    // The search page lists up to 200 songs: a broad query ("love" matches 315) shows the
    // first 200, the way the anime results stop at their 60 candidates.
    private const int MaxLimit = 200;

    /// <summary>Songs matching <paramref name="q"/>, best first. The type-ahead asks for the
    /// default 5; the results page asks for up to 200. <c>ready</c> says whether the catalogue
    /// has ever loaded, so "not loaded yet" isn't mistaken for "no songs found".</summary>
    [HttpGet("api/themes/search")]
    public async Task<IActionResult> Search(
        [FromQuery] string? q, [FromQuery] int limit = DefaultLimit, CancellationToken ct = default) =>
        Ok(await searchService.SearchAsync(q ?? "", Math.Clamp(limit, 1, MaxLimit), ct));
}
