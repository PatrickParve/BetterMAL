using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Games;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Controllers;

/// <summary>Guess the Anime (add-guess-the-anime design.md D3). The game runs on the
/// server, so a guess is sent and its comparison comes back; the answer is in no
/// response until the game ends. The two GET routes read stored data only. The
/// POSTs may call MyAnimeList, to fetch an anime's missing stats (design D6).
/// Searching has no route here: the field uses <c>GET /api/anime/search</c>.
/// Failures map as the design lists them: 400 for settings or a guess that can't
/// be right, 404 for a game no longer in progress, 409 for no anime to play or a
/// guess the game has passed, and 502, with a reason, for a start or a guess that
/// needed MyAnimeList and couldn't have it.</summary>
[ApiController]
public class GuessTheAnimeController(IGuessTheAnimeService service) : ControllerBase
{
    /// <summary>The game in progress with its guesses compared, the release years
    /// to offer, and two posters for the Games tile. One read serves both pages. The
    /// answer is not in it. <c>hideHentai</c> shapes the years and posters, not a
    /// game already started.</summary>
    [HttpGet("api/games/guess-the-anime")]
    public async Task<IActionResult> GetState([FromQuery] bool hideHentai = false, CancellationToken ct = default) =>
        Ok(await service.GetStateAsync(hideHentai, ct));

    /// <summary>How many anime some settings leave and whether that is enough to
    /// play, for the setup screen's count. <c>types</c> repeats once per type; none
    /// means every type. <c>list</c> is <c>All</c>, <c>OnlyMyList</c> or
    /// <c>NotOnMyList</c>.</summary>
    [HttpGet("api/games/guess-the-anime/pool")]
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

    /// <summary>Starts a game with no guesses, replacing any game in progress. 400
    /// for settings that can't describe a game; 409 when they leave no anime; 502
    /// when no answer could be settled, with the reason.</summary>
    [HttpPost("api/games/guess-the-anime/runs")]
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
        catch (GuessTheAnimeNotPlayableException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (GuessTheAnimeNoAnswerException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message });
        }
    }

    /// <summary>One guess: its row, and when it ended the game, how, with the
    /// answer. 400 for an anime already guessed, 404 for a game no longer in
    /// progress, 409 for a guess the game has passed, 502 when the anime's details
    /// couldn't be fetched, with the reason; none of them counts a guess.</summary>
    [HttpPost("api/games/guess-the-anime/runs/{id:int}/guesses")]
    public async Task<IActionResult> Guess(int id, [FromBody] GuessTheAnimeGuessRequest request, CancellationToken ct = default)
    {
        try
        {
            return Ok(await service.GuessAsync(id, request.Number, request.AnimeId, ct));
        }
        catch (GuessTheAnimeRunNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (GuessTheAnimeStaleGuessException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (GuessTheAnimeAlreadyGuessedException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (GuessTheAnimeDetailsUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message });
        }
    }

    /// <summary>Ends the game early and answers with the answer. 404 for a game no
    /// longer in progress.</summary>
    [HttpPost("api/games/guess-the-anime/runs/{id:int}/end")]
    public async Task<IActionResult> End(int id, CancellationToken ct = default)
    {
        try
        {
            return Ok(await service.EndAsync(id, ct));
        }
        catch (GuessTheAnimeRunNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}
