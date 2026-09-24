using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Services.Tmdb;

/// <summary>One cached TMDB image as a picker option (design.md D11). The
/// cache stores only TMDB's file path; <c>Url</c> is the full <c>original</c>
/// URL, built on read by <see cref="TmdbImageUrl"/>, and is also exactly what
/// a choice stores in <c>SelectedPictureUrl</c> (D9). <c>Width</c> and
/// <c>Height</c> are TMDB's own, so the page can reserve a backdrop's
/// landscape footprint before it decodes rather than guess.</summary>
public record TmdbPictureDto(string Url, TmdbImageKind Kind, int Width, int Height);

/// <summary>One language's images within a scope (or, on a series, within the
/// whole franchise). <c>Language</c> is "none" for an image with no language,
/// otherwise "ja" or "en". Posters come first, then backdrops, each in the
/// order TMDB lists them, and an empty group is never sent.</summary>
public record TmdbLanguageGroupDto(string Language, List<TmdbPictureDto> Pictures);

/// <summary>One scope of an anime's TMDB images: its show's series-level
/// images, its season's posters, or its movie(s). <c>SeasonNumber</c> is
/// TMDB's own season number, set for a Season scope only — it can differ from
/// MAL's split (Re:Zero's seasons 2, 2-part-2 and 3 are all TMDB season 1), so
/// the picker names it as TMDB numbers it. An empty scope is never sent.</summary>
public record TmdbScopeDto(TmdbScope Scope, int? SeasonNumber, List<TmdbLanguageGroupDto> Languages);

/// <summary>An anime's cached TMDB pictures, in the order the picker lists
/// them: Series, then Season, then Movie. <c>HasMapping</c> is whether the
/// anime's mapping names any TMDB id at all — false for no mapping row and for
/// a row that holds only IMDb ids — which is what tells "TMDB has no match"
/// from "nothing cached yet". <c>Configured</c> is whether a TMDB API key is
/// set. Cached images are offered with or without one, so this DTO exists
/// either way and this flag is the client's only way to tell "TMDB has no
/// match" from "TMDB is off", where the picker says nothing about TMDB.</summary>
public record AnimeTmdbPicturesDto(bool Configured, bool HasMapping, List<TmdbScopeDto> Scopes);

/// <summary>A series' cached TMDB pictures for its picker: the images of the
/// whole franchise, grouped by language only, with the series, season and
/// movie images mixed within each language group (design.md D8, D12).
/// <c>HasMapping</c> is whether any member's mapping names a TMDB id.
/// <c>Configured</c> is whether a TMDB API key is set, and has the same job as
/// on <see cref="AnimeTmdbPicturesDto"/>. <c>PendingCount</c> is how many of
/// the franchise's sets are due — never fetched, or last fetched more than 30
/// days ago — and 0 while no API key is configured.</summary>
public record SeriesTmdbPicturesDto(bool Configured, bool HasMapping, List<TmdbLanguageGroupDto> Languages, int PendingCount);
