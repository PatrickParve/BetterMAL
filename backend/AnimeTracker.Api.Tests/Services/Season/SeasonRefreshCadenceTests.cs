using AnimeTracker.Api.Services.Season;

namespace AnimeTracker.Api.Tests.Services.Season;

// SeasonRefreshCadence (design.md D7/D8, tasks.md 1.2-1.5): pure functions,
// so these tests drive them directly with an explicit "today" rather than a
// DB or the system clock.
public class SeasonRefreshCadenceTests
{
    [Theory]
    [InlineData(2026, "summer", 2026, 7, 15, 1)] // started this quarter
    [InlineData(2025, "fall", 2026, 9, 1, 1)] // started 11 months ago
    [InlineData(2025, "fall", 2026, 10, 1, 3)] // started exactly 12 months ago to the day
    [InlineData(2025, "fall", 2026, 11, 1, 3)] // started 13 months ago
    [InlineData(2025, "fall", 2027, 10, 1, 5)] // started exactly 24 months ago
    [InlineData(2025, "fall", 2030, 9, 1, 5)] // started 4 years 11 months ago
    [InlineData(2025, "fall", 2030, 10, 1, 10)] // started exactly 5 years ago
    [InlineData(2025, "fall", 2045, 10, 1, 10)] // started 20 years ago
    // A season whose start date has not arrived yet must stay in the daily
    // tier — SeasonHorizon's forward ceiling depends on this (design D8).
    [InlineData(2027, "winter", 2026, 9, 2, 1)]
    public void MinimumDaysBetweenFetchesMatchesTheAgeTier(int year, string season, int todayYear, int todayMonth, int todayDay, int expected)
    {
        var today = new DateOnly(todayYear, todayMonth, todayDay);

        var minimumDays = SeasonRefreshCadence.MinimumDaysBetweenFetches(year, season, today);

        Assert.Equal(expected, minimumDays);
    }

    [Theory]
    [InlineData(2026, "summer", 2026, 7, 15, 2026, 7, 15, true)] // same day, tier 0 (1-day interval)
    [InlineData(2025, "fall", 2026, 11, 1, 2026, 11, 1, true)] // same day, tier 1 (3-day interval)
    [InlineData(2025, "fall", 2027, 10, 1, 2027, 10, 1, true)] // same day, tier 5 (5-day interval)
    [InlineData(2025, "fall", 2045, 10, 1, 2045, 10, 1, true)] // same day, tier 10 (10-day interval)
    [InlineData(2026, "summer", 2026, 7, 15, 2026, 7, 14, false)] // one day later, stale in tier 0
    [InlineData(2025, "fall", 2026, 11, 1, 2026, 10, 31, true)] // one day later, fresh in tier 1
    [InlineData(2025, "fall", 2026, 11, 1, 2026, 10, 30, true)] // two days later, fresh in tier 1
    [InlineData(2025, "fall", 2026, 11, 1, 2026, 10, 29, false)] // three days later, stale in tier 1
    [InlineData(2025, "fall", 2027, 10, 1, 2027, 9, 27, true)] // four days later, fresh in the 5-day tier
    [InlineData(2025, "fall", 2027, 10, 1, 2027, 9, 26, false)] // five days later, stale in the 5-day tier
    [InlineData(2025, "fall", 2045, 10, 1, 2045, 9, 22, true)] // nine days later, fresh in the 10-day tier
    [InlineData(2025, "fall", 2045, 10, 1, 2045, 9, 21, false)] // ten days later, stale in the 10-day tier
    public void IsFreshHoldsAtTierBoundaries(
        int year, string season,
        int todayYear, int todayMonth, int todayDay,
        int lastFetchedYear, int lastFetchedMonth, int lastFetchedDay,
        bool expected)
    {
        var today = new DateOnly(todayYear, todayMonth, todayDay);
        var lastFetched = new DateOnly(lastFetchedYear, lastFetchedMonth, lastFetchedDay);

        var isFresh = SeasonRefreshCadence.IsFresh(year, season, lastFetched, today);

        Assert.Equal(expected, isFresh);
    }
}
