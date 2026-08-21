using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Services.Airing;

/// <summary>An episode that airs on a particular local date: the local time of
/// day, and its number.</summary>
public record ResolvedEpisode(TimeOnly LocalTime, int EpisodeNumber);

/// <summary>Answers "does an episode of this anime air on this local date, and
/// which one?" — the single source of truth behind the home "Airing today"
/// list. A thin reader over stored per-episode AniList airing rows; never
/// estimates from a broadcast cadence.</summary>
public interface IEpisodeScheduleService
{
    Task<ResolvedEpisode?> ResolveOnLocalDateAsync(AnimeMetadata anime, DateOnly localDate, CancellationToken ct = default);

    /// <summary>The earliest stored air instant after afterUtc, or null when no
    /// future episode is stored. Backs the currently-watching countdown.</summary>
    Task<DateTimeOffset?> NextAiringInstantAsync(AnimeMetadata anime, DateTimeOffset afterUtc, CancellationToken ct = default);

    /// <summary>The highest stored episode number whose air instant has passed
    /// as of nowUtc, or null when none has. Backs the home page's
    /// airing-progress bar and the detail page's aired count.</summary>
    Task<int?> EpisodesAiredAsOfAsync(AnimeMetadata anime, DateTimeOffset nowUtc, CancellationToken ct = default);

    /// <summary>The same aired-so-far count as <see
    /// cref="EpisodesAiredAsOfAsync(AnimeMetadata,DateTimeOffset,CancellationToken)"/>,
    /// for many anime at once in a single database read. An anime with no
    /// stored aired row is absent from the result. Backs the profile page's
    /// whole-list episode progress bar (design.md decision 8), so it can
    /// resolve every unknown-total entry's aired count without a query per
    /// entry.</summary>
    Task<Dictionary<int, int>> EpisodesAiredAsOfAsync(IReadOnlyCollection<AnimeMetadata> anime, DateTimeOffset nowUtc, CancellationToken ct = default);
}
