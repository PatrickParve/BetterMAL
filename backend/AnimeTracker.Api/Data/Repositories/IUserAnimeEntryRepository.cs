using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Data.Repositories;

/// <summary>Read-only access to my list entries. Backs every page read — never
/// call the MAL client on a render path.</summary>
public interface IUserAnimeEntryRepository
{
    Task<UserAnimeEntry?> GetByAnimeIdAsync(int animeId, CancellationToken ct = default);
    Task<List<UserAnimeEntry>> GetAllAsync(CancellationToken ct = default);

    /// <summary>The same entries as <see cref="GetAllAsync"/>, every entry column
    /// filled, but <c>Anime</c> carries only the 11 fields the list and aggregate
    /// pages read (My List, Home, Profile, Recap): Id, Title, EnglishTitle,
    /// PictureUrl, MediaType, TotalEpisodes, AiringStatus, MalScore,
    /// PopularityRank, AiredFrom, AverageEpisodeDurationSeconds. Every other
    /// AnimeMetadata property is left at its default. Never attach these objects
    /// to a DbContext. ListViewReadParityTests fails if one of those pages starts
    /// reading a column this leaves out. Extend the projection and that test's
    /// fixture together. The default implementation returns the full read, so a
    /// test double need not restate it; any implementation backed by a database
    /// MUST override it.</summary>
    Task<List<UserAnimeEntry>> GetAllForListViewAsync(CancellationToken ct = default) => GetAllAsync(ct);

    /// <summary>Pending-and-unheld count, held-for-review count, and the most
    /// recent successful push time across all entries, for the settings
    /// page's sync status display (design.md D14 — PendingCount excludes
    /// held rows so the two figures never conflate "in flight" with "waiting
    /// on me").</summary>
    Task<(int PendingCount, int HeldCount, DateTimeOffset? LastSyncedAt)> GetSyncStatusAsync(CancellationToken ct = default);
}
