using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Metadata;
using AnimeTracker.Api.Services.Series;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Artwork;

// These six methods are the only writers of SelectedPictureModifiedAt
// (anime and series) and SelectedTitleModifiedAt: every set or clear stamps
// UtcNow at the point of the write, which is what makes "stamped when the
// change is made, never when it is later read, exported or rebuilt" true by
// construction (spec `artwork-selection`).
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

        anime.SelectedPictureUrl = pictureUrl;
        anime.SelectedPictureModifiedAt = DateTimeOffset.UtcNow;
        anime.ResolvePictureUrl();
        await db.SaveChangesAsync(ct);
        return anime.PictureUrl;
    }

    public async Task<string?> ResetAnimePictureAsync(int animeId, CancellationToken ct = default)
    {
        var anime = await db.AnimeMetadata.FirstOrDefaultAsync(a => a.Id == animeId, ct)
            ?? throw new AnimeMetadataNotFoundException(animeId);

        // Clearing is now the only way to remove a choice. It stamps because
        // a clear must be able to outrank an earlier set made on another
        // device.
        anime.SelectedPictureUrl = null;
        anime.SelectedPictureModifiedAt = DateTimeOffset.UtcNow;
        anime.ResolvePictureUrl();
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
        series.SelectedTitleModifiedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return series.SelectedTitle;
    }

    public async Task<string?> ResetSeriesTitleAsync(int seriesId, CancellationToken ct = default)
    {
        var series = await db.Series.FirstOrDefaultAsync(s => s.Id == seriesId, ct)
            ?? throw new SeriesIdNotFoundException(seriesId);

        series.SelectedTitle = null;
        series.SelectedTitleModifiedAt = DateTimeOffset.UtcNow;
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
        series.SelectedPictureModifiedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return series.SelectedPictureUrl;
    }

    public async Task<string?> ResetSeriesPictureAsync(int seriesId, CancellationToken ct = default)
    {
        var series = await db.Series.FirstOrDefaultAsync(s => s.Id == seriesId, ct)
            ?? throw new SeriesIdNotFoundException(seriesId);

        series.SelectedPictureUrl = null;
        series.SelectedPictureModifiedAt = DateTimeOffset.UtcNow;
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
