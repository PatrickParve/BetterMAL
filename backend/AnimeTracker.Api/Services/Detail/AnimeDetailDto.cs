using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Dashboard;
using AnimeTracker.Api.Services.Entries;
using AnimeTracker.Api.Services.Relations;
using AnimeTracker.Api.Services.Season;
using AnimeTracker.Api.Services.Tmdb;

namespace AnimeTracker.Api.Services.Detail;

/// <summary>Full projection of one anime for the detail page — the only page
/// read that includes the rich detail-only fields (genres, synopsis,
/// background, related anime), since every other page only ever needs the
/// lean listing fields.</summary>
public record AnimeDetailDto(
    int AnimeId,
    string Title,
    string? EnglishTitle,
    string? PictureUrl,
    // Feeds the picture picker (artwork-selection): MAL's own main picture,
    // alongside the displayed PictureUrl above, plus the full picture set.
    string? MalPictureUrl,
    // The stored choice itself (null means none), so the client knows
    // whether there is anything to clear without comparing PictureUrl and
    // MalPictureUrl. No timestamp is exposed here — nothing in the UI reads one.
    string? SelectedPictureUrl,
    List<string>? PictureUrls,
    // Handshake flag: true when this anime is in my list and its picture set
    // has never been fetched, so the client should call the one-anime
    // backfill endpoint (design.md D4b).
    bool PicturesFetchPending,
    // Feeds the picker's TMDB sections (tmdb-artwork): this anime's cached
    // TMDB pictures grouped by scope and language, plus whether its mapping
    // names any TMDB id at all (what the "TMDB has no match" note reads).
    // Null unless the anime is in my list — only such an anime has a picture
    // to choose. Cache only: this read never calls TMDB.
    AnimeTmdbPicturesDto? Tmdb,
    // Handshake flag, the TMDB twin of PicturesFetchPending: true only when a
    // key is configured, the anime is in my list and at least one of its sets
    // is due (never fetched, or last fetched over 30 days ago), so the client
    // should call the one-anime TMDB refresh endpoint. Never true without a
    // key, so a key-less install makes no follow-up request at all.
    bool TmdbFetchPending,
    double? MalScore,
    int? Rank,
    int? PopularityRank,
    string? MediaType,
    string? AiringStatus,
    int? TotalEpisodes,
    int? EpisodesAired,
    DateOnly? AiredFrom,
    DateOnly? AiredTo,
    string? Studio,
    string? Source,
    int? AverageEpisodeDurationSeconds,
    List<string>? Genres,
    string? Synopsis,
    string? Background,
    string? Rating,
    int? SeasonYear,
    string? Season,
    NextEpisodeEtaDto? NextEpisode,
    int? AniListId,
    // The IMDb ids of this anime's id mapping (external-id-mapping), in the
    // mapping's order: [] for an anime with no mapping or none valid. Sent for
    // every anime, in my list or not, because the IMDb link needs no TMDB key.
    List<string> ImdbIds,
    List<RelatedAnimeDto> RelatedAnime,
    UserAnimeEntryDto? Entry,
    // True when a Series row already lists this anime as a member — a plain
    // SeriesMembers PK lookup, no build. RelatedAnime alone can't answer this:
    // a member reached only by a *reverse* edge from another anime (this row
    // itself lean or its relations otherwise thin) has no traversable relation
    // of its own to key a "Series" link off of, even though it already
    // belongs to a built series. InSeries covers that gap for free; the
    // relation-based check (SERIES_TRAVERSAL_RELATIONS on the client) still
    // covers a series that hasn't been built yet at all.
    bool InSeries,
    // True when a visit-triggered live fetch was attempted and failed, so
    // whatever is returned here is served from cache rather than confirmed
    // fresh. Otherwise a failed refresh is indistinguishable from a correct
    // empty relation set (design.md D11) — the client renders a retry
    // affordance instead of treating this as "genuinely no relations".
    bool RefreshFailed,
    // Server-resolved ranked pick for each button — replaces the client's old
    // first-by-array-order logic and includes edges MAL stored only on the
    // other side. Null when no candidate (direct or series-neighbour
    // fallback) exists. RelatedAnime above is unaffected by this.
    ResolvedRelationDto? Prequel,
    ResolvedRelationDto? Sequel,
    ResolvedRelationDto? ParentStory)
{
    public static AnimeDetailDto FromEntity(
        AnimeMetadata anime,
        int? episodesAired,
        NextEpisodeEtaDto? nextEpisode,
        int? aniListId,
        IReadOnlyDictionary<int, RelatedMetadata> relatedMetadataByAnimeId,
        bool inSeries,
        bool refreshFailed,
        RelationResolution relations,
        bool picturesFetchPending,
        AnimeTmdbPicturesDto? tmdb,
        bool tmdbFetchPending,
        List<string> imdbIds)
    {
        (int Year, string Season)? season = anime.AiredFrom is { } airedFrom
            ? SeasonCalendar.GetSeasonFor(airedFrom)
            : null;

        return new(
            anime.Id,
            anime.Title,
            anime.EnglishTitle,
            anime.PictureUrl,
            anime.MalPictureUrl,
            anime.SelectedPictureUrl,
            anime.PictureUrls,
            picturesFetchPending,
            tmdb,
            tmdbFetchPending,
            anime.MalScore,
            anime.Rank,
            anime.PopularityRank,
            anime.MediaType,
            anime.AiringStatus,
            anime.TotalEpisodes,
            episodesAired,
            anime.AiredFrom,
            anime.AiredTo,
            anime.Studio,
            anime.Source,
            anime.AverageEpisodeDurationSeconds,
            anime.Genres,
            anime.Synopsis,
            anime.Background,
            anime.Rating,
            season?.Year,
            season?.Season,
            nextEpisode,
            aniListId,
            imdbIds,
            anime.RelatedAnime.Select(r => RelatedAnimeDto.FromEntity(r, relatedMetadataByAnimeId)).ToList(),
            anime.UserEntry is null ? null : UserAnimeEntryDto.FromEntity(anime.UserEntry),
            inSeries,
            refreshFailed,
            ResolvedRelationDto.FromResolved(relations.Prequel),
            ResolvedRelationDto.FromResolved(relations.Sequel),
            ResolvedRelationDto.FromResolved(relations.ParentStory));
    }
}
