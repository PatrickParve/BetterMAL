using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Recap;

namespace AnimeTracker.Api.Tests.Services.Recap;

// RecapEntrySelector.Select (tasks.md 2.2-2.3, design.md decision 5): the
// single home of the recap's inclusion rules.
public class RecapEntrySelectorTests
{
    private static UserAnimeEntry Entry(
        int animeId, WatchStatus status, DateOnly? airedFrom = null, DateOnly? completedAt = null) =>
        new()
        {
            AnimeId = animeId,
            Anime = new AnimeMetadata { Id = animeId, Title = $"Anime {animeId}", AiredFrom = airedFrom },
            Status = status,
            CompletedAt = completedAt,
        };

    [Fact]
    public void LateDecemberStartLandsInTheEarlierYear()
    {
        var entry = Entry(1, WatchStatus.Watching, airedFrom: new DateOnly(2022, 12, 30));
        var period2022 = RecapPeriod.Yearly(2022);
        var period2023 = RecapPeriod.Yearly(2023);

        Assert.Single(RecapEntrySelector.Select([entry], period2022, RecapTimeFilter.Aired));
        Assert.Empty(RecapEntrySelector.Select([entry], period2023, RecapTimeFilter.Aired));
    }

    [Fact]
    public void LateDecemberStartIsIncludedInAMultiYearRangeEndingThatYear()
    {
        var entry = Entry(1, WatchStatus.Watching, airedFrom: new DateOnly(2020, 12, 30));
        var period = RecapPeriod.MultiYear(2011, 2020);

        Assert.Single(RecapEntrySelector.Select([entry], period, RecapTimeFilter.Aired));
    }

    [Fact]
    public void AnAnimeSpanningTwoYearsAppearsInExactlyOneYear()
    {
        // AiredFrom alone drives attribution — AiredTo (not modeled here)
        // never matters for selection, so a show that continued airing into
        // the following year still belongs to just its start year.
        var entry = Entry(1, WatchStatus.Watching, airedFrom: new DateOnly(2022, 6, 1));

        Assert.Single(RecapEntrySelector.Select([entry], RecapPeriod.Yearly(2022), RecapTimeFilter.Aired));
        Assert.Empty(RecapEntrySelector.Select([entry], RecapPeriod.Yearly(2023), RecapTimeFilter.Aired));
    }

    [Fact]
    public void MissingAiredFromIsExcludedUnderAiredButIncludedUnderWatched()
    {
        var entry = Entry(1, WatchStatus.Completed, airedFrom: null, completedAt: new DateOnly(2022, 5, 1));
        var period = RecapPeriod.Yearly(2022);

        Assert.Empty(RecapEntrySelector.Select([entry], period, RecapTimeFilter.Aired));
        Assert.Single(RecapEntrySelector.Select([entry], period, RecapTimeFilter.Watched));
    }

    [Theory]
    [InlineData(RecapTimeFilter.Watched)]
    [InlineData(RecapTimeFilter.Aired)]
    public void PlanToWatchNeverAppearsUnderEitherFilter(string filter)
    {
        var entry = Entry(
            1, WatchStatus.PlanToWatch,
            airedFrom: new DateOnly(2022, 5, 1),
            completedAt: new DateOnly(2022, 5, 1));

        Assert.Empty(RecapEntrySelector.Select([entry], RecapPeriod.Yearly(2022), filter));
    }

    [Fact]
    public void WatchedRequiresCompletedOrDroppedStatus()
    {
        var watching = Entry(1, WatchStatus.Watching, completedAt: new DateOnly(2022, 5, 1));
        var onHold = Entry(2, WatchStatus.OnHold, completedAt: new DateOnly(2022, 5, 1));
        var dropped = Entry(3, WatchStatus.Dropped, completedAt: new DateOnly(2022, 5, 1));
        var completed = Entry(4, WatchStatus.Completed, completedAt: new DateOnly(2022, 5, 1));

        var included = RecapEntrySelector.Select([watching, onHold, dropped, completed], RecapPeriod.Yearly(2022), RecapTimeFilter.Watched);

        Assert.Equal([3, 4], included.Select(e => e.AnimeId).OrderBy(id => id));
    }

    [Fact]
    public void AiredAllowsCompletedDroppedOnHoldAndWatching()
    {
        var statuses = new[] { WatchStatus.Completed, WatchStatus.Dropped, WatchStatus.OnHold, WatchStatus.Watching };
        var entries = statuses.Select((status, i) => Entry(i + 1, status, airedFrom: new DateOnly(2022, 5, 1))).ToList();

        var included = RecapEntrySelector.Select(entries, RecapPeriod.Yearly(2022), RecapTimeFilter.Aired);

        Assert.Equal(4, included.Count);
    }

    // The logged-progress arm under "watched" (design.md decision 3,
    // tasks.md 5.3/5.8).

    [Fact]
    public void AnEntryWithLoggedProgressAloneIsIncludedWithNoCompletionDate()
    {
        var entry = Entry(1, WatchStatus.Watching);

        var included = RecapEntrySelector.Select([entry], RecapPeriod.Yearly(2024), RecapTimeFilter.Watched, watchedAnimeIds: new HashSet<int> { 1 });

        Assert.Single(included);
    }

    [Fact]
    public void AnEntryQualifyingOnBothArmsIsCountedOnce()
    {
        var entry = Entry(1, WatchStatus.Completed, completedAt: new DateOnly(2024, 5, 1));

        var included = RecapEntrySelector.Select([entry], RecapPeriod.Yearly(2024), RecapTimeFilter.Watched, watchedAnimeIds: new HashSet<int> { 1 });

        Assert.Single(included);
    }

    [Fact]
    public void ARewatchPlacesAnAnimeInALaterPeriodsRecap()
    {
        var entry = Entry(1, WatchStatus.Completed, completedAt: new DateOnly(2022, 6, 1));

        var included2022 = RecapEntrySelector.Select([entry], RecapPeriod.Yearly(2022), RecapTimeFilter.Watched, watchedAnimeIds: new HashSet<int>());
        var included2024 = RecapEntrySelector.Select([entry], RecapPeriod.Yearly(2024), RecapTimeFilter.Watched, watchedAnimeIds: new HashSet<int> { 1 });

        Assert.Single(included2022);
        Assert.Single(included2024);
    }

    [Fact]
    public void NoLoggedProgressAndNoCompletionDateInPeriodExcludesTheEntry()
    {
        var entry = Entry(1, WatchStatus.Watching);

        var included = RecapEntrySelector.Select([entry], RecapPeriod.Yearly(2024), RecapTimeFilter.Watched, watchedAnimeIds: new HashSet<int>());

        Assert.Empty(included);
    }
}
