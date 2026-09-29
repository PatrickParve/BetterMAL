using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Data.Repositories;

/// <summary>Read/write access to stored per-episode airing rows — the single
/// source behind every aired-count, schedule-slot, and next-episode read.</summary>
public interface IEpisodeAiringRepository
{
    /// <summary>The highest episode number whose AirsAtUtc is at/before
    /// asOfUtc, or null when the anime has no aired row.</summary>
    Task<int?> GetMaxAiredEpisodeAsync(int animeId, DateTimeOffset asOfUtc, CancellationToken ct = default);

    /// <summary>The same aired-so-far count as <see
    /// cref="GetMaxAiredEpisodeAsync"/>, for many anime at once in a single
    /// database read. An anime with no stored aired row is absent from the
    /// result rather than present with zero, so "nothing stored" stays
    /// distinguishable from "nothing aired yet". Returns an empty dictionary
    /// without touching the database when animeIds is empty.</summary>
    Task<Dictionary<int, int>> GetMaxAiredEpisodesAsync(IReadOnlyCollection<int> animeIds, DateTimeOffset asOfUtc, CancellationToken ct = default);

    /// <summary>The earliest AirsAtUtc strictly after afterUtc, or null when no
    /// future row is stored.</summary>
    Task<DateTimeOffset?> GetNextAiringInstantAsync(int animeId, DateTimeOffset afterUtc, CancellationToken ct = default);

    /// <summary>The same next-airing-instant read as <see
    /// cref="GetNextAiringInstantAsync(int,DateTimeOffset,CancellationToken)"/>,
    /// for many anime at once in a single database read. An anime with no
    /// stored future row is absent from the result rather than present with a
    /// placeholder instant, so "nothing scheduled" stays distinguishable from
    /// any real instant. Returns an empty dictionary without touching the
    /// database when animeIds is empty.</summary>
    Task<Dictionary<int, DateTimeOffset>> GetNextAiringInstantsAsync(IReadOnlyCollection<int> animeIds, DateTimeOffset afterUtc, CancellationToken ct = default);

    /// <summary>Every stored row for the given anime whose AirsAtUtc falls in
    /// [fromUtc, toUtc), across all of them in one query.</summary>
    Task<List<EpisodeAiring>> GetRowsInRangeAsync(IReadOnlyCollection<int> animeIds, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct = default);

    /// <summary>Replaces this anime's entire stored row set with rows, in one
    /// transaction (the caller's own, when it already holds one: then it is the
    /// caller that commits). A no-op when rows is empty, so a failed or empty
    /// fetch never wipes good data.</summary>
    Task ReplaceForAnimeAsync(int animeId, IReadOnlyList<EpisodeAiring> rows, CancellationToken ct = default);
}
