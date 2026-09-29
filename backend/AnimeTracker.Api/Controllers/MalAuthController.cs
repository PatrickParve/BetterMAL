using AnimeTracker.Api.Services.Import;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Mal.Auth;
using AnimeTracker.Api.Services.Setup;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AnimeTracker.Api.Controllers;

[ApiController]
public class MalAuthController(
    IMalOAuthService oauthService,
    IMalTokenStore tokenStore,
    IImportTrigger importTrigger,
    ISetupTrigger setupTrigger,
    SetupGate setupGate,
    IOptions<MalOptions> options,
    ILogger<MalAuthController> logger) : ControllerBase
{
    /// <summary>The connection's state — Connected, Lost (MyAnimeList has
    /// refused this login) or NotConnected (design.md D14). The frontend
    /// shows the first-run "Connect to MAL" prompt only for NotConnected.</summary>
    [HttpGet("api/mal-auth/status")]
    public async Task<IActionResult> Status(CancellationToken ct)
    {
        var token = await tokenStore.GetAsync(ct);
        return Ok(new { state = MalConnectionState.Of(token), lostAt = token?.ConnectionLostAt });
    }

    /// <summary>Kicks off the one-time interactive PKCE flow by redirecting
    /// the browser to MAL's authorize page. With a credential missing it
    /// answers 409 naming what is missing instead: the redirect would carry an
    /// empty client id, and the exchange after it could never succeed (design
    /// D5).</summary>
    [HttpGet("api/mal-auth/start")]
    public IActionResult Start()
    {
        var missing = MalCredentials.Missing(options.Value);
        if (missing.Count > 0)
        {
            return Conflict(new
            {
                error = "MyAnimeList credentials are missing.",
                missingCredentials = missing,
            });
        }

        return Redirect(oauthService.BuildAuthorizeUrl());
    }

    /// <summary>OAuth redirect target. Must be served at exactly this path —
    /// it's registered verbatim as the app's MAL redirect URI. It renders no
    /// page of its own: every outcome redirects back into the app (design D6),
    /// to the setup screen at the root while setup is unfinished and to the
    /// Settings page after. A failure carries only a short code — never MAL's
    /// error text or an exception message, which stay in the log.</summary>
    [HttpGet("/callback")]
    public async Task<IActionResult> Callback(
        [FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(error))
        {
            logger.LogWarning("MAL authorization was denied or failed: {Error}", error);
            return ReturnToApp("denied");
        }

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
        {
            logger.LogWarning("MAL OAuth callback arrived without a code or a state.");
            return ReturnToApp("failed");
        }

        MalCallbackResult result;
        try
        {
            result = await oauthService.HandleCallbackAsync(code, state, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to complete MAL OAuth callback.");
            return ReturnToApp("failed");
        }

        if (result is MalCallbackResult.StateRejected)
            return ReturnToApp("expired");

        // Before setup has finished the login starts (or resumes) setup; after
        // it, a re-authorization runs the list import as it always has.
        if (setupGate.IsFinished)
            importTrigger.Signal();
        else
            setupTrigger.Signal();

        return ReturnToApp(null);
    }

    private RedirectResult ReturnToApp(string? connectError)
    {
        var target = $"http://localhost:{options.Value.FrontendPort}{(setupGate.IsFinished ? "/settings" : "/")}";
        if (connectError is not null)
            target += $"?connectError={connectError}";
        return Redirect(target);
    }
}
