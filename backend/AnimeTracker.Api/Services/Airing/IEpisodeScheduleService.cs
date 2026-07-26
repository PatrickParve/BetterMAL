using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Services.Airing;

/// <summary>An episode that airs on a particular local date: the local time of
/// day, and its number when known.</summary>
public record ResolvedEpisode(TimeOnly LocalTime, int? EpisodeNumber);

/// <summary>Answers "does an episode of this anime air on this local date, and
/// which one?" — the single source of truth behind the weekly schedule grid and
/// the home "Airing today" list. Prefers exact per-episode data from AniList
/// (break-aware); falls back to a weekly-cadence estimate bounded to the show's
/// actual air window so a finished/upcoming show only appears in the weeks it
/// really aired, not every week.</summary>
public interface IEpisodeScheduleService
{
    ResolvedEpisode? ResolveOnLocalDate(AnimeMetadata anime, DateOnly localDate);

    /// <summary>The next UTC instant at/after afterUtc an episode airs — from
    /// AniList when cached (so a mid-run break is skipped), else the plain
    /// weekly broadcast estimate. Backs the currently-watching countdown.</summary>
    DateTimeOffset? NextAiringInstant(AnimeMetadata anime, DateTimeOffset afterUtc);

    /// <summary>How many episodes have aired as of nowUtc — from the cached
    /// AniList schedule when present, else the weekly-cadence estimate. Never
    /// exceeds a known TotalEpisodes. Null means "not determinable": no cached
    /// schedule and no start date/broadcast time to estimate from. Backs the
    /// home page's airing-progress bar.</summary>
    int? EpisodesAiredAsOf(AnimeMetadata anime, DateTimeOffset nowUtc);
}
