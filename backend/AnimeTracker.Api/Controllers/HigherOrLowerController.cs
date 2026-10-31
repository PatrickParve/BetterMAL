using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Games;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Controllers;

/// <summary>Higher or Lower (add-games-higher-or-lower design.md D3). The game runs
/// on the server, so a pick is sent and the verdict and the next round come back.
/// Every route reads stored data only; nothing here calls MyAnimeList or
/// AniList. Failures map as the design lists them: 400 for settings or a pick
/// that can't be right, 404 for a game no longer in progress, 409 for too few
/// anime to play or a round the game has passed.</summary>
[ApiController]
public class HigherOrLowerController(IHigherOrLowerService service) : ControllerBase
{
    /// <summary>The game in progress, the release years to offer, and two posters
    /// for the Games tile. One read serves both pages. A hidden score is never in
    /// it. <c>hideHentai</c> shapes the years and posters, not a game already
    /// started.</summary>
    [HttpGet("api/games/higher-or-lower")]
    public async Task<IActionResult> GetState([FromQuery] bool hideHentai = false, CancellationToken ct = default) =>
        Ok(await service.GetStateAsync(hideHentai, ct));

    /// <summary>How many anime some settings leave and whether that is enough to
    /// play, for the setup screen's count. <c>types</c> repeats once per type; none
    /// means every type. <c>list</c> is <c>All</c>, <c>OnlyMyList</c> or
    /// <c>NotOnMyList</c>.</summary>
    [HttpGet("api/games/higher-or-lower/pool")]
    public async Task<IActionResult> GetPool(
        [FromQuery] int? yearFrom,
        [FromQuery] int? yearTo,
        [FromQuery] string[]? types,
        [FromQuery] GameListScope list = GameListScope.All,
        [FromQuery] bool hideHentai = false,
        CancellationToken ct = default)
    {
        var settings = new GameSettings(yearFrom, yearTo, types?.ToList(), list, hideHentai);
        try
        {
            return Ok(await service.GetPoolAsync(settings, ct));
        }
        catch (GameSettingsException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Starts a game and returns its first round, replacing any game in
    /// progress. 400 for settings that can't describe a game; 409 when they leave
    /// fewer than two different scores.</summary>
    [HttpPost("api/games/higher-or-lower/runs")]
    public async Task<IActionResult> Start([FromBody] GameSettings settings, CancellationToken ct = default)
    {
        try
        {
            return Ok(await service.StartAsync(settings, ct));
        }
        catch (GameSettingsException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (HigherOrLowerNotPlayableException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>One pick: the verdict, the scores it was judged on, and either the
    /// next round or how the game ended. 400 for an anime that isn't on the table,
    /// 404 for a game no longer in progress, 409 for a round the game has
    /// passed.</summary>
    [HttpPost("api/games/higher-or-lower/runs/{id:int}/picks")]
    public async Task<IActionResult> Pick(int id, [FromBody] HigherOrLowerPickRequest request, CancellationToken ct = default)
    {
        try
        {
            return Ok(await service.PickAsync(id, request.Round, request.AnimeId, ct));
        }
        catch (HigherOrLowerRunNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (HigherOrLowerStaleRoundException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (HigherOrLowerCardNotOnTableException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Ends the game early and answers with its streak. 404 for a game no
    /// longer in progress.</summary>
    [HttpPost("api/games/higher-or-lower/runs/{id:int}/end")]
    public async Task<IActionResult> End(int id, CancellationToken ct = default)
    {
        try
        {
            return Ok(await service.EndAsync(id, ct));
        }
        catch (HigherOrLowerRunNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}
