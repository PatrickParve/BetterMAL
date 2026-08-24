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

    /// <summary>Primitive form, shared with callers (e.g. the series ranking
    /// projection) that don't carry a full AnimeMetadata row — so nothing
    /// restates the 24-minute assumed duration independently and risks
    /// disagreeing with this one.</summary>
    public static int EpisodeSeconds(int? averageEpisodeDurationSeconds) =>
        averageEpisodeDurationSeconds ?? ProfileService.AssumedMinutesPerEpisode * 60;

    public static int EpisodeSeconds(AnimeMetadata anime) => EpisodeSeconds(anime.AverageEpisodeDurationSeconds);

    /// <summary>The episodes a rewatch alone represents: one further complete
    /// run of the anime for every recorded rewatch, with no first viewing
    /// counted. The rewatch baseline is the anime's <em>published total</em>,
    /// not the current progress — a completed rewatch is a full run through
    /// the anime regardless of where episodes-watched currently sits, so an
    /// entry one episode into its third viewing of a 12-episode series counts
    /// 2*12 = 24 rewatch episodes, not 2. Episodes-watched is the fallback
    /// baseline only when no total is published, since that's the only
    /// length the app knows for a still-airing or unpublished-length show.
    /// Primitive form shared for the same reason as <see
    /// cref="EpisodeSeconds(int?)"/>.</summary>
    public static int RewatchOnlyEpisodes(int rewatchCount, int? totalEpisodes, int episodesWatched) =>
        rewatchCount * (totalEpisodes ?? episodesWatched);

    public static int RewatchOnlyEpisodes(UserAnimeEntry entry) =>
        RewatchOnlyEpisodes(entry.RewatchCount, entry.Anime.TotalEpisodes, entry.EpisodesWatched);

    /// <summary>An entry's rewatch-inclusive episode count: its current
    /// episodes watched, plus <see cref="RewatchOnlyEpisodes"/> — so an entry
    /// one episode into its third viewing of a 12-episode series reads
    /// 1 + 24 = 25, not 3.</summary>
    public static int RewatchInclusiveEpisodes(UserAnimeEntry entry) =>
        entry.EpisodesWatched + RewatchOnlyEpisodes(entry);

    public static bool IsMovie(AnimeMetadata anime) =>
        string.Equals(anime.MediaType, MovieMediaType, StringComparison.OrdinalIgnoreCase);

    public static bool IsMusic(AnimeMetadata anime) =>
        string.Equals(anime.MediaType, MusicMediaType, StringComparison.OrdinalIgnoreCase);
}
