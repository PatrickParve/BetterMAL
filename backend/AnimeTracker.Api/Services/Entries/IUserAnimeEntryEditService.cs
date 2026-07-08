namespace AnimeTracker.Api.Services.Entries;

public interface IUserAnimeEntryEditService
{
    Task<UserAnimeEntryDto> UpdateEntryAsync(int animeId, UserAnimeEntryEditRequest request, CancellationToken ct = default);
}
