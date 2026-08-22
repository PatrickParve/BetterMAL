using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Recap;
using AnimeTracker.Api.Services.Series;

namespace AnimeTracker.Api.Services.Profile;

public record AnimeStatsDto(
    double Days,
    double? MeanScore,
    int Watching,
    int Completed,
    int OnHold,
    int Dropped,
    int PlanToWatch,
    int TotalEntries,
    int Rewatched,
    int Episodes,
    int Movies,
    int Rewatching);

public record ActivityFeedItemDto(
    long Id,
    DateTimeOffset Timestamp,
    int AnimeId,
    string AnimeTitle,
    string? AnimeEnglishTitle,
    string? PictureUrl,
    ActivityChangeType ChangeType,
    string? ChangeDetail,
    string Summary);

public record TopAnimeEntryDto(
    int AnimeId,
    string Title,
    string? EnglishTitle,
    string? PictureUrl,
    int MyScore);

/// <summary>One score tier of the ordered preference list. Members is every
/// scored anime in this tier for the current scope, in tier order (explicitly
/// ordered members first, then alphabetical); IncludedCount is how many of
/// them made it into the resolved top list — the rest sit below the overlay's
/// cut line.</summary>
public record TopAnimeTierDto(int Score, List<TopAnimeEntryDto> Members, int IncludedCount);

/// <summary>Items is the resolved "My top anime" list (score-10s plus the
/// filled remainder) for MediaType's scope. Tiers carries only the tiers that
/// contribute to Items, each in full (including members below the cut line),
/// and backs the tier order editor overlay.</summary>
public record TopAnimeSectionDto(
    List<TopAnimeEntryDto> Items,
    List<TopAnimeTierDto> Tiers,
    string MediaType);

public record RewatchedEntryDto(
    int AnimeId,
    string Title,
    string? EnglishTitle,
    string? PictureUrl,
    int RewatchCount,
    int? MyScore);

/// <summary>Every rewatched entry (RewatchCount > 0) for MediaType's scope,
/// ordered by rewatch count descending with no tiers and no cap.</summary>
public record RewatchedSectionDto(List<RewatchedEntryDto> Items, string MediaType);

/// <summary>One franchise ranked by Top series: display fields from its root
/// anime, its total member count, and its two main-series averages —
/// identical in shape and computation to the series page's own
/// <c>MAL · main series</c>/<c>Mine · main series</c> chips (design.md
/// decision 1), so the two surfaces can never disagree. <c>MalRevealed</c> is
/// the server-computed "may this be shown under 'always show completed
/// scores'" boolean (design.md decision 5) — the client passes it straight
/// into ScoreValue's <c>completed</c> prop. <c>MainLineAiredCount</c> is the
/// main-line member count excluding entries that are announced but haven't
/// started airing — a main-line sequel with zero episodes out doesn't make
/// the franchise multi-entry yet, so the client's multi-entry filter reads
/// this instead of <c>MalMain.TotalCount</c>/<c>MineMain.TotalCount</c>.</summary>
public record TopSeriesItemDto(
    int SeriesId,
    int RootAnimeId,
    string Title,
    string? EnglishTitle,
    string? PictureUrl,
    int EntryCount,
    int MainLineAiredCount,
    SeriesAverageDto MalMain,
    SeriesAverageDto MineMain,
    bool MalRevealed);

/// <summary>Every series with at least one member in my list (design.md
/// decision 2), ordered by my main-series average descending, then scored
/// main-line count descending, then raw title case-insensitively — a
/// sensible default for a client that does nothing with the ranking-basis
/// control (design.md decision 3/task 3.3). The client re-sorts and filters
/// this same array locally when the basis is switched (design.md decision
/// 4).</summary>
public record TopSeriesSectionDto(List<TopSeriesItemDto> Items);

/// <summary>One entry the progress bar can't resolve a total for — neither a
/// published episode count nor a known non-zero aired count, despite the
/// anime having actually started airing — identifying enough to let the
/// unresolved-entries overlay open its detail page.</summary>
public record UnresolvedEpisodeEntryDto(int AnimeId, string Title, string? EnglishTitle, string? PictureUrl, int EpisodesWatched);

/// <summary>All-list episode progress (profile-stats "All-list episode
/// progress"). <c>TotalEntries</c> is my whole list, dropped entries
/// included, kept only to contextualise <c>UnresolvedEntries</c> (design.md
/// decision 9) — the figure itself excludes dropped entries outright. Every
/// other entry's total resolves, in order, to its anime's published
/// <c>TotalEpisodes</c>, then its stored aired-so-far count when that's known
/// and non-zero. An entry with neither, whose anime hasn't started airing at
/// all, has nothing to progress against yet and is excluded outright, same as
/// a dropped entry. Anything else reaching that point is genuinely
/// <b>unresolved</b>: counted in <c>UnresolvedEntries</c>, listed in full in
/// <c>UnresolvedAnime</c> (alphabetical by title), and excluded from
/// <c>EpisodesWatched</c>/<c>EpisodesTotal</c> alike, so the gap stays visible
/// rather than silent.</summary>
public record EpisodeProgressDto(
    int EpisodesWatched, int EpisodesTotal, int UnresolvedEntries, int TotalEntries,
    List<UnresolvedEpisodeEntryDto> UnresolvedAnime);

public record ScoreDistributionBucketDto(int Score, int Count);

public record ScoreDistributionDto(List<ScoreDistributionBucketDto> Buckets, double? MeanScore);

/// <summary><c>IsCompleted</c> is the server-computed "may this MAL score be
/// shown under 'always show completed scores'" flag — true for Completed
/// and Dropped entries alike (score-visibility), despite the field's name,
/// which is kept as-is since it's the wire contract for ScoreValue's
/// <c>completed</c> prop.</summary>
public record OpinionDivergenceItemDto(
    int AnimeId,
    string Title,
    string? EnglishTitle,
    string? PictureUrl,
    int MyScore,
    double MalScore,
    bool IsCompleted);

/// <summary>Rewatched is the "Most rewatched" section for the "all" scope —
/// ordered by rewatch count, with no tiers — for the page's initial render.
/// FavouriteSeasons/FavouriteYears rank my whole list on the same Bayesian
/// basis as a recap's season/year rankings (profile-stats "Favourite seasons
/// and years", design.md decision 6) — sent in full, not truncated to five,
/// since the "See all" overlay needs the tail.</summary>
public record ProfileDto(
    AnimeStatsDto Stats,
    EpisodeProgressDto EpisodeProgress,
    List<ActivityFeedItemDto> RecentActivity,
    TopAnimeSectionDto TopAnime,
    RewatchedSectionDto Rewatched,
    ScoreDistributionDto ScoreDistribution,
    List<OpinionDivergenceItemDto> TheyLikedItIDidnt,
    List<OpinionDivergenceItemDto> ILikedItTheyDidnt,
    List<RecapSeasonRankingDto> FavouriteSeasons,
    List<RecapYearRankingDto> FavouriteYears);
