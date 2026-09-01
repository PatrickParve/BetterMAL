using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Services.Profile;
using AnimeTracker.Api.Services.Ranking;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Controllers;

/// <summary>The anime-ranking capability's HTTP surface (design.md D4/D7):
/// reading a tier-scoped slice of the ranking for the editor, and saving an
/// edited tier order. Supersedes <c>/api/top-anime/order</c>, which is now
/// <see cref="ReplaceOrder"/>.</summary>
[ApiController]
public class RankingController(IAnimeRankingService rankingService) : ControllerBase
{
    /// <summary>The editor's score selector (every non-empty score, highest
    /// first, with its hand-orderable count) and one score's full
    /// hand-orderable membership in ranking order, each carrying its overall
    /// rank. Omitting <paramref name="score"/> selects the highest non-empty
    /// score under the scope; there being none is not an error, just an
    /// empty selector and no tier.</summary>
    [HttpGet("api/rankings")]
    public async Task<IActionResult> Get([FromQuery] string? mediaType, [FromQuery] int? score, CancellationToken ct)
    {
        if (!TopAnimeMediaTypeScope.IsValid(mediaType))
            return BadRequest(new { error = $"Unknown mediaType: {mediaType}" });

        var scores = await rankingService.GetScoreCountsAsync(mediaType, ct);

        var effectiveScore = score ?? scores.FirstOrDefault()?.Score;
        if (effectiveScore is not { } resolvedScore)
            return Ok(new AnimeRankingResponseDto(scores, null));

        var tier = await rankingService.GetTierAsync(resolvedScore, mediaType, ct);
        if (tier is null)
            return BadRequest(new { error = $"Unknown score: {resolvedScore}" });

        return Ok(new AnimeRankingResponseDto(scores, tier));
    }

    /// <summary>Replaces the tier order for the given tiers wholesale, using
    /// the slot-preserving merge: every member of an edited tier becomes
    /// explicitly ordered, and members a filtered view hid keep their
    /// relative positions.</summary>
    [HttpPut("api/rankings/order")]
    public async Task<IActionResult> ReplaceOrder([FromBody] AnimeRankingOrderRequest request, CancellationToken ct)
    {
        if (!TopAnimeMediaTypeScope.IsValid(request.MediaType))
            return BadRequest(new { error = $"Unknown mediaType: {request.MediaType}" });

        try
        {
            await rankingService.ApplyTierOrderAsync(request.Tiers, ct);
            return NoContent();
        }
        catch (UnknownAnimeIdsException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (AnimeRankingTierScoreMismatchException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>The series page's tied-favourite reorder: moves
    /// <see cref="AnimeRankingMoveAdjacentRequest.DemotedAnimeId"/> to sit
    /// immediately after <see cref="AnimeRankingMoveAdjacentRequest.PromotedAnimeId"/>
    /// within their shared score tier, so a favourite tie broken on the
    /// series page is reflected the same way everywhere else the ranking is
    /// read. A no-op, reported as success, when the pair isn't currently
    /// hand-orderable at the same score.</summary>
    [HttpPut("api/rankings/move-adjacent")]
    public async Task<IActionResult> MoveAdjacent([FromBody] AnimeRankingMoveAdjacentRequest request, CancellationToken ct)
    {
        await rankingService.MoveAdjacentAsync(request.PromotedAnimeId, request.DemotedAnimeId, ct);
        return NoContent();
    }
}

public record AnimeRankingResponseDto(List<AnimeRankingScoreCountDto> Scores, AnimeRankingTierDto? Tier);

public class AnimeRankingOrderRequest
{
    public string MediaType { get; set; } = "";
    public List<AnimeRankingTierOrderRequest> Tiers { get; set; } = [];
}

public record AnimeRankingMoveAdjacentRequest(int PromotedAnimeId, int DemotedAnimeId);
