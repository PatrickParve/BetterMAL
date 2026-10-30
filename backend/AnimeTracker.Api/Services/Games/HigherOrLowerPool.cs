using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Games;

/// <summary>Which stored anime a Higher or Lower game can show
/// (add-games-higher-or-lower design.md D4). <see cref="Query"/> is the one
/// definition behind the count, the start and every pick, so what the setup
/// screen counts is exactly what a game draws from.</summary>
public static class HigherOrLowerPool
{
    // MAL's rating for Hentai, as the season read filters on it.
    internal const string HentaiRating = "rx";

    private const string NotYetAired = "not_yet_aired";

    /// <summary>Every anime with a MAL score, a picture (a title is required on the
    /// model) and an episode that has aired by <paramref name="nowUtc"/> that the
    /// settings allow. Not tracked, so callers project it.
    ///
    /// "Has aired" is <see cref="Entries.AiredEpisodeGate.HasAired"/>'s rule written
    /// as a predicate, so the game and the rest of the app agree: a stored aired
    /// episode settles it, and otherwise only a status of <c>not_yet_aired</c> says
    /// no. A row with no status at all passes, as it must: listing-only rows carry
    /// none, and they are most of the pool. An anime on my list is held to it too, so
    /// a Plan to watch show that hasn't premiered never appears.
    ///
    /// The year bounds are whole-year <see cref="DateOnly"/> comparisons on
    /// AiredFrom, which both Npgsql and the in-memory provider translate; with
    /// either bound set an anime with no start date doesn't match, because its
    /// year isn't known. Any list status counts as on my list. A null rating
    /// passes Hide NSFW, as it does in the season read: only a confirmed rx is
    /// left out.</summary>
    public static IQueryable<AnimeMetadata> Query(
        AnimeTrackerDbContext db, HigherOrLowerSettings settings, DateTimeOffset nowUtc)
    {
        var query = db.AnimeMetadata.AsNoTracking()
            .Where(a => a.MalScore > 0 && a.PictureUrl != null)
            .Where(a => a.AiringStatus != NotYetAired
                || db.EpisodeAirings.Any(e => e.AnimeId == a.Id && e.AirsAtUtc <= nowUtc));

        if (settings.YearFrom is { } yearFrom)
        {
            var from = new DateOnly(yearFrom, 1, 1);
            query = query.Where(a => a.AiredFrom >= from);
        }

        if (settings.YearTo is { } yearTo)
        {
            var to = new DateOnly(yearTo, 12, 31);
            query = query.Where(a => a.AiredFrom <= to);
        }

        if (settings.MediaTypes is { Count: > 0 } mediaTypes)
        {
            var types = mediaTypes.ToArray();
            query = query.Where(a => a.MediaType != null && types.Contains(a.MediaType));
        }

        query = settings.List switch
        {
            HigherOrLowerListScope.OnlyMyList => query.Where(a => a.UserEntry != null),
            HigherOrLowerListScope.NotOnMyList => query.Where(a => a.UserEntry == null),
            _ => query,
        };

        if (settings.HideHentai)
            query = query.Where(a => a.Rating != HentaiRating);

        return query;
    }

    /// <summary>The four columns a draw needs, rather than whole rows.</summary>
    public static IQueryable<PickCandidate> ToCandidates(this IQueryable<AnimeMetadata> query) =>
        query.Select(a => new PickCandidate(a.Id, a.MalScore!.Value, a.PopularityRank, a.UserEntry != null));

    /// <summary>Playable means at least two different scores at the two decimals
    /// shown: with fewer, no first pair can be drawn.</summary>
    public static bool IsPlayable(IEnumerable<double> scores)
    {
        long? first = null;
        foreach (var score in scores)
        {
            var hundredths = HigherOrLowerPicker.Hundredths(score);
            if (first is null)
                first = hundredths;
            else if (hundredths != first)
                return true;
        }

        return false;
    }
}
