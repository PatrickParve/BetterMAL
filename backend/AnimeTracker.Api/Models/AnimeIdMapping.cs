namespace AnimeTracker.Api.Models;

/// <summary>One MyAnimeList anime's ids on TMDB and IMDb, copied from the
/// third-party Fribb mapping file by AnimeIdMappingSyncService (spec
/// `external-id-mapping`). Keyed on the MAL id, which the sync supplies and
/// the database never generates. Deliberately no FK to
/// <see cref="AnimeMetadata"/>, as with <see cref="AnimeRelatedAnime.RelatedAnimeId"/>:
/// most mapped ids belong to anime that are never cached, and requiring the
/// row to exist would fail the whole sync. A MAL id with no row has no
/// mapping.</summary>
public class AnimeIdMapping
{
    public int AnimeId { get; set; } // MAL id
    public int? TmdbTvId { get; set; }

    /// <summary>The TMDB season this entry corresponds to, stored as given
    /// (0, TMDB's "Specials" bucket, included) and only ever alongside
    /// <see cref="TmdbTvId"/>. Skipping season 0 is decided once, at read
    /// time (design.md D7), so the stored mapping stays a faithful copy.</summary>
    public int? TmdbSeasonNumber { get; set; }

    public List<int> TmdbMovieIds { get; set; } = [];

    /// <summary>Only `tt` followed by digits, in the file's order and without
    /// duplicates. Usually one id, rarely several.</summary>
    public List<string> ImdbIds { get; set; } = [];
}
