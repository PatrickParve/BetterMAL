using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Entries;

namespace AnimeTracker.Api.Services.Detail;

/// <summary>Full projection of one anime for the detail page — the only page
/// read that includes the rich detail-only fields (genres, synopsis,
/// background, prequel/sequel), since every other page only ever needs the
/// lean listing fields.</summary>
public record AnimeDetailDto(
    int AnimeId,
    string Title,
    string? PictureUrl,
    double? MalScore,
    int? PopularityRank,
    string? MediaType,
    string? AiringStatus,
    int? TotalEpisodes,
    DateOnly? AiredFrom,
    DateOnly? AiredTo,
    string? Studio,
    List<string>? Genres,
    string? Synopsis,
    string? Background,
    int? PrequelMalId,
    string? PrequelTitle,
    int? SequelMalId,
    string? SequelTitle,
    UserAnimeEntryDto? Entry)
{
    public static AnimeDetailDto FromEntity(AnimeMetadata anime) => new(
        anime.Id,
        anime.Title,
        anime.PictureUrl,
        anime.MalScore,
        anime.PopularityRank,
        anime.MediaType,
        anime.AiringStatus,
        anime.TotalEpisodes,
        anime.AiredFrom,
        anime.AiredTo,
        anime.Studio,
        anime.Genres,
        anime.Synopsis,
        anime.Background,
        anime.PrequelMalId,
        anime.PrequelTitle,
        anime.SequelMalId,
        anime.SequelTitle,
        anime.UserEntry is null ? null : UserAnimeEntryDto.FromEntity(anime.UserEntry));
}
