using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Profile;

namespace AnimeTracker.Api.Services.Recap;

/// <summary>The recap's stat block, including hot takes (tasks.md 3.1-3.3).</summary>
public static class RecapStatsBuilder
{
    private const int HotTakeCount = 5;
    private const string MovieMediaType = "movie";

    public static RecapStatsDto Build(List<UserAnimeEntry> included, List<UserAnimeEntry> wholeList)
    {
        var scored = included.Where(e => e.MyScore is not null).ToList();
        var nonMovies = included.Where(e => e.Anime.MediaType != MovieMediaType).ToList();

        // D6: a movie counts when its included entry has at least one
        // episode watched — status-agnostic, same as "episodes watched"
        // reading progress rather than status.
        var moviesWatched = included.Count(e => e.Anime.MediaType == MovieMediaType && e.EpisodesWatched > 0);

        // D7: runtime uses each anime's cached duration, falling back to the
        // app's one standing assumption — movies included, unlike the
        // episode count above.
        var timeSpentSeconds = included.Sum(e => (long)e.EpisodesWatched * RecapTimeMath.EpisodeSeconds(e.Anime));

        return new RecapStatsDto(
            MeanScore: scored.Count > 0 ? Math.Round(scored.Average(e => e.MyScore!.Value), 2) : null,
            AnimeCounted: included.Count,
            Completed: included.Count(e => e.Status == WatchStatus.Completed),
            Dropped: included.Count(e => e.Status == WatchStatus.Dropped),
            EpisodesWatched: nonMovies.Sum(e => e.EpisodesWatched),
            MoviesWatched: moviesWatched,
            TimeSpentSeconds: timeSpentSeconds,
            HotTakes: BuildHotTakes(included, wholeList));
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
