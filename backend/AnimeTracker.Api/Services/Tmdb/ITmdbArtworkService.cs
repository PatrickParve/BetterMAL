namespace AnimeTracker.Api.Services.Tmdb;

/// <summary>TMDB images as the rest of the app sees them (spec
/// `tmdb-artwork`, design.md D10). It has two halves. The <b>read side</b>
/// serves whatever is cached and never makes a network call, so a page's own
/// read never waits on TMDB. The <b>fetch side</b> is what a page's follow-up
/// request triggers: it asks TMDB for the sets that are due, and is a no-op
/// while no API key is configured.
///
/// Sets are keyed by TMDB identity (<see cref="TmdbSetKey"/>), never by MAL id,
/// so every anime that maps to a set shares it and a set is fetched once for
/// all of them. A set is <i>due</i> when it was never fetched or was last
/// fetched more than 30 days ago; a fetch that returns nothing is stored as
/// fetched and empty, which is not the same as never fetched.</summary>
public interface ITmdbArtworkService
{
    /// <summary>An anime's cached TMDB pictures, grouped by scope and language
    /// as its picker shows them. Null unless the anime is in my list: only
    /// such an anime has a picture to choose (spec `tmdb-artwork` "An anime's
    /// TMDB sets are fetched only while it is in my list"). Offers cached
    /// images whether or not a key is configured now.</summary>
    Task<AnimeTmdbPicturesDto?> GetAnimePicturesAsync(int animeId, CancellationToken ct = default);

    /// <summary>A series' cached TMDB pictures, grouped by language only, plus
    /// how many of its franchise's sets are due. The series' members are read
    /// here rather than passed in, so the pictures shown, the options
    /// validated (<see cref="GetSeriesOptionUrlsAsync"/>) and the sets fetched
    /// (<see cref="RefreshSeriesAsync"/>) are all worked out from one place.
    /// Not limited to my list: a series' picture can be chosen for any
    /// series.</summary>
    Task<SeriesTmdbPicturesDto> GetSeriesPicturesAsync(int seriesId, CancellationToken ct = default);

    /// <summary>Whether a follow-up fetch is worth making for this anime: a
    /// key is configured, the anime is in my list, and at least one of its
    /// sets is due. This is the detail response's <c>tmdbFetchPending</c>
    /// flag.</summary>
    Task<bool> IsAnimeFetchDueAsync(int animeId, CancellationToken ct = default);

    /// <summary>Every URL among the anime's cached TMDB pictures — the ones a
    /// choice for it may be validated against (design.md D13). Cache only, and
    /// built from the same groups the picker shows, so what is offered and what
    /// is accepted cannot drift. Says nothing about my list: that check
    /// belongs to the caller.</summary>
    Task<IReadOnlyList<string>> GetAnimeOptionUrlsAsync(int animeId, CancellationToken ct = default);

    /// <summary>Every URL among the series' cached TMDB pictures, for
    /// validating a series choice against.</summary>
    Task<IReadOnlyList<string>> GetSeriesOptionUrlsAsync(int seriesId, CancellationToken ct = default);

    /// <summary>Fetches the anime's due sets from TMDB, or every one of them
    /// when <paramref name="force"/> is set ("Refresh data" and
    /// device-transfer, design.md D14, D15). A no-op when no key is configured
    /// or the anime is not in my list. A failed fetch leaves its set as it was
    /// and never throws: TMDB trouble must not fail the page or action the
    /// fetch belongs to.</summary>
    Task RefreshAnimeAsync(int animeId, bool force, CancellationToken ct = default);

    /// <summary>Fetches up to <paramref name="budget"/> of the series'
    /// franchise sets that are due — or, when <paramref name="force"/> is set,
    /// of all of them — in key order: series sets, then seasons, then movies,
    /// so the most representative art arrives first. Returns how many remain
    /// due, which the next visit picks up. Not limited to my list. A no-op,
    /// returning 0, when no key is configured.</summary>
    Task<int> RefreshSeriesAsync(int seriesId, int budget, bool force, CancellationToken ct = default);
}
