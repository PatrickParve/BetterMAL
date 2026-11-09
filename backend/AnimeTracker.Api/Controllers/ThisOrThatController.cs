using AnimeTracker.Api.Services.Ranking.ThisOrThat;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Controllers;

/// <summary>This or That (add-this-or-that-ranking design.md D5). A ranking of one
/// score runs on the server, so a pick is sent and the next state comes back. Every
/// route reads stored data only; nothing here calls MyAnimeList or AniList, and the
/// two GETs write nothing. Failures map as the design lists them: 400 for a score
/// with too few anime to start or a winner that isn't on the table, 404 for a score
/// that isn't offered or a ranking not in progress, 409 for a request made on a
/// ranking that has moved on.</summary>
[ApiController]
public class ThisOrThatController(IThisOrThatService service) : ControllerBase
{
    /// <summary>The scores that can be ranked, highest first, each with its anime
    /// count, its most picks and whether a ranking of it is in progress.</summary>
    [HttpGet("api/this-or-that")]
    public async Task<IActionResult> GetScores(CancellationToken ct = default) =>
        Ok(await service.GetScoresAsync(ct));

    /// <summary>One score and its ranking in progress, if any, as my list stands
    /// now. 404 when the score isn't offered.</summary>
    [HttpGet("api/this-or-that/{score:int}")]
    public async Task<IActionResult> GetScore(int score, CancellationToken ct = default)
    {
        var state = await service.GetScoreAsync(score, ct);
        return state is null
            ? NotFound(new { error = $"Score {score} has nothing to rank." })
            : Ok(state);
    }

    /// <summary>Starts a ranking of the score, or starts it over when the body names
    /// the version of the one in progress. 400 for a score with fewer than two anime;
    /// 409 when a ranking is in progress and the version isn't its own.</summary>
    [HttpPost("api/this-or-that/{score:int}")]
    public async Task<IActionResult> Start(
        int score, [FromBody] ThisOrThatStartRequest? request = null, CancellationToken ct = default)
    {
        try
        {
            return Ok(await service.StartAsync(score, request?.ReplacingVersion, ct));
        }
        catch (ThisOrThatNotEnoughAnimeException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (ThisOrThatStaleException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>One pick on the named pair; a null <c>winnerId</c> is Can't decide.
    /// 400 for a winner that is neither card, 404 with no ranking in progress, 409
    /// when the pair isn't the current one.</summary>
    [HttpPost("api/this-or-that/{score:int}/picks")]
    public async Task<IActionResult> Pick(int score, [FromBody] ThisOrThatPickRequest request, CancellationToken ct = default)
    {
        try
        {
            return Ok(await service.PickAsync(score, request.LeftId, request.RightId, request.WinnerId, ct));
        }
        catch (ThisOrThatRunNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (ThisOrThatStaleException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (ThisOrThatWinnerNotInPairException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Takes back the last pick. 404 with no ranking in progress; 409 for a
    /// version that isn't current or when there is nothing to take back.</summary>
    [HttpPost("api/this-or-that/{score:int}/undo")]
    public async Task<IActionResult> Undo(int score, [FromBody] ThisOrThatVersionRequest request, CancellationToken ct = default)
    {
        try
        {
            return Ok(await service.UndoAsync(score, request.Version, ct));
        }
        catch (ThisOrThatRunNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (ThisOrThatStaleException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>Writes the finished order into my ranking and ends the ranking. 404
    /// with no ranking in progress; 409 for a stale version or a ranking that isn't
    /// finished.</summary>
    [HttpPost("api/this-or-that/{score:int}/save")]
    public async Task<IActionResult> Save(int score, [FromBody] ThisOrThatVersionRequest request, CancellationToken ct = default)
    {
        try
        {
            await service.SaveAsync(score, request.Version, ct);
            return NoContent();
        }
        catch (ThisOrThatRunNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (ThisOrThatStaleException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (ThisOrThatNotFinishedException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>Ends the ranking and leaves my ranking as it was (Keep my old order,
    /// Done). The version is on the query string, as a DELETE carries no body. 400
    /// without it; 404 with no ranking in progress; 409 for a stale version.</summary>
    [HttpDelete("api/this-or-that/{score:int}")]
    public async Task<IActionResult> Discard(int score, [FromQuery] int? version, CancellationToken ct = default)
    {
        if (version is not { } current)
            return BadRequest(new { error = "version is required." });

        try
        {
            await service.DiscardAsync(score, current, ct);
            return NoContent();
        }
        catch (ThisOrThatRunNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (ThisOrThatStaleException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }
}
