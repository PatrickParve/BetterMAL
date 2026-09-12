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

    /// <summary>The same local-date read as <see
    /// cref="ResolveOnLocalDateAsync(AnimeMetadata,DateOnly,CancellationToken)"/>,
    /// for many anime at once. Default implementation loops over the
    /// per-anime member, so a test double need not restate it — but any
    /// implementation backed by a database MUST override this with a single
    /// read (design.md D6), since the default still issues one query per
    /// anime.</summary>
    async Task<Dictionary<int, ResolvedEpisode>> ResolveOnLocalDateAsync(IReadOnlyCollection<AnimeMetadata> anime, DateOnly localDate, CancellationToken ct = default)
    {
        var result = new Dictionary<int, ResolvedEpisode>();
        foreach (var a in anime)
            if (await ResolveOnLocalDateAsync(a, localDate, ct) is { } episode)
                result[a.Id] = episode;
        return result;
    }

    /// <summary>The earliest stored air instant after afterUtc, or null when no
    /// future episode is stored. Backs the currently-watching countdown.</summary>
    Task<DateTimeOffset?> NextAiringInstantAsync(AnimeMetadata anime, DateTimeOffset afterUtc, CancellationToken ct = default);

    /// <summary>The same next-instant read as <see
    /// cref="NextAiringInstantAsync(AnimeMetadata,DateTimeOffset,CancellationToken)"/>,
    /// for many anime at once. Default implementation loops over the
    /// per-anime member, so a test double need not restate it — but any
    /// implementation backed by a database MUST override this with a single
    /// read (design.md D6), since the default still issues one query per
    /// anime.</summary>
    async Task<Dictionary<int, DateTimeOffset>> NextAiringInstantAsync(IReadOnlyCollection<AnimeMetadata> anime, DateTimeOffset afterUtc, CancellationToken ct = default)
    {
        var result = new Dictionary<int, DateTimeOffset>();
        foreach (var a in anime)
            if (await NextAiringInstantAsync(a, afterUtc, ct) is { } instant)
                result[a.Id] = instant;
        return result;
    }

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

    /// <summary>The same batched aired-so-far read as the <c>AnimeMetadata</c>
    /// overload above, taking anime ids directly rather than loaded entities
    /// (add-series-browser design.md D6) — the honest signature for what this
    /// does, since the implementation only ever reads the id. Backs the
    /// series list projection's behind-count, which has ids from a
    /// navigation-property-free projection and no reason to load entities
    /// just to discard everything but their id.</summary>
    Task<Dictionary<int, int>> EpisodesAiredAsOfAsync(IReadOnlyCollection<int> animeIds, DateTimeOffset nowUtc, CancellationToken ct = default);
}
