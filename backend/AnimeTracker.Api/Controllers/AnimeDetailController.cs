using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Services.Artwork;
using AnimeTracker.Api.Services.Detail;
using AnimeTracker.Api.Services.Metadata;
using AnimeTracker.Api.Services.Tmdb;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Controllers;

[ApiController]
public class AnimeDetailController(
    IAnimeDetailService detailService,
    IArtworkSelectionService artworkSelectionService,
    IPictureRefreshService pictureRefreshService,
    ITmdbArtworkService tmdbArtwork,
    IAnimeMetadataRepository metadataRepository) : ControllerBase
{
    /// <summary>Single anime detail page read — picture, title, scores, my
    /// entry, info fields, synopsis/background, and prequel/sequel links.</summary>
    [HttpGet("api/anime/{animeId:int}")]
    public async Task<IActionResult> GetDetail(int animeId, CancellationToken ct)
    {
        try
        {
            var result = await detailService.GetDetailAsync(animeId, ct);
            return Ok(result);
        }
        catch (AnimeMetadataNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPut("api/anime/{animeId:int}/picture")]
    public async Task<IActionResult> SetPicture(int animeId, [FromBody] SetPictureRequest request, CancellationToken ct)
    {
        try
        {
            var pictureUrl = await artworkSelectionService.SetAnimePictureAsync(animeId, request.PictureUrl, ct);
            return Ok(new { pictureUrl, selectedPictureUrl = pictureUrl });
        }
        catch (AnimeMetadataNotFoundException)
        {
            return NotFound();
        }
        catch (ArtworkSelectionRejectedException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("api/anime/{animeId:int}/picture")]
    public async Task<IActionResult> ResetPicture(int animeId, CancellationToken ct)
    {
        try
        {
            var pictureUrl = await artworkSelectionService.ResetAnimePictureAsync(animeId, ct);
            return Ok(new { pictureUrl, selectedPictureUrl = (string?)null });
        }
        catch (AnimeMetadataNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>One-anime picture backfill (design.md D4b) — no-ops when the
    /// anime isn't in my list or already has a picture set. Always returns
    /// the anime's current option set and selection, whether or not a fetch
    /// actually ran.</summary>
    [HttpPost("api/anime/{animeId:int}/pictures/refresh")]
    public async Task<IActionResult> RefreshPictures(int animeId, CancellationToken ct)
    {
        await pictureRefreshService.RefreshOneAsync(animeId, ct: ct);

        var anime = await metadataRepository.GetByIdAsync(animeId, ct);
        if (anime is null)
            return NotFound();

        return Ok(new { pictureUrl = anime.PictureUrl, selectedPictureUrl = anime.SelectedPictureUrl, pictureUrls = AnimePicture.Options(anime) });
    }

    /// <summary>One-anime TMDB follow-up fetch (design.md D10, D11) — fetches
    /// the anime's due TMDB sets, then returns its TMDB pictures and whether
    /// any set is still due, so the page can merge them without a reload.
    /// 404 when the anime isn't cached. An anime not in my list has no TMDB
    /// pictures (tmdb-artwork), so it gets <c>{ tmdb: null, tmdbFetchPending:
    /// false }</c> without a fetch. With no key configured nothing is fetched
    /// and nothing reads as pending, but images cached earlier are still
    /// returned. TMDB trouble never fails the action: a set that could not be
    /// fetched simply stays due, which the returned flag says.</summary>
    [HttpPost("api/anime/{animeId:int}/tmdb-pictures/refresh")]
    public async Task<IActionResult> RefreshTmdbPictures(int animeId, CancellationToken ct)
    {
        var anime = await metadataRepository.GetByIdAsync(animeId, ct);
        if (anime is null)
            return NotFound();

        if (anime.UserEntry is null)
            return Ok(new { tmdb = (AnimeTmdbPicturesDto?)null, tmdbFetchPending = false });

        await tmdbArtwork.RefreshAnimeAsync(animeId, force: false, ct);

        return Ok(new
        {
            tmdb = await tmdbArtwork.GetAnimePicturesAsync(animeId, ct),
            tmdbFetchPending = await tmdbArtwork.IsAnimeFetchDueAsync(animeId, ct),
        });
    }
}

public class SetPictureRequest
{
    public required string PictureUrl { get; set; }
}
