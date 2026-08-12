using AnimeTracker.Api.Models;

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
    int Episodes);

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

public record ScoreDistributionBucketDto(int Score, int Count);

public record ScoreDistributionDto(List<ScoreDistributionBucketDto> Buckets, double? MeanScore);

public record OpinionDivergenceItemDto(
    int AnimeId,
    string Title,
    string? EnglishTitle,
    string? PictureUrl,
    int MyScore,
    double MalScore,
    bool IsCompleted);

/// <summary>Rewatched is the "Most rewatched" section for the "all" scope —
/// ordered by rewatch count, with no tiers — for the page's initial render.</summary>
public record ProfileDto(
    AnimeStatsDto Stats,
    List<ActivityFeedItemDto> RecentActivity,
    TopAnimeSectionDto TopAnime,
    RewatchedSectionDto Rewatched,
    ScoreDistributionDto ScoreDistribution,
    List<OpinionDivergenceItemDto> TheyLikedItIDidnt,
    List<OpinionDivergenceItemDto> ILikedItTheyDidnt);
