using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Profile;
using AnimeTracker.Api.Services.Watching;

namespace AnimeTracker.Api.Services.Recap;

/// <summary>The recap's stat block, including hot takes (tasks.md 3.1-3.3).</summary>
public static class RecapStatsBuilder
{
    private const int HotTakeCount = 5;

    public static RecapStatsDto Build(
        List<UserAnimeEntry> included, List<UserAnimeEntry> wholeList, List<UserAnimeEntry> airedIncluded,
        IReadOnlyDictionary<int, int> watchLog, string filter)
    {
        var scored = included.Where(e => e.MyScore is not null).ToList();
        var nonMovies = included.Where(e => !WatchMath.IsMovie(e.Anime)).ToList();

        // D6: a movie counts when its included entry has at least one
        // episode watched — status-agnostic, same as "episodes watched"
        // reading progress rather than status.
        var moviesWatched = included.Count(e => WatchMath.IsMovie(e.Anime) && e.EpisodesWatched > 0);

        // design.md decisions 5/6: under "watched", an entry's figure is its
        // logged in-period episodes when the log reaches it, else the
        // rewatch-inclusive fallback for a pre-tracking completion — the
        // only place a stored rewatch count is read. Under "aired" (and on
        // every season recap, which always resolves to "aired") it's the
        // plain stored count, with no log lookup and no rewatch multiplier.
        var episodesWatched = nonMovies.Sum(e => EntryEpisodeFigure(e, filter, watchLog));

        // D7: runtime uses each anime's cached duration, falling back to the
        // app's one standing assumption — movies included, unlike the
        // episode count above. Computed over the same per-entry figure
        // Episodes watched reports, so dividing one by the other always
        // yields a plausible runtime.
        var timeSpentSeconds = included.Sum(e => (long)EntryEpisodeFigure(e, filter, watchLog) * WatchMath.EpisodeSeconds(e.Anime));

        // D2 (decision 2): counted on the anime's air-start date, from
        // airedIncluded, never from `included`. An in-progress entry has no
        // CompletedAt, so under the "watched" filter it never appears in
        // `included` at all — a filter-scoped count would always read zero
        // for a period whose anime I am demonstrably still watching.
        var currentlyWatching = airedIncluded.Count(e => e.Status == WatchStatus.Watching);

        return new RecapStatsDto(
            MeanScore: scored.Count > 0 ? Math.Round(scored.Average(e => e.MyScore!.Value), 2) : null,
            AnimeCounted: included.Count,
            Completed: included.Count(e => e.Status == WatchStatus.Completed),
            Dropped: included.Count(e => e.Status == WatchStatus.Dropped),
            CurrentlyWatching: currentlyWatching,
            EpisodesWatched: episodesWatched,
            MoviesWatched: moviesWatched,
            TimeSpentSeconds: timeSpentSeconds,
            HotTakes: BuildHotTakes(included, wholeList));
    }

    private static int EntryEpisodeFigure(UserAnimeEntry entry, string filter, IReadOnlyDictionary<int, int> watchLog)
    {
        if (filter != RecapTimeFilter.Watched)
            return entry.EpisodesWatched;

        return watchLog.TryGetValue(entry.AnimeId, out var loggedEpisodes)
            ? loggedEpisodes
            : WatchMath.RewatchInclusiveEpisodes(entry);
    }

    // D4 (polish-recap-page design.md decision 2): a hot take must clear the
    // same divergence-and-label gate the profile page's opinion-divergence
    // lists apply (ScoreDivergence.IsOpinionDivergent), not just be the
    // period's most divergent pick — so the recap and the profile page can
    // never disagree about what counts as a hot take. Ties (equal absolute
    // divergence) break by title for a stable order.
    private static List<RecapHotTakeDto> BuildHotTakes(List<UserAnimeEntry> included, List<UserAnimeEntry> wholeList)
    {
        if (ScoreDivergence.TryCompute(wholeList) is not { } context)
            return [];

        return included
            .Where(e => e.MyScore is not null && e.Anime.MalScore is not null)
            .Select(e => (Entry: e, Divergence: context.DivergenceOf(e)))
            .Where(x => ScoreDivergence.IsOpinionDivergent(x.Entry, x.Divergence))
            .OrderByDescending(x => Math.Abs(x.Divergence))
            .ThenBy(x => x.Entry.Anime.Title, StringComparer.OrdinalIgnoreCase)
            .Take(HotTakeCount)
            .Select(x => new RecapHotTakeDto(
                x.Entry.AnimeId, x.Entry.Anime.Title, x.Entry.Anime.EnglishTitle, x.Entry.Anime.PictureUrl,
                x.Entry.MyScore!.Value, x.Entry.Anime.MalScore!.Value, x.Divergence,
                x.Entry.Status.IsScoreRevealable()))
            .ToList();
    }
}
