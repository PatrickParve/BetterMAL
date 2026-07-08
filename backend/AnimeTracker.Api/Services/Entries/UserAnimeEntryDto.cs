using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Services.Entries;

/// <summary>Flat projection of UserAnimeEntry for API responses — avoids
/// serializing the tracked entity directly, whose Anime/UserEntry navigation
/// properties form a reference cycle.</summary>
public record UserAnimeEntryDto(
    int AnimeId,
    WatchStatus Status,
    int EpisodesWatched,
    int? MyScore,
    DateOnly? StartedAt,
    DateOnly? CompletedAt,
    int RewatchCount,
    bool PendingSync,
    DateTimeOffset? LastSyncedAt)
{
    public static UserAnimeEntryDto FromEntity(UserAnimeEntry entry) => new(
        entry.AnimeId,
        entry.Status,
        entry.EpisodesWatched,
        entry.MyScore,
        entry.StartedAt,
        entry.CompletedAt,
        entry.RewatchCount,
        entry.PendingSync,
        entry.LastSyncedAt);
}
