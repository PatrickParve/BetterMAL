using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Series;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Setup;

/// <summary>One list entry's anime, with just what setup's counts and orders need.</summary>
internal sealed record LibraryAnime(
    int AnimeId,
    string Title,
    WatchStatus Status,
    DateTimeOffset LastSyncedAt,
    DateTimeOffset? LastRefreshFailedAt,
    string? AiringStatus,
    DateOnly? AiredFrom)
{
    /// <summary>The entry is Watching or Rewatching, which puts the anime first in its tier.</summary>
    public bool IsWatching => Status is WatchStatus.Watching or WatchStatus.Rewatching;

    /// <summary>MyAnimeList answered 404 for its details when the details step asked (or a
    /// scheduled refresh did): the details step's permanent mark. An anime a later refresh
    /// fetches successfully has a sync time again, so it drops out by itself.</summary>
    public bool NotOnMal => LastSyncedAt == default && LastRefreshFailedAt is not null;

    /// <summary>Never fully fetched and not marked missing: what the details step still owes.</summary>
    public bool DetailsLeft => LastSyncedAt == default && LastRefreshFailedAt is null;
}

/// <summary>What setup has done so far, read from the database in one go (design D1): the
/// list's anime, which have an airing-fetched mark, and which are covered by a series. The
/// workers ask it what is left, the Home check asks it whether everything is done and the
/// status read asks it for the counts, so the screen and the work can never disagree.
/// <para>Nothing is held in memory across calls: a series build covers several list anime at
/// once, and a scheduled refresh may fetch one, so the answer is read fresh each time. On a
/// list of hundreds that is a handful of small queries.</para></summary>
internal sealed class SetupLibrary
{
    /// <summary>Every list entry's anime.</summary>
    public required IReadOnlyList<LibraryAnime> Anime { get; init; }

    /// <summary>List anime with an airing-fetched mark: AniList was asked about them, whether
    /// or not it knew them (episode-airing-data).</summary>
    public required IReadOnlySet<int> Marked { get; init; }

    /// <summary>List anime holding a primary membership in a series that is up to date under
    /// the current rules and not partial (design D9).</summary>
    public required IReadOnlySet<int> Covered { get; init; }

    public static async Task<SetupLibrary> LoadAsync(AnimeTrackerDbContext db, CancellationToken ct)
    {
        var anime = await db.UserAnimeEntries.AsNoTracking()
            .Select(e => new LibraryAnime(
                e.AnimeId, e.Anime.Title, e.Status, e.Anime.LastSyncedAt, e.Anime.LastRefreshFailedAt,
                e.Anime.AiringStatus, e.Anime.AiredFrom))
            .ToListAsync(ct);
        var listIds = anime.Select(a => a.AnimeId).ToHashSet();

        // Only list anime are counted: a franchise anime the series step fetched has a mark
        // or a membership of its own that setup doesn't ask about.
        var marked = (await db.AnimeAiringSyncs.AsNoTracking()
                .Where(s => s.LastFetchedAt != null)
                .Select(s => s.AnimeId)
                .ToListAsync(ct))
            .Where(listIds.Contains)
            .ToHashSet();

        var covered = (await db.SeriesMembers.AsNoTracking()
                .Where(m => m.IsPrimary)
                .Join(db.Series.AsNoTracking(), m => m.SeriesId, s => s.Id, (m, s) => new { m.AnimeId, s.BuiltAt, s.IsPartial })
                .Where(x => x.BuiltAt >= SeriesGraphBuilder.ClassificationRevisedAt && !x.IsPartial)
                .Select(x => x.AnimeId)
                .ToListAsync(ct))
            .Where(listIds.Contains)
            .ToHashSet();

        return new SetupLibrary { Anime = anime, Marked = marked, Covered = covered };
    }
}
