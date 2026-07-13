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
    string? ChangeDetail);

public record TopAnimeEntryDto(
    int AnimeId,
    string Title,
    string? EnglishTitle,
    string? PictureUrl,
    int MyScore);

/// <summary>Items is the resolved "My top anime" list (score-10s plus the
/// filled remainder). Candidates/TieBreakSlots are only non-empty/non-zero
/// when the fill boundary lands mid-tier — i.e. there's an actual tie to
/// break — and back the selection overlay (15.6).</summary>
public record TopAnimeSectionDto(
    List<TopAnimeEntryDto> Items,
    int TieBreakSlots,
    List<TopAnimeEntryDto> Candidates,
    List<int> SelectedAnimeIds);

public record ScoreDistributionBucketDto(int Score, int Count);

public record ScoreDistributionDto(List<ScoreDistributionBucketDto> Buckets, double? MeanScore);

public record OpinionDivergenceItemDto(
    int AnimeId,
    string Title,
    string? EnglishTitle,
    string? PictureUrl,
    int MyScore,
    double MalScore);

public record ProfileDto(
    AnimeStatsDto Stats,
    List<ActivityFeedItemDto> RecentActivity,
    TopAnimeSectionDto TopAnime,
    ScoreDistributionDto ScoreDistribution,
    List<OpinionDivergenceItemDto> TheyLikedItIDidnt,
    List<OpinionDivergenceItemDto> ILikedItTheyDidnt);
