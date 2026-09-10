using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Services.Artwork;
using AnimeTracker.Api.Services.Detail;
using AnimeTracker.Api.Services.Metadata;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Controllers;

[ApiController]
public class AnimeDetailController(
    IAnimeDetailService detailService,
    IArtworkSelectionService artworkSelectionService,
    IPictureRefreshService pictureRefreshService,
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
}

public class SetPictureRequest
{
    public required string PictureUrl { get; set; }
}
