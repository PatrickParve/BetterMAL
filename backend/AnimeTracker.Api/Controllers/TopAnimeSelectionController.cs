using AnimeTracker.Api.Data.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Controllers;

[ApiController]
public class TopAnimeSelectionController(ITopAnimeSelectionRepository repository) : ControllerBase
{
    /// <summary>The current manual "My top anime" selection, if any. An empty
    /// list means no manual selection is stored and the profile page falls
    /// back to the default next-highest auto-fill.</summary>
    [HttpGet("api/top-anime/selection")]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var animeIds = await repository.GetSelectedAnimeIdsAsync(ct);
        return Ok(new { animeIds });
    }

    /// <summary>Replaces the manual selection wholesale. Pass an empty list to
    /// clear it and revert to the default auto-fill.</summary>
    [HttpPut("api/top-anime/selection")]
    public async Task<IActionResult> Replace([FromBody] TopAnimeSelectionRequest request, CancellationToken ct)
    {
        try
        {
            await repository.ReplaceSelectionAsync(request.AnimeIds, ct);
            return NoContent();
        }
        catch (UnknownAnimeIdsException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

public class TopAnimeSelectionRequest
{
    public List<int> AnimeIds { get; set; } = [];
}
