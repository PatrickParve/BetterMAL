using AnimeTracker.Api.Services.Entries;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Controllers;

[ApiController]
public class EntriesController(IUserAnimeEntryEditService editService) : ControllerBase
{
    /// <summary>Partial edit of a list entry (episode count, status, score,
    /// rewatch count). Creates the entry if the anime isn't in my list yet.</summary>
    [HttpPatch("api/anime/{animeId:int}/entry")]
    public async Task<IActionResult> UpdateEntry(int animeId, [FromBody] UserAnimeEntryEditRequest request, CancellationToken ct)
    {
        try
        {
            var result = await editService.UpdateEntryAsync(animeId, request, ct);
            return Ok(result);
        }
        catch (AnimeNotFoundException)
        {
            return NotFound();
        }
        catch (EntryEditRejectedException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Removes the anime from my list — locally at once, and from
    /// MAL durably in the background.</summary>
    [HttpDelete("api/anime/{animeId:int}/entry")]
    public async Task<IActionResult> DeleteEntry(int animeId, CancellationToken ct)
    {
        try
        {
            await editService.RemoveEntryAsync(animeId, ct);
            return NoContent();
        }
        catch (EntryNotFoundException)
        {
            return NotFound();
        }
    }
}
