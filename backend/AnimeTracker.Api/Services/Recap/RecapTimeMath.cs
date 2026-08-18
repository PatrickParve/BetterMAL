using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Profile;

namespace AnimeTracker.Api.Services.Recap;

/// <summary>The one place per-episode runtime is resolved (design.md
/// decision 7), shared by <see cref="RecapStatsBuilder"/>'s "Time spent" stat
/// and <see cref="RecapRankingBuilder"/>'s time-watched ranking, so a
/// ranking's per-group total can never drift from the headline stat.</summary>
internal static class RecapTimeMath
{
    public static int EpisodeSeconds(AnimeMetadata anime) =>
        anime.AverageEpisodeDurationSeconds ?? ProfileService.AssumedMinutesPerEpisode * 60;
}
