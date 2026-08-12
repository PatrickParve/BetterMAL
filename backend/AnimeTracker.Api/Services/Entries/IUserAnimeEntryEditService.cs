namespace AnimeTracker.Api.Services.Entries;

public interface IUserAnimeEntryEditService
{
    Task<UserAnimeEntryDto> UpdateEntryAsync(int animeId, UserAnimeEntryEditRequest request, CancellationToken ct = default);

    /// <summary>Removes the anime from my list: deletes the UserAnimeEntry,
    /// logs the removal, and queues its durable MAL removal. Throws
    /// EntryNotFoundException if the anime isn't in my list.</summary>
    Task RemoveEntryAsync(int animeId, CancellationToken ct = default);
}
