using AnimeTracker.Api.Services.Studios;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Controllers;

[ApiController]
public class StudioController(StudioLinkResolver resolver) : ControllerBase
{
    // MyAnimeList's longest studio name is far shorter. A cap keeps a made-up address from
    // becoming a long database comparison.
    private const int MaxNameLength = 200;

    /// <summary>Where a studio link goes (spec <c>studio-links</c>): a 302 to the studio's
    /// own AniList page when the app can say which studio it is, otherwise to AniList's
    /// search for the name, so the link is never broken. Every studio link in the app is
    /// this one address, whether or not the studio is known yet, so a page is drawn the
    /// same way either way and never waits on AniList. A plain GET navigation, which
    /// <c>CrossSiteRequestGuard</c> leaves alone, and which nginx and the dev server
    /// already pass through under <c>/api/</c>.</summary>
    [HttpGet("api/studios/open")]
    public async Task<IActionResult> Open([FromQuery] string? name, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > MaxNameLength)
            return BadRequest(new { error = "A studio name is required." });

        var aniListStudioId = await resolver.ResolveAsync(name, ct);

        // What a link leads to changes as the app learns, so no one is told to keep it.
        Response.Headers.CacheControl = "no-store";

        return Redirect(aniListStudioId is { } id
            ? $"https://anilist.co/studio/{id}"
            : $"https://anilist.co/search/studios?search={Uri.EscapeDataString(name)}");
    }
}
