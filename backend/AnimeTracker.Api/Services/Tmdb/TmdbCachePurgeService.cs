using AnimeTracker.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Tmdb;

/// <summary>Keeps the TMDB image cache inside the time TMDB's API terms allow
/// (design.md D20).</summary>
public interface ITmdbCachePurgeService
{
    /// <summary>Deletes every cached set, with its images, whose last fetch is
    /// older than <see cref="TmdbCachePurgeService.MaxAge"/>, and returns how
    /// many sets that was. Makes no request and needs no key. A removed set is
    /// "never fetched" again, so the next page that needs it fetches it as it
    /// would any new set.</summary>
    Task<int> PurgeExpiredAsync(CancellationToken ct = default);
}

/// <summary>TMDB's API terms forbid keeping what it returns for more than six
/// months. A set is refetched after 30 days only when a page that offers it is
/// opened, so a set nobody opens would otherwise stay for ever. This removes
/// every set, of all three stores, whose last fetch is more than
/// <see cref="MaxAge"/> ago.
///
/// It leaves two things alone on purpose. A picked TMDB picture is not cache:
/// it is stored on the anime or series as the address that was picked
/// (design.md D9), so it keeps showing on every page whatever becomes of the
/// set it came from, and the picker offers it as the current picture. And the
/// id mapping is not TMDB's data (it comes from Fribb's file), so it stays
/// too.
///
/// Whole sets go, tracked and with their images, rather than by a bulk
/// delete: the in-memory test provider has none, and this way a set's images
/// leave with it whatever the foreign key does. The sets are read and then
/// deleted by key, so one refreshed in the moment between the two is removed
/// anyway (one refetch on its next visit) or makes the save fail (logged, and
/// the next tick runs the purge again). A set that old has not been visited
/// for months, so neither is likely.</summary>
public class TmdbCachePurgeService(AnimeTrackerDbContext db, ILogger<TmdbCachePurgeService> logger) : ITmdbCachePurgeService
{
    /// <summary>Five months, so the purge lands well inside the six the terms
    /// allow: room for the app having been stopped for a while, since nothing
    /// is deleted while it is off. It is far above
    /// <see cref="TmdbArtworkService.StaleAfter"/>, so a set that anyone opens
    /// is refreshed long before it can expire.</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromDays(150);

    public async Task<int> PurgeExpiredAsync(CancellationToken ct = default)
    {
        var cutoff = DateTimeOffset.UtcNow - MaxAge;

        var tvSets = await db.TmdbTvImageSets.Include(s => s.Images).Where(s => s.FetchedAt < cutoff).ToListAsync(ct);
        var seasonSets = await db.TmdbSeasonImageSets.Include(s => s.Images).Where(s => s.FetchedAt < cutoff).ToListAsync(ct);
        var movieSets = await db.TmdbMovieImageSets.Include(s => s.Images).Where(s => s.FetchedAt < cutoff).ToListAsync(ct);

        var sets = tvSets.Count + seasonSets.Count + movieSets.Count;
        if (sets == 0)
            return 0;

        var images = tvSets.Sum(s => s.Images.Count) + seasonSets.Sum(s => s.Images.Count) + movieSets.Sum(s => s.Images.Count);

        db.TmdbTvImageSets.RemoveRange(tvSets);
        db.TmdbSeasonImageSets.RemoveRange(seasonSets);
        db.TmdbMovieImageSets.RemoveRange(movieSets);
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Removed {Sets} cached TMDB image sets ({Images} images) last fetched more than {Days} days ago.",
            sets, images, (int)MaxAge.TotalDays);
        return sets;
    }
}
