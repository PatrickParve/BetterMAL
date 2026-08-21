using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Profile;

namespace AnimeTracker.Api.Services.Watching;

/// <summary>Shared per-entry watch-quantity math (design.md decision 1): how
/// long an episode runs, and how many episodes an entry represents once
/// rewatches are folded in. Used by the profile page's own stats as well as
/// the recap's, which is why this lives outside <c>Services.Recap</c>.</summary>
internal static class WatchMath
{
    private const string MovieMediaType = "movie";
    private const string MusicMediaType = "music";

    public static int EpisodeSeconds(AnimeMetadata anime) =>
        anime.AverageEpisodeDurationSeconds ?? ProfileService.AssumedMinutesPerEpisode * 60;

    /// <summary>An entry's rewatch-inclusive episode count: its current
    /// episodes watched, plus one further complete run of the anime for
    /// every recorded rewatch. The rewatch baseline is the anime's
    /// <em>published total</em>, not the entry's current progress — a
    /// completed rewatch is a full run through the anime regardless of where
    /// EpisodesWatched currently sits, so an entry one episode into its
    /// third viewing of a 12-episode series reads 1 + 2*12 = 25, not 3.
    /// EpisodesWatched is the fallback baseline only when no total is
    /// published, since that's the only length the app knows for a
    /// still-airing or unpublished-length show.</summary>
    public static int RewatchInclusiveEpisodes(UserAnimeEntry entry) =>
        entry.EpisodesWatched + entry.RewatchCount * (entry.Anime.TotalEpisodes ?? entry.EpisodesWatched);

    public static bool IsMovie(AnimeMetadata anime) =>
        string.Equals(anime.MediaType, MovieMediaType, StringComparison.OrdinalIgnoreCase);

    public static bool IsMusic(AnimeMetadata anime) =>
        string.Equals(anime.MediaType, MusicMediaType, StringComparison.OrdinalIgnoreCase);
}
