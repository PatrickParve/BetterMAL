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

    /// <summary>Rewatch time for a member that may currently be mid-rewatch:
    /// <see cref="RewatchOnlyEpisodes(int, int?, int)"/> — the completed runs
    /// — plus, when the entry is marked <see cref="WatchStatus.Rewatching"/>,
    /// the episodes watched so far in the run that hasn't finished yet
    /// (design.md D1). The two terms cannot double-count: entering Rewatching
    /// resets episodes-watched to 0, and the rewatch count only increases once
    /// a run finishes, so episodes-watched is never simultaneously "progress
    /// on the current run" and "part of a run <see cref="RewatchOnlyEpisodes(int, int?, int)"/>
    /// already counted".
    ///
    /// <para>Known fallback gap: when no total is published,
    /// <see cref="RewatchOnlyEpisodes(int, int?, int)"/> falls back to
    /// episodes-watched as the per-run baseline. For an entry with no
    /// published total that is currently rewatching, that baseline is the
    /// current run's partial progress, not a full run — e.g. a rewatch count
    /// of 2 with 3 episodes watched of an unpublished-length show yields
    /// 2*3 + 3 = 9, understating the true figure. This is the pre-existing
    /// fallback being wrong in a new way rather than a new bug (it yielded
    /// 2*3 = 6 before this method existed); it only affects entries with no
    /// published episode count, both figures are lower bounds, and it is not
    /// fixed here.</para></summary>
    public static int RewatchEpisodesIncludingCurrentRun(
        int rewatchCount, int? totalEpisodes, int episodesWatched, WatchStatus? status) =>
        RewatchOnlyEpisodes(rewatchCount, totalEpisodes, episodesWatched)
        + (status == WatchStatus.Rewatching ? episodesWatched : 0);

    /// <summary>An entry's rewatch-inclusive episode count: its current
    /// episodes watched, plus <see cref="RewatchOnlyEpisodes"/> — so an entry
    /// one episode into its third viewing of a 12-episode series reads
    /// 1 + 24 = 25, not 3.</summary>
    public static int RewatchInclusiveEpisodes(UserAnimeEntry entry) =>
        entry.EpisodesWatched + RewatchOnlyEpisodes(entry);

    /// <summary>Primitive form of <see cref="FirstViewingEpisodes(UserAnimeEntry)"/>,
    /// shared for the same reason as <see cref="EpisodeSeconds(int?)"/>: the
    /// series ranking projection has no <see cref="UserAnimeEntry"/> to pass,
    /// and <c>MemberRewatchSeconds</c> already reaches this class's primitives
    /// for exactly this reason rather than restating their fallbacks.
    ///
    /// <para>An entry not marked <see cref="WatchStatus.Rewatching"/> is still
    /// on (or has only ever had) its first viewing, so its episodes watched
    /// already are the first-viewing figure. An entry marked Rewatching has,
    /// by definition, finished its first viewing before starting the
    /// rewatch — its episodes watched now describe the rewatch in progress
    /// instead — so its first viewing is counted as one complete run: the
    /// anime's published total, or its own episodes watched when no total is
    /// published (Rewatching requires a finished-airing anime, so a total is
    /// almost always available).</para></summary>
    public static int FirstViewingEpisodes(int? totalEpisodes, int episodesWatched, WatchStatus? status) =>
        status == WatchStatus.Rewatching ? totalEpisodes ?? episodesWatched : episodesWatched;

    /// <summary>An entry's first-viewing episode count only, with no rewatch
    /// folded in (page-polish-and-first-run-defaults design.md D4). See <see
    /// cref="FirstViewingEpisodes(int?, int, WatchStatus?)"/> for the rule.</summary>
    public static int FirstViewingEpisodes(UserAnimeEntry entry) =>
        FirstViewingEpisodes(entry.Anime.TotalEpisodes, entry.EpisodesWatched, entry.Status);

    /// <summary>The episodes an entry counts as watched for "how much of the
    /// main line have I watched" purposes: the greater of its own
    /// episodes-watched and its aired-so-far figure when the entry is marked
    /// <see cref="WatchStatus.Rewatching"/>, else episodes-watched as-is
    /// (design.md D2). Entering Rewatching resets episodes-watched to 0, so
    /// without this a franchise seen in full and now being rewatched would
    /// read as unwatched wherever this figure is used — the personal badge,
    /// the progress bar, and time watched/left. <c>max</c> rather than a bare
    /// aired figure: a rewatch already run past what the app believes has
    /// aired shouldn't be counted down. Falls back to episodesWatched when
    /// airedEpisodes is unknown. Governs only the watched side of a pair —
    /// never the episode total or the aired figure themselves, which describe
    /// the anime rather than the viewer.</summary>
    public static int EffectiveWatchedEpisodes(int episodesWatched, int? airedEpisodes, WatchStatus? status) =>
        status == WatchStatus.Rewatching
            ? Math.Max(episodesWatched, airedEpisodes ?? episodesWatched)
            : episodesWatched;

    public static bool IsMovie(AnimeMetadata anime) =>
        string.Equals(anime.MediaType, MovieMediaType, StringComparison.OrdinalIgnoreCase);

    public static bool IsMusic(AnimeMetadata anime) =>
        string.Equals(anime.MediaType, MusicMediaType, StringComparison.OrdinalIgnoreCase);
}
