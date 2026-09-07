namespace AnimeTracker.Api.Services.Series;

/// <summary>The Series page's card-scale progress badge (add-series-browser
/// design.md D5; six-state precedence widened by
/// polish-series-badges-and-filters design.md D1), mirroring
/// `SeriesPage.tsx`'s client-side `completionBadge` precedence — the two
/// must be changed together. <see cref="Completed"/> is the whole series
/// finished and watched; <see cref="Dropped"/> is the most-recently-aired
/// main-line entry marked Dropped with nothing watched after it;
/// <see cref="CaughtUp"/>/<see cref="Behind"/> compare total watched against
/// total broadcast across the whole aired main line, not just a
/// currently-airing entry; <see cref="Unwatched"/> is something aired but
/// nothing watched, when not already <see cref="Dropped"/>.
/// `BehindEpisodes` is populated only when the value is <see
/// cref="Behind"/>.</summary>
public enum SeriesProgressBadge
{
    None,
    Completed,
    CaughtUp,
    Behind,
    Dropped,
    Unwatched,
}

/// <summary>One series' worth of Series-page card figures (add-series-browser
/// design.md D2/D7), computed at read time from stored members — never
/// cached. `MainLineEpisodeTotal`/`HasUnknownEpisodeCounts` are scoped to the
/// main line only, matching the series page's own episode-total stat and the
/// card's main-line averages, so every episode figure on the card describes
/// the same member set; `EntryCount` is the opposite scope on purpose —
/// every member, extras included, matching `SeriesBadge` everywhere else in
/// the app. `MainLineWatchedEpisodes`/`MainLineAiredEpisodes` are the two
/// figures the client's "my progress" sort divides. `MainLineAiredCount` is
/// main-line members that have started airing — the same figure Top series
/// filters on — and `MainLineAverageRank` is the mean overall ranking
/// position of the main-line members my rankings cover, `null` when they
/// cover none.</summary>
// SeriesId is the root entry's MAL id (key-series-by-root-anime-id
// design.md D1/D7), so the card's link target reads this same field.
public record SeriesListItemDto(
    int SeriesId,
    string Title,
    string? EnglishTitle,
    string? PictureUrl,
    string Status,
    SeriesProgressBadge ProgressBadge,
    int? BehindEpisodes,
    SeriesAverageDto MalMain,
    SeriesAverageDto MineMain,
    bool MalRevealed,
    int? FirstYear,
    int? LastYear,
    int MainLineEpisodeTotal,
    bool HasUnknownEpisodeCounts,
    int EntryCount,
    int MainLineWatchedEpisodes,
    int MainLineAiredEpisodes,
    int MainLineAiredCount,
    double? MainLineAverageRank);

/// <summary>Every series eligible for the Series page (add-series-browser
/// design.md D1), in the endpoint's deterministic default order — my
/// main-line average descending, nulls last, then raw title
/// case-insensitively.</summary>
public record SeriesListDto(List<SeriesListItemDto> Items);
