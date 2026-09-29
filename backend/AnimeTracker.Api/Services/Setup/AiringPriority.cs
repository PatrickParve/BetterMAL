using AnimeTracker.Api.Services.Season;

namespace AnimeTracker.Api.Services.Setup;

/// <summary>Which of setup's four airing tiers an anime falls in (design D10; spec
/// "Airing dates are fetched for every list anime, most useful first"):
/// <list type="number">
/// <item><description>currently airing anime;</description></item>
/// <item><description>not-yet-aired anime starting this season or next season;</description></item>
/// <item><description>anime that started last season;</description></item>
/// <item><description>every other list anime.</description></item>
/// </list>
/// Tiers 1 to 3 are the airing priority set. Seasons are the app's viewing seasons,
/// judged from the anime's start date against today's local date. An upcoming anime
/// with no start date can't be said to start this season or next, so it is tier 4.
/// The AniList worker orders by tier and the status read counts the priority set
/// with the same function, so the two can't disagree.</summary>
public static class AiringPriority
{
    public const int PriorityTiers = 3;
    public const int LastTier = 4;

    public static int TierOf(string? malAiringStatus, DateOnly? airedFrom, DateOnly today)
    {
        if (malAiringStatus == "currently_airing")
            return 1;

        if (airedFrom is not { } started)
            return LastTier;

        var (year, season) = SeasonCalendar.GetSeasonFor(today);
        var (startYear, startSeason) = SeasonCalendar.GetSeasonFor(started);
        var seasonsAhead = SeasonCalendar.GetSeasonPointIndex(startYear, startSeason) - SeasonCalendar.GetSeasonPointIndex(year, season);

        if (malAiringStatus == "not_yet_aired" && seasonsAhead is 0 or 1)
            return 2;

        return seasonsAhead == -1 ? 3 : LastTier;
    }

    public static bool IsPriority(int tier) => tier <= PriorityTiers;
}
