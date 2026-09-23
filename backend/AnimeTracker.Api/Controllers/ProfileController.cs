using AnimeTracker.Api.Services.Profile;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Controllers;

[ApiController]
public class ProfileController(IProfileService profileService) : ControllerBase
{
    /// <summary>Profile page data: anime stats, recent activity, my top anime
    /// (with tie-break candidates), score distribution, and opinion-divergence
    /// lists — all computed from cached Postgres data.</summary>
    [HttpGet("api/profile")]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var profile = await profileService.GetProfileAsync(ct);
        return Ok(profile);
    }

    /// <summary>Full edit history, most recent first — backs the "Latest
    /// updates" box's history overlay.</summary>
    [HttpGet("api/profile/activity")]
    public async Task<IActionResult> GetActivityHistory(CancellationToken ct)
    {
        var history = await profileService.GetActivityHistoryAsync(ct);
        return Ok(history);
    }

    /// <summary>"My top anime" recomputed over one media-type scope (or "all")
    /// for the profile page's filter tabs.</summary>
    [HttpGet("api/profile/top-anime")]
    public async Task<IActionResult> GetTopAnime([FromQuery] string? mediaType, CancellationToken ct)
    {
        if (!TopAnimeMediaTypeScope.IsValid(mediaType))
            return BadRequest(new { error = $"Unknown mediaType: {mediaType}" });

        var section = await profileService.GetTopAnimeSectionAsync(mediaType, ct);
        return Ok(section);
    }

    /// <summary>"Most rewatched" recomputed over one media-type scope (or
    /// "all") for the profile page's filter tabs.</summary>
    [HttpGet("api/profile/rewatched")]
    public async Task<IActionResult> GetRewatched([FromQuery] string? mediaType, CancellationToken ct)
    {
        if (!TopAnimeMediaTypeScope.IsValid(mediaType))
            return BadRequest(new { error = $"Unknown mediaType: {mediaType}" });

        var section = await profileService.GetRewatchedSectionAsync(mediaType, ct);
        return Ok(section);
    }

    /// <summary>"Top series": every franchise with a member in my list,
    /// ranked by both its MAL main-series average and my main-series average
    /// in one payload (design.md decision 4) — the client re-sorts/filters
    /// the same array locally when the ranking basis is switched rather than
    /// this endpoint being called again. Deliberately not embedded in <see
    /// cref="Get"/>: unlike TopAnime/Rewatched, which just recompute over the
    /// entries that call already loads, this section runs its own
    /// SeriesMembers ⋈ AnimeMetadata join and enqueues background series
    /// builds (design.md decision 6/task 3.5) — costs that shouldn't land on
    /// every profile visit when the section may not even be scrolled
    /// to.</summary>
    [HttpGet("api/profile/top-series")]
    public async Task<IActionResult> GetTopSeries(CancellationToken ct)
    {
        var section = await profileService.GetTopSeriesSectionAsync(ct);
        return Ok(section);
    }

    /// <summary>"Most rewatched"'s Series scope: every franchise with
    /// above-zero total rewatch time, ranked by that total descending
    /// (design.md D9). Deliberately not embedded in <see cref="Get"/>, for
    /// the same reason as <see cref="GetTopSeries"/> above: it runs its own
    /// SeriesMembers ⋈ AnimeMetadata join, which shouldn't land on every
    /// profile visit for a scope that may never be selected. Unlike
    /// top-series it does not enqueue background series builds — the same
    /// page's Top series read already does (design.md D10).</summary>
    [HttpGet("api/profile/rewatched-series")]
    public async Task<IActionResult> GetRewatchedSeries(CancellationToken ct)
    {
        var section = await profileService.GetRewatchedSeriesSectionAsync(ct);
        return Ok(section);
    }

    /// <summary>"Most time spent": every franchise with at least one
    /// main-line member watched, ranked by total watch time descending
    /// (time-spent-sort-and-main-line-gate design.md D1/D2). Deliberately not
    /// embedded in <see cref="Get"/>, for the same reason as <see
    /// cref="GetTopSeries"/>: it pays for its own <c>SeriesRankingLookup</c>
    /// load rather than being folded into <c>top-series</c>, which is
    /// accepted because the lookup short-circuits on a cold install and the
    /// repo already runs this shape of query per keystroke in
    /// <c>SeriesSearchLookup</c> (design.md D6). Like rewatched-series it does
    /// not enqueue background series builds — the same page's Top series
    /// read already does.</summary>
    [HttpGet("api/profile/time-spent-series")]
    public async Task<IActionResult> GetTimeSpentSeries(CancellationToken ct)
    {
        var section = await profileService.GetTimeSpentSeriesSectionAsync(ct);
        return Ok(section);
    }
}
