using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Dashboard;
using AnimeTracker.Api.Services.Entries;
using AnimeTracker.Api.Services.Season;

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
    List<RelatedAnimeDto> RelatedAnime,
    UserAnimeEntryDto? Entry)
{
    public static AnimeDetailDto FromEntity(
        AnimeMetadata anime,
        int? episodesAired,
        NextEpisodeEtaDto? nextEpisode,
        int? aniListId,
        IReadOnlyDictionary<int, string?> relatedMediaTypeByAnimeId)
    {
        (int Year, string Season)? season = anime.AiredFrom is { } airedFrom
            ? SeasonCalendar.GetSeasonFor(airedFrom)
            : null;

        return new(
            anime.Id,
            anime.Title,
            anime.EnglishTitle,
            anime.PictureUrl,
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
            anime.RelatedAnime.Select(r => RelatedAnimeDto.FromEntity(r, relatedMediaTypeByAnimeId)).ToList(),
            anime.UserEntry is null ? null : UserAnimeEntryDto.FromEntity(anime.UserEntry));
    }
}
