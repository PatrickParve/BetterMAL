using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Games;
using static AnimeTracker.Api.Tests.Services.Games.HigherOrLowerTestKit;

namespace AnimeTracker.Api.Tests.Services.Games;

// add-games-higher-or-lower task 3.1: what settings can describe a game at all.
// Whether enough anime match them is another question, which the data answers.
public class HigherOrLowerSettingsTests
{
    [Fact]
    public void TheDefaultsAreValid()
    {
        AnyAnime.Validate();
    }

    [Theory]
    [InlineData(2015, 2015)]
    [InlineData(2010, 2015)]
    [InlineData(null, 2015)]
    [InlineData(2010, null)]
    [InlineData(1917, 2026)]
    public void ARangeInOrderIsValid(int? from, int? to)
    {
        (AnyAnime with { YearFrom = from, YearTo = to }).Validate();
    }

    [Fact]
    public void AFromAfterTheToIsRefused()
    {
        var ex = Assert.Throws<HigherOrLowerSettingsException>(
            () => (AnyAnime with { YearFrom = 2016, YearTo = 2015 }).Validate());

        Assert.Contains("2016", ex.Message);
        Assert.Contains("2015", ex.Message);
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(-5, null)]
    [InlineData(null, 10_000)]
    [InlineData(null, 99_999)]
    public void AYearThatIsNotAYearIsRefused(int? from, int? to)
    {
        Assert.Throws<HigherOrLowerSettingsException>(
            () => (AnyAnime with { YearFrom = from, YearTo = to }).Validate());
    }

    [Fact]
    public void AnUnknownListSettingIsRefused()
    {
        Assert.Throws<HigherOrLowerSettingsException>(
            () => (AnyAnime with { List = (HigherOrLowerListScope)7 }).Validate());
    }

    [Fact]
    public void EmptyTypesAreEveryType()
    {
        Assert.Null((AnyAnime with { MediaTypes = [] }).Normalized().MediaTypes);
        Assert.Null(AnyAnime.Normalized().MediaTypes);
    }

    [Fact]
    public void ARepeatedTypeIsCountedOnce()
    {
        var normalized = (AnyAnime with { MediaTypes = ["tv", "movie", "tv"] }).Normalized();

        Assert.Equal(["tv", "movie"], normalized.MediaTypes);
    }

    [Fact]
    public void NormalizingChangesNothingElse()
    {
        var settings = new HigherOrLowerSettings(2010, 2015, ["ova"], HigherOrLowerListScope.NotOnMyList, true);

        Assert.Equal(settings, settings.Normalized(), new SettingsComparer());
    }

    [Fact]
    public void ARunHandsBackTheSettingsItWasStartedWith()
    {
        var run = new HigherOrLowerRun
        {
            YearFrom = 2000, YearTo = 2009, MediaTypes = ["tv"], ListScope = HigherOrLowerListScope.OnlyMyList, HideHentai = true,
        };

        var settings = HigherOrLowerSettings.From(run);

        Assert.Equal(2000, settings.YearFrom);
        Assert.Equal(2009, settings.YearTo);
        Assert.Equal(["tv"], settings.MediaTypes);
        Assert.Equal(HigherOrLowerListScope.OnlyMyList, settings.List);
        Assert.True(settings.HideHentai);
    }

    // The record's own equality compares MediaTypes by reference.
    private class SettingsComparer : IEqualityComparer<HigherOrLowerSettings>
    {
        public bool Equals(HigherOrLowerSettings? a, HigherOrLowerSettings? b) =>
            a is not null && b is not null
            && a.YearFrom == b.YearFrom && a.YearTo == b.YearTo && a.List == b.List && a.HideHentai == b.HideHentai
            && (a.MediaTypes ?? []).SequenceEqual(b.MediaTypes ?? []);

        public int GetHashCode(HigherOrLowerSettings s) => HashCode.Combine(s.YearFrom, s.YearTo, s.List, s.HideHentai);
    }
}
