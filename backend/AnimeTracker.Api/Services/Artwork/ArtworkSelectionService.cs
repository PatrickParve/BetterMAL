using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Metadata;
using AnimeTracker.Api.Services.Search;
using AnimeTracker.Api.Services.Series;
using AnimeTracker.Api.Services.Tmdb;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Artwork;

// These nine methods are the only writers of SelectedPictureModifiedAt
// (anime and series) and SelectedTitleModifiedAt: every set, clear or
// adoption stamps a time at the point of the write, which is what makes
// "stamped when the change is made, never when it is later read, exported
// or rebuilt" true by construction (spec `artwork-selection`). The three
// Adopt* methods are the one exception to "now": adopting another device's
// choice (device-transfer) stores the file's own time, since adopting a
// choice is not making one (design.md D6).
public class ArtworkSelectionService(
    AnimeTrackerDbContext db, IAnimeSearchIndex searchIndex, ITmdbArtworkService tmdbArtwork) : IArtworkSelectionService
{
    public async Task<string?> SetAnimePictureAsync(int animeId, string pictureUrl, CancellationToken ct = default)
    {
        var anime = await ValidateAnimePictureAsync(animeId, pictureUrl, ct);

        anime.SelectedPictureUrl = pictureUrl;
        anime.SelectedPictureModifiedAt = DateTimeOffset.UtcNow;
        anime.ResolvePictureUrl();
        await db.SaveChangesAsync(ct);
        searchIndex.Invalidate();
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
        searchIndex.Invalidate();
        return anime.PictureUrl;
    }

    public async Task<string?> AdoptAnimePictureAsync(int animeId, string? pictureUrl, DateTimeOffset modifiedAt, CancellationToken ct = default)
    {
        var anime = pictureUrl is null
            ? await db.AnimeMetadata.FirstOrDefaultAsync(a => a.Id == animeId, ct) ?? throw new AnimeMetadataNotFoundException(animeId)
            : await ValidateAnimePictureAsync(animeId, pictureUrl, ct);

        anime.SelectedPictureUrl = pictureUrl;
        anime.SelectedPictureModifiedAt = modifiedAt;
        anime.ResolvePictureUrl();
        await db.SaveChangesAsync(ct);
        searchIndex.Invalidate();
        return anime.PictureUrl;
    }

    public Task CheckAnimePictureAsync(int animeId, string pictureUrl, CancellationToken ct = default) =>
        ValidateAnimePictureAsync(animeId, pictureUrl, ct);

    public async Task<string> SetSeriesTitleAsync(int seriesId, string title, CancellationToken ct = default)
    {
        var series = await ValidateSeriesTitleAsync(seriesId, title, ct);

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

    public async Task<string?> AdoptSeriesTitleAsync(int seriesId, string? title, DateTimeOffset modifiedAt, CancellationToken ct = default)
    {
        var series = title is null
            ? await db.Series.FirstOrDefaultAsync(s => s.Id == seriesId, ct) ?? throw new SeriesIdNotFoundException(seriesId)
            : await ValidateSeriesTitleAsync(seriesId, title, ct);

        series.SelectedTitle = title is null ? null : SeriesTitleRule.Normalize(title);
        series.SelectedTitleModifiedAt = modifiedAt;
        await db.SaveChangesAsync(ct);
        return series.SelectedTitle;
    }

    public async Task<string?> SetSeriesPictureAsync(int seriesId, string pictureUrl, CancellationToken ct = default)
    {
        var series = await ValidateSeriesPictureAsync(seriesId, pictureUrl, ct);

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

    public async Task<string?> AdoptSeriesPictureAsync(int seriesId, string? pictureUrl, DateTimeOffset modifiedAt, CancellationToken ct = default)
    {
        var series = pictureUrl is null
            ? await db.Series.FirstOrDefaultAsync(s => s.Id == seriesId, ct) ?? throw new SeriesIdNotFoundException(seriesId)
            : await ValidateSeriesPictureAsync(seriesId, pictureUrl, ct);

        series.SelectedPictureUrl = pictureUrl;
        series.SelectedPictureModifiedAt = modifiedAt;
        await db.SaveChangesAsync(ct);
        return series.SelectedPictureUrl;
    }

    public Task CheckSeriesPictureAsync(int seriesId, string pictureUrl, CancellationToken ct = default) =>
        ValidateSeriesPictureAsync(seriesId, pictureUrl, ct);

    // --- Validation, shared by every set/adopt/check path (design.md D6) ---

    private async Task<AnimeMetadata> ValidateAnimePictureAsync(int animeId, string pictureUrl, CancellationToken ct)
    {
        var anime = await db.AnimeMetadata.Include(a => a.UserEntry).FirstOrDefaultAsync(a => a.Id == animeId, ct)
            ?? throw new AnimeMetadataNotFoundException(animeId);

        if (anime.UserEntry is null)
            throw new ArtworkSelectionRejectedException($"Anime {animeId} is not in my list.");

        if (!await IsAnimeOptionAsync(anime, pictureUrl, ct))
            throw new ArtworkSelectionRejectedException($"'{pictureUrl}' is not one of anime {animeId}'s pictures.");

        return anime;
    }

    /// <summary>The anime's option set (design.md D13): its MyAnimeList
    /// options, the TMDB images of the sets it draws from as cached on this
    /// device, and its current choice, whichever source that came from. The
    /// TMDB half is read from the cache alone, never fetched, so a choice can
    /// only be made from what the picker could show. Only a TMDB-shaped URL can
    /// be in a TMDB set, so a MAL one never costs the extra read.</summary>
    private async Task<bool> IsAnimeOptionAsync(AnimeMetadata anime, string pictureUrl, CancellationToken ct) =>
        AnimePicture.Options(anime).Contains(pictureUrl)
        || anime.SelectedPictureUrl == pictureUrl
        || (TmdbImageUrl.IsTmdbImage(pictureUrl) && (await tmdbArtwork.GetAnimeOptionUrlsAsync(anime.Id, ct)).Contains(pictureUrl));

    private async Task<Models.Series> ValidateSeriesTitleAsync(int seriesId, string title, CancellationToken ct)
    {
        var series = await db.Series.FirstOrDefaultAsync(s => s.Id == seriesId, ct)
            ?? throw new SeriesIdNotFoundException(seriesId);

        var offeredTitles = SeriesTitleRule.OfferedTitles(await MainLineMembersAsync(seriesId, ct));
        if (!SeriesTitleRule.IsAcceptable(title, offeredTitles))
            throw new ArtworkSelectionRejectedException($"'{title}' is not an acceptable title for series {seriesId}.");

        return series;
    }

    private async Task<Models.Series> ValidateSeriesPictureAsync(int seriesId, string pictureUrl, CancellationToken ct)
    {
        var series = await db.Series.FirstOrDefaultAsync(s => s.Id == seriesId, ct)
            ?? throw new SeriesIdNotFoundException(seriesId);

        var mainLineMembers = await MainLineMembersAsync(seriesId, ct);
        var options = SeriesPicturePool.Build(mainLineMembers);
        // The current choice is accepted whichever source it came from, so one
        // that is a TMDB image survives the set dropping it (design.md D9).
        if (series.SelectedPictureUrl is { } current && !options.Contains(current))
            options.Add(current);

        // The MAL pool and the current choice, plus the franchise's cached TMDB
        // images (D13); as for an anime, only a TMDB-shaped URL needs the read.
        var accepted = options.Contains(pictureUrl)
            || (TmdbImageUrl.IsTmdbImage(pictureUrl) && (await tmdbArtwork.GetSeriesOptionUrlsAsync(seriesId, ct)).Contains(pictureUrl));
        if (!accepted)
            throw new ArtworkSelectionRejectedException($"'{pictureUrl}' is not one of series {seriesId}'s pictures.");

        return series;
    }

    private Task<List<SeriesMember>> MainLineMembersAsync(int seriesId, CancellationToken ct) =>
        db.SeriesMembers
            .Include(m => m.Anime)
            .Where(m => m.SeriesId == seriesId && m.IsMainLine)
            .OrderBy(m => m.Order)
            .ToListAsync(ct);
}
