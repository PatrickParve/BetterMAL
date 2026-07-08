using AnimeTracker.Api.Services.Import;
using AnimeTracker.Api.Services.Mal.Auth;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Controllers;

[ApiController]
public class MalAuthController(
    IMalOAuthService oauthService,
    IMalTokenStore tokenStore,
    IImportTrigger importTrigger,
    ILogger<MalAuthController> logger) : ControllerBase
{
    /// <summary>Whether a token is on file — the frontend uses this to decide
    /// whether to show the first-run "Connect to MAL" prompt.</summary>
    [HttpGet("api/mal-auth/status")]
    public async Task<IActionResult> Status(CancellationToken ct)
    {
        var token = await tokenStore.GetAsync(ct);
        return Ok(new { connected = token is not null });
    }

    /// <summary>Kicks off the one-time interactive PKCE flow by redirecting
    /// the browser to MAL's authorize page.</summary>
    [HttpGet("api/mal-auth/start")]
    public IActionResult Start() => Redirect(oauthService.BuildAuthorizeUrl());

    /// <summary>OAuth redirect target. Must be served at exactly this path —
    /// it's registered verbatim as the app's MAL redirect URI.</summary>
    [HttpGet("/callback")]
    public async Task<IActionResult> Callback(
        [FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(error))
        {
            logger.LogWarning("MAL authorization was denied or failed: {Error}", error);
            return HtmlResult("Authorization failed", $"MAL returned an error: {error}. You can close this tab and try again.");
        }

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
            return BadRequest("Missing code or state.");

        try
        {
            await oauthService.HandleCallbackAsync(code, state, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to complete MAL OAuth callback.");
            return HtmlResult("Authorization failed", "Something went wrong completing authorization. Check the backend logs and try again.");
        }

        importTrigger.Signal();
        return HtmlResult("Connected to MyAnimeList", "You can close this tab and return to the app.");
    }

    private ContentResult HtmlResult(string title, string message) => Content(
        $"""
         <!doctype html>
         <html><head><meta charset="utf-8"><title>{title}</title></head>
         <body style="font-family: sans-serif; text-align: center; padding-top: 4rem;">
         <h1>{title}</h1>
         <p>{message}</p>
         </body></html>
         """,
        "text/html");
}
