using AnimeTracker.Api.Services.Entries;

namespace AnimeTracker.Api.Services.Series;

/// <summary>One anime's place in a series — either a main-line entry or an
/// extra. <c>RelationType</c> is the traversal-set relation (design.md
/// decision 1) connecting this anime to another member, taken from this
/// anime's own outgoing relation rows; a member reached only by a reverse
/// edge (its own relations were never fetched) or the root itself has no
/// such row, so it's null.</summary>
public record SeriesEntryDto(
    int AnimeId,
    string Title,
    string? EnglishTitle,
    string? PictureUrl,
    string? MediaType,
    string? AiringStatus,
    int? TotalEpisodes,
    int? AverageEpisodeDurationSeconds,
    DateOnly? AiredFrom,
    double? MalScore,
    string? RelationType,
    int Order,
    UserAnimeEntryDto? Entry);

/// <summary>One unweighted mean plus the count it was computed over, e.g.
/// "8.42 · 5 of 6 scored". <c>Value</c> is null when <c>ScoredCount</c> is
/// zero, and is otherwise unrounded (design.md decision 8) — the client
/// rounds for display.</summary>
public record SeriesAverageDto(double? Value, int ScoredCount, int TotalCount);

/// <summary>The four averages the series page shows (design.md decision 8):
/// MAL's score and mine, each across the main line and across every member.</summary>
public record SeriesScoresDto(
    SeriesAverageDto MalMain,
    SeriesAverageDto MalAll,
    SeriesAverageDto MineMain,
    SeriesAverageDto MineAll);

/// <summary>Series-wide stats (design.md decision 9). Episode/runtime totals
/// count only members with a known <c>TotalEpisodes</c> — an entry with an
/// unknown count contributes nothing and sets <c>HasUnknownEpisodeCounts</c>,
/// so the total is always a true lower bound rather than a guess. My
/// progress (watched episodes/seconds, entries completed) is scoped to the
/// main line only, matching "time left" being main-line runtime minus
/// watched runtime. <c>MainLineAiredEpisodes</c> is summed over the same
/// known-total main-line set (design.md decision 4): finished entries
/// contribute their full total, a currently-airing entry contributes its
/// aired-so-far count, and an entry that hasn't aired yet contributes
/// nothing — so it can never exceed <c>MainLineEpisodeTotal</c>.
/// <c>MainLineCompletedByMe</c> mirrors the frontend's
/// <c>isGroupCompleted</c> convention over the main line (design.md decision
/// 7). The highest-scored/favourite/studios/genres figures span every
/// member, main line and extras alike; the highest-scored lists carry every
/// tied entry rather than one arbitrary winner (design.md decision 8).</summary>
public record SeriesStatsDto(
    int MainLineEpisodeTotal,
    long MainLineRuntimeSeconds,
    bool HasUnknownEpisodeCounts,
    int MainLineAiredEpisodes,
    int ExtrasEpisodeTotal,
    long ExtrasRuntimeSeconds,
    int MyWatchedEpisodes,
    long MyWatchedSeconds,
    int EntriesCompleted,
    bool MainLineCompletedByMe,
    int MainLineCount,
    int ExtrasCount,
    int? LongestGapDays,
    int? LongestGapFromAnimeId,
    int? LongestGapToAnimeId,
    List<int> HighestMalScoreAnimeIds,
    List<int> MyHighestScoreAnimeIds,
    List<string> Studios,
    List<string> Genres);

/// <summary>Full projection of a franchise for the series page. <c>Status</c>
/// is one of "Ongoing", "Upcoming", "Finished", or "Finished · sequel
/// upcoming" (design.md/task 3.4) — computed server-side since it depends on
/// every member's airing status, not just the root's. <c>Title</c>,
/// <c>EnglishTitle</c> and <c>PictureUrl</c> come from the root entry
/// (design.md decision 4). <c>RootAniListId</c> is the root's AniList id
/// when a sync row exists for it, read the same way
/// <c>AnimeDetailService</c> reads it for a single anime (design.md decision
/// 6) — null falls back to an AniList title search client-side.</summary>
public record SeriesDto(
    int SeriesId,
    int RootAnimeId,
    int? RootAniListId,
    string Title,
    string? EnglishTitle,
    string? PictureUrl,
    string Status,
    int? FirstYear,
    int? LastYear,
    DateTimeOffset BuiltAt,
    bool IsPartial,
    bool IsTruncated,
    SeriesScoresDto Scores,
    SeriesStatsDto Stats,
    List<SeriesEntryDto> MainLine,
    List<SeriesEntryDto> Extras);
