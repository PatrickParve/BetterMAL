namespace AnimeTracker.Api.Services.Artwork;

/// <summary>Validates and applies picture (and, later, title) choices — the
/// write side of the `artwork-selection` capability. Every set is validated
/// against the anime's own option set (design.md D13); nothing here reaches
/// MyAnimeList.</summary>
public interface IArtworkSelectionService
{
    /// <summary>Sets an anime's displayed picture. Throws
    /// <see cref="Metadata.AnimeMetadataNotFoundException"/> for an unknown
    /// anime id, and <see cref="ArtworkSelectionRejectedException"/> when the
    /// anime has no <c>UserAnimeEntry</c> or <paramref name="pictureUrl"/> is
    /// outside <see cref="AnimePicture.Options"/>. Setting MAL's own main
    /// picture is accepted and clears the override (design.md D2). Returns
    /// the resulting displayed picture.</summary>
    Task<string?> SetAnimePictureAsync(int animeId, string pictureUrl, CancellationToken ct = default);

    /// <summary>Resets an anime's displayed picture to MAL's main picture.
    /// Returns the resulting displayed picture.</summary>
    Task<string?> ResetAnimePictureAsync(int animeId, CancellationToken ct = default);

    /// <summary>Sets or clears an anime's displayed picture, storing
    /// <paramref name="modifiedAt"/> rather than now (device-transfer
    /// design.md D6) — an import adopting another device's choice, not
    /// making one. A non-null <paramref name="pictureUrl"/> runs the same
    /// checks as <see cref="SetAnimePictureAsync"/>; null clears with no
    /// check, as <see cref="ResetAnimePictureAsync"/> does. Returns the
    /// resulting displayed picture.</summary>
    Task<string?> AdoptAnimePictureAsync(int animeId, string? pictureUrl, DateTimeOffset modifiedAt, CancellationToken ct = default);

    /// <summary>Validates <paramref name="pictureUrl"/> against
    /// <see cref="SetAnimePictureAsync"/>'s own checks, writing nothing —
    /// the pre-check an import runs before deciding whether a refresh is
    /// worth retrying (device-transfer design.md D7). Throws the same
    /// exceptions <see cref="SetAnimePictureAsync"/> would.</summary>
    Task CheckAnimePictureAsync(int animeId, string pictureUrl, CancellationToken ct = default);

    /// <summary>Sets a series' chosen title. Throws
    /// <c>SeriesIdNotFoundException</c> for an unknown series id, and
    /// <see cref="ArtworkSelectionRejectedException"/> when <paramref name="title"/>
    /// fails <c>SeriesTitleRule.IsAcceptable</c>. Stores the normalized
    /// candidate (design.md D8). Returns the resulting title.</summary>
    Task<string> SetSeriesTitleAsync(int seriesId, string title, CancellationToken ct = default);

    /// <summary>Clears a series' chosen title, reverting display to its root
    /// member. Returns the resulting title (the root's).</summary>
    Task<string?> ResetSeriesTitleAsync(int seriesId, CancellationToken ct = default);

    /// <summary>Sets or clears a series' chosen title, storing
    /// <paramref name="modifiedAt"/> rather than now (device-transfer
    /// design.md D6). A non-null <paramref name="title"/> runs the same
    /// checks as <see cref="SetSeriesTitleAsync"/>, and is stored normalized;
    /// null clears with no check, as <see cref="ResetSeriesTitleAsync"/>
    /// does. Returns the resulting title.</summary>
    Task<string?> AdoptSeriesTitleAsync(int seriesId, string? title, DateTimeOffset modifiedAt, CancellationToken ct = default);

    /// <summary>Sets a series' chosen picture. Throws
    /// <c>SeriesIdNotFoundException</c> for an unknown series id, and
    /// <see cref="ArtworkSelectionRejectedException"/> when <paramref name="pictureUrl"/>
    /// is outside <see cref="SeriesPicturePool"/>'s pool (plus the series'
    /// current selection). Returns the resulting picture.</summary>
    Task<string?> SetSeriesPictureAsync(int seriesId, string pictureUrl, CancellationToken ct = default);

    /// <summary>Clears a series' chosen picture, reverting display to its
    /// root member's displayed picture. Returns the resulting picture.</summary>
    Task<string?> ResetSeriesPictureAsync(int seriesId, CancellationToken ct = default);

    /// <summary>Sets or clears a series' chosen picture, storing
    /// <paramref name="modifiedAt"/> rather than now (device-transfer
    /// design.md D6). A non-null <paramref name="pictureUrl"/> runs the same
    /// checks as <see cref="SetSeriesPictureAsync"/>; null clears with no
    /// check, as <see cref="ResetSeriesPictureAsync"/> does. Returns the
    /// resulting picture.</summary>
    Task<string?> AdoptSeriesPictureAsync(int seriesId, string? pictureUrl, DateTimeOffset modifiedAt, CancellationToken ct = default);

    /// <summary>Validates <paramref name="pictureUrl"/> against
    /// <see cref="SetSeriesPictureAsync"/>'s own checks, writing nothing —
    /// the pre-check an import runs before deciding whether a refresh is
    /// worth retrying (device-transfer design.md D7). Throws the same
    /// exceptions <see cref="SetSeriesPictureAsync"/> would.</summary>
    Task CheckSeriesPictureAsync(int seriesId, string pictureUrl, CancellationToken ct = default);
}
