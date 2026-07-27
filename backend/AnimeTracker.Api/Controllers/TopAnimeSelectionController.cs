using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Services.Profile;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Controllers;

[ApiController]
public class TopAnimeSelectionController(IProfileService profileService) : ControllerBase
{
    /// <summary>Replaces the tier order for the given tiers wholesale, using
    /// the slot-preserving merge: every member of an edited tier becomes
    /// explicitly ordered, and members a filtered view hid keep their
    /// relative positions.</summary>
    [HttpPut("api/top-anime/order")]
    public async Task<IActionResult> ReplaceOrder([FromBody] TopAnimeOrderRequest request, CancellationToken ct)
    {
        if (!TopAnimeMediaTypeScope.IsValid(request.MediaType))
            return BadRequest(new { error = $"Unknown mediaType: {request.MediaType}" });

        try
        {
            await profileService.ApplyTopAnimeOrderAsync(request.Tiers, ct);
            return NoContent();
        }
        catch (UnknownAnimeIdsException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (TopAnimeTierScoreMismatchException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

public class TopAnimeOrderRequest
{
    public string MediaType { get; set; } = "";
    public List<TopAnimeTierOrderRequest> Tiers { get; set; } = [];
}
