using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Metadata;
using AnimeTracker.Api.Services.Series;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Artwork;

public class ArtworkSelectionService(AnimeTrackerDbContext db) : IArtworkSelectionService
{
    public async Task<string?> SetAnimePictureAsync(int animeId, string pictureUrl, CancellationToken ct = default)
    {
        var anime = await db.AnimeMetadata.Include(a => a.UserEntry).FirstOrDefaultAsync(a => a.Id == animeId, ct)
            ?? throw new AnimeMetadataNotFoundException(animeId);

        if (anime.UserEntry is null)
            throw new ArtworkSelectionRejectedException($"Anime {animeId} is not in my list.");

        if (!AnimePicture.Options(anime).Contains(pictureUrl))
            throw new ArtworkSelectionRejectedException($"'{pictureUrl}' is not one of anime {animeId}'s pictures.");

        // Setting MAL's own main picture *is* the clear — it makes PictureUrl
        // equal MalPictureUrl again, which is exactly what "overridden" means
        // the absence of (design.md D2).
        anime.PictureUrl = pictureUrl;
        await db.SaveChangesAsync(ct);
        return anime.PictureUrl;
    }

    public async Task<string?> ResetAnimePictureAsync(int animeId, CancellationToken ct = default)
    {
        var anime = await db.AnimeMetadata.FirstOrDefaultAsync(a => a.Id == animeId, ct)
            ?? throw new AnimeMetadataNotFoundException(animeId);

        anime.PictureUrl = anime.MalPictureUrl;
        await db.SaveChangesAsync(ct);
        return anime.PictureUrl;
    }

    public async Task<string> SetSeriesTitleAsync(int seriesId, string title, CancellationToken ct = default)
    {
        var series = await db.Series.FirstOrDefaultAsync(s => s.Id == seriesId, ct)
            ?? throw new SeriesIdNotFoundException(seriesId);

        var offeredTitles = SeriesTitleRule.OfferedTitles(await MainLineMembersAsync(seriesId, ct));
        if (!SeriesTitleRule.IsAcceptable(title, offeredTitles))
            throw new ArtworkSelectionRejectedException($"'{title}' is not an acceptable title for series {seriesId}.");

        series.SelectedTitle = SeriesTitleRule.Normalize(title);
        await db.SaveChangesAsync(ct);
        return series.SelectedTitle;
    }

    public async Task<string?> ResetSeriesTitleAsync(int seriesId, CancellationToken ct = default)
    {
        var series = await db.Series.FirstOrDefaultAsync(s => s.Id == seriesId, ct)
            ?? throw new SeriesIdNotFoundException(seriesId);

        series.SelectedTitle = null;
        await db.SaveChangesAsync(ct);
        return series.SelectedTitle;
    }

    public async Task<string?> SetSeriesPictureAsync(int seriesId, string pictureUrl, CancellationToken ct = default)
    {
        var series = await db.Series.FirstOrDefaultAsync(s => s.Id == seriesId, ct)
            ?? throw new SeriesIdNotFoundException(seriesId);

        var mainLineMembers = await MainLineMembersAsync(seriesId, ct);
        var options = SeriesPicturePool.Build(mainLineMembers);
        if (series.SelectedPictureUrl is { } current && !options.Contains(current))
            options.Add(current); // a stored choice is never re-validated away (design.md D9)

        if (!options.Contains(pictureUrl))
            throw new ArtworkSelectionRejectedException($"'{pictureUrl}' is not one of series {seriesId}'s pictures.");

        series.SelectedPictureUrl = pictureUrl;
        await db.SaveChangesAsync(ct);
        return series.SelectedPictureUrl;
    }

    public async Task<string?> ResetSeriesPictureAsync(int seriesId, CancellationToken ct = default)
    {
        var series = await db.Series.FirstOrDefaultAsync(s => s.Id == seriesId, ct)
            ?? throw new SeriesIdNotFoundException(seriesId);

        series.SelectedPictureUrl = null;
        await db.SaveChangesAsync(ct);
        return series.SelectedPictureUrl;
    }

    private Task<List<SeriesMember>> MainLineMembersAsync(int seriesId, CancellationToken ct) =>
        db.SeriesMembers
            .Include(m => m.Anime)
            .Where(m => m.SeriesId == seriesId && m.IsMainLine)
            .OrderBy(m => m.Order)
            .ToListAsync(ct);
}
