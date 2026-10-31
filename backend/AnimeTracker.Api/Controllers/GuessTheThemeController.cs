using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.AnimeThemes;
using AnimeTracker.Api.Services.Games;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Server.Kestrel.Core.Features;

namespace AnimeTracker.Api.Controllers;

/// <summary>Guess the Opening/Ending (add-anime-themes-and-guess-the-theme design.md D7). The game
/// runs on the server, so a guess is sent and its result comes back; nothing that names a song is
/// in any response until the song is revealed. The two state GETs read stored data only, and the
/// media GETs write nothing. No route here calls MyAnimeList or AniList. Searching has no route of
/// its own: the field uses <c>GET /api/anime/search</c>.
/// Failures map as the design lists them: 400 for settings or a guess that can't be right, 404 for
/// a game or a song it doesn't hold, 409 for no song to play, a move the game has passed, a move
/// out of its phase, or a file that is still there, 410 for a song whose file has gone, and 502,
/// with a reason, when AnimeThemes didn't answer.</summary>
[ApiController]
public class GuessTheThemeController(
    IGuessTheThemeService service,
    IAnimeThemesMediaRelay relay,
    IAnimeThemesMediaHold media,
    ILogger<GuessTheThemeController> logger) : ControllerBase
{
    /// <summary>The game in progress, the release years to offer, two posters for the Games tile and
    /// whether the catalogue has ever loaded. One read serves both pages. No unrevealed song is in
    /// it. <c>hideHentai</c> shapes the years and posters, not a game already started.</summary>
    [HttpGet("api/games/guess-the-theme")]
    public async Task<IActionResult> GetState([FromQuery] bool hideHentai = false, CancellationToken ct = default) =>
        Ok(await service.GetStateAsync(hideHentai, ct));

    /// <summary>How many anime some settings leave, for the setup screen's count. <c>types</c> repeats
    /// once per type; none means every type. <c>list</c> is <c>All</c>, <c>OnlyMyList</c> or
    /// <c>NotOnMyList</c>, <c>themes</c> is <c>All</c>, <c>Openings</c> or <c>Endings</c>.</summary>
    [HttpGet("api/games/guess-the-theme/pool")]
    public async Task<IActionResult> GetPool(
        [FromQuery] int? yearFrom,
        [FromQuery] int? yearTo,
        [FromQuery] string[]? types,
        [FromQuery] GameListScope list = GameListScope.All,
        [FromQuery] GameThemeChoice themes = GameThemeChoice.All,
        [FromQuery] bool hideHentai = false,
        CancellationToken ct = default)
    {
        var settings = new GuessTheThemeSettings(yearFrom, yearTo, types?.ToList(), list, hideHentai, themes);
        try
        {
            return Ok(await service.GetPoolAsync(settings, ct));
        }
        catch (GameSettingsException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Has a song ready for these settings, so Play can start on it at once. The setup screen
    /// asks while it's open. Answers 204 whether or not there was a song to ready, and 400 for settings
    /// that can't describe a game.</summary>
    [HttpPost("api/games/guess-the-theme/prepare")]
    public async Task<IActionResult> Prepare([FromBody] GuessTheThemeSettings settings, CancellationToken ct = default)
    {
        try
        {
            await service.PrepareAsync(settings, ct);
            return NoContent();
        }
        catch (GameSettingsException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Starts a game on a first song drawn, replacing any game of this kind in progress. 400
    /// for settings that can't describe a game; 409 when they leave no song to play.</summary>
    [HttpPost("api/games/guess-the-theme/runs")]
    public async Task<IActionResult> Start([FromBody] GuessTheThemeSettings settings, CancellationToken ct = default)
    {
        try
        {
            return Ok(await service.StartAsync(settings, ct));
        }
        catch (GameSettingsException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (GuessTheThemeNotPlayableException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>One guess at the current length. 400 for an anime already guessed this round, 404 for
    /// a game no longer in progress, 409 for a move the game has passed or a guess during a
    /// reveal.</summary>
    [HttpPost("api/games/guess-the-theme/runs/{id:int}/guesses")]
    public Task<IActionResult> Guess(int id, [FromBody] GuessTheThemeGuessRequest request, CancellationToken ct = default) =>
        MoveAsync(() => service.GuessAsync(id, request.Move, request.AnimeId, ct));

    /// <summary>A skip, which is a guess of nothing. 404 for a game no longer in progress, 409 for a
    /// move the game has passed or a skip during a reveal.</summary>
    [HttpPost("api/games/guess-the-theme/runs/{id:int}/skips")]
    public Task<IActionResult> Skip(int id, [FromBody] GuessTheThemeSkipRequest request, CancellationToken ct = default) =>
        MoveAsync(() => service.SkipAsync(id, request.Move, ct));

    /// <summary>Next song, from a reveal. 404 for a game no longer in progress, 409 for a move the
    /// game has passed or Next song outside a reveal.</summary>
    [HttpPost("api/games/guess-the-theme/runs/{id:int}/next")]
    public Task<IActionResult> Next(int id, [FromBody] GuessTheThemeNextRequest request, CancellationToken ct = default) =>
        MoveAsync(() => service.NextAsync(id, request.Move, ct));

    /// <summary>Ends the game early. During a round it answers with the song; during a reveal, with
    /// nothing. 404 for a game no longer in progress.</summary>
    [HttpPost("api/games/guess-the-theme/runs/{id:int}/end")]
    public Task<IActionResult> End(int id, CancellationToken ct = default) =>
        MoveAsync(() => service.EndAsync(id, ct));

    /// <summary>Another song for the round being played, whose file has gone. 404 for a game or a
    /// song it doesn't hold, 409 when the file is still there, 502 when it couldn't be checked.</summary>
    [HttpPost("api/games/guess-the-theme/runs/{id:int}/replace")]
    public Task<IActionResult> Replace(int id, [FromBody] GuessTheThemeReplaceRequest request, CancellationToken ct = default) =>
        MoveAsync(() => service.ReplaceAsync(id, request.Round, ct));

    /// <summary>The first part of a round's audio, and nothing else: the host's file name, its
    /// `Content-Disposition` and its cookies name the show or mean nothing here, so no upstream header
    /// is copied. It comes from what has been read ahead, or waits for the read still under way.
    /// <c>audio/ogg</c>, told not to be stored. 404 for a song the game doesn't hold, 410 when
    /// AnimeThemes no longer has the file, 502 when it didn't answer. Writes nothing, so it is a
    /// GET.</summary>
    [HttpGet("api/games/guess-the-theme/runs/{id:int}/clip")]
    public async Task<IActionResult> Clip(int id, [FromQuery] int round, CancellationToken ct = default)
    {
        string source;
        try
        {
            source = await service.GetClipSourceAsync(id, round, ct);
        }
        catch (Exception ex) when (ex is GuessTheThemeRunNotFoundException or GuessTheThemeRoundNotFoundException)
        {
            return NotFound(new { error = ex.Message });
        }

        byte[] clip;
        try
        {
            clip = await media.ClipAsync(source, ct);
        }
        catch (AnimeThemesMediaGoneException ex)
        {
            return StatusCode(StatusCodes.Status410Gone, new { error = ex.Message });
        }
        catch (AnimeThemesMediaUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message });
        }

        Response.Headers.CacheControl = "no-store";
        return File(clip, "audio/ogg");
    }

    /// <summary>A revealed song's video, from the app's own address so it can start from what has been
    /// read ahead: the start of the file comes from memory and the rest straight from AnimeThemes as it
    /// is watched. It answers a `Range` as a browser's video needs it to, and no upstream header leaves.
    /// A reveal has named the song, and this is by the song and not the game, since a reveal outlives
    /// the game it ended. 404 for a theme with no video, 410 when AnimeThemes no longer has the file,
    /// 502 when it didn't answer, 416 for a range the file doesn't have. A body that fails part way is
    /// cut off, not ended, so the browser sees a failed load rather than a short video.</summary>
    [HttpGet("api/games/guess-the-theme/video")]
    public async Task<IActionResult> Video([FromQuery] int anime, [FromQuery] int theme, CancellationToken ct = default)
    {
        var source = await service.GetVideoSourceAsync(anime, theme, ct);
        if (source is null)
            return NotFound(new { error = "This song has no video." });

        AnimeThemesVideoStart start;
        try
        {
            start = await media.VideoStartAsync(source, ct);
        }
        catch (AnimeThemesMediaGoneException ex)
        {
            return StatusCode(StatusCodes.Status410Gone, new { error = ex.Message });
        }
        catch (AnimeThemesMediaUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message });
        }

        var whole = start.WholeFileBytes;
        var range = Request.GetTypedHeaders().Range;
        var partial = range is { Unit: var unit, Ranges.Count: 1 } && unit.Equals("bytes", StringComparison.OrdinalIgnoreCase);
        long first = 0, last = whole - 1;
        if (partial)
        {
            var asked = range!.Ranges.First();
            first = asked.From ?? Math.Max(0, whole - (asked.To ?? 0));
            last = asked.From is null ? whole - 1 : Math.Min(asked.To ?? whole - 1, whole - 1);

            if (first >= whole || last < first)
            {
                Response.Headers.ContentRange = $"bytes */{whole}";
                return StatusCode(StatusCodes.Status416RangeNotSatisfiable);
            }

            Response.Headers.ContentRange = $"bytes {first}-{last}/{whole}";
        }

        Response.StatusCode = partial ? StatusCodes.Status206PartialContent : StatusCodes.Status200OK;
        Response.ContentType = "video/webm";
        Response.ContentLength = last - first + 1;
        Response.Headers.AcceptRanges = "bytes";
        Response.Headers.CacheControl = "no-store";

        // A paused video stops reading, and Kestrel would drop a response it took for a slow client.
        var dataRate = HttpContext.Features.Get<IHttpMinResponseDataRateFeature>();
        if (dataRate is not null)
            dataRate.MinDataRate = null;

        try
        {
            var next = first;
            if (next < start.Bytes.Length)
            {
                var end = (int)Math.Min(last + 1, start.Bytes.Length);
                await Response.Body.WriteAsync(start.Bytes.AsMemory((int)next, end - (int)next), ct);
                next = end;
            }

            if (next <= last)
            {
                await using var rest = await relay.OpenVideoAsync(source, next, last, ct);
                await rest.CopyToAsync(Response.Body, ct);
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "A song's video stopped part way through the relay.");
            HttpContext.Abort();
        }
        catch (OperationCanceledException)
        {
            // The video was left: Next song, or the page.
        }

        return new EmptyResult();
    }

    /// <summary>Runs a service call and maps what it throws, which is the same set for every move.</summary>
    private static async Task<IActionResult> MoveAsync<T>(Func<Task<T>> call)
    {
        try
        {
            return new OkObjectResult(await call());
        }
        catch (Exception ex) when (ex is GuessTheThemeRunNotFoundException or GuessTheThemeRoundNotFoundException)
        {
            return new NotFoundObjectResult(new { error = ex.Message });
        }
        catch (Exception ex) when (ex is GuessTheThemeStaleMoveException
                                       or GuessTheThemeWrongPhaseException
                                       or GuessTheThemeFileStillThereException)
        {
            return new ConflictObjectResult(new { error = ex.Message });
        }
        catch (GuessTheThemeAlreadyGuessedException ex)
        {
            return new BadRequestObjectResult(new { error = ex.Message });
        }
        catch (GuessTheThemeCannotCheckException ex)
        {
            return new ObjectResult(new { error = ex.Message }) { StatusCode = StatusCodes.Status502BadGateway };
        }
    }
}
