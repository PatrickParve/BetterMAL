using System.Text.Json;
using System.Text.Json.Serialization;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Games;
using static AnimeTracker.Api.Tests.Services.Games.GuessTheThemeTestKit;

namespace AnimeTracker.Api.Tests.Services.Games;

// add-anime-themes-and-guess-the-theme task 5.1: the settings are the shared four plus
// Theme, and the shared half behaves exactly as the other games' do.
public class GuessTheThemeSettingsTests
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public void GameIsTheSharedHalf()
    {
        var settings = new GuessTheThemeSettings(1990, 2010, ["tv", "ova"], GameListScope.OnlyMyList, true, GameThemeChoice.Endings);

        var game = settings.Game;

        Assert.Equal(1990, game.YearFrom);
        Assert.Equal(2010, game.YearTo);
        Assert.Equal(["tv", "ova"], game.MediaTypes);
        Assert.Equal(GameListScope.OnlyMyList, game.List);
        Assert.True(game.HideHentai);
    }

    [Fact]
    public void ASettingsRunStartedWithComesBackAsTheSameSettings()
    {
        var run = new GuessTheThemeRun
        {
            YearFrom = 2000, YearTo = 2005, MediaTypes = ["movie"], ListScope = GameListScope.NotOnMyList,
            HideHentai = true, Themes = GameThemeChoice.Openings,
        };

        var settings = GuessTheThemeSettings.From(run);

        Assert.Equal(2000, settings.YearFrom);
        Assert.Equal(2005, settings.YearTo);
        Assert.Equal(["movie"], settings.MediaTypes);
        Assert.Equal(GameListScope.NotOnMyList, settings.List);
        Assert.True(settings.HideHentai);
        Assert.Equal(GameThemeChoice.Openings, settings.Themes);
    }

    [Fact]
    public void ARunWithNoTypesIsEveryType()
    {
        var settings = GuessTheThemeSettings.From(new GuessTheThemeRun { MediaTypes = null });

        Assert.Null(settings.MediaTypes);
    }

    // ---- normal form ----

    [Fact]
    public void NoTypesIsStoredAsNullAndARepeatedTypeCountsOnce()
    {
        var empty = (AnyTheme with { MediaTypes = [] }).Normalized();
        var repeated = (AnyTheme with { MediaTypes = ["tv", "tv", "ova"] }).Normalized();

        Assert.Null(empty.MediaTypes);
        Assert.Equal(["tv", "ova"], repeated.MediaTypes);
    }

    [Fact]
    public void NormalisingKeepsTheTheme()
    {
        var settings = (AnyTheme with { Themes = GameThemeChoice.Endings, MediaTypes = [] }).Normalized();

        Assert.Equal(GameThemeChoice.Endings, settings.Themes);
    }

    // ---- validation ----

    [Theory]
    [InlineData(GameThemeChoice.All)]
    [InlineData(GameThemeChoice.Openings)]
    [InlineData(GameThemeChoice.Endings)]
    public void EachThemeChoiceIsValid(GameThemeChoice themes)
    {
        (AnyTheme with { Themes = themes }).Validate();
    }

    [Fact]
    public void AThemeThatIsNoneOfTheThreeIsRefused()
    {
        var settings = AnyTheme with { Themes = (GameThemeChoice)7 };

        var ex = Assert.Throws<GameSettingsException>(settings.Validate);

        Assert.Contains("theme", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WhatTheSharedSettingsRefuseIsRefusedHere()
    {
        Assert.Throws<GameSettingsException>((AnyTheme with { YearFrom = 2020, YearTo = 2010 }).Validate);
        Assert.Throws<GameSettingsException>((AnyTheme with { YearFrom = 0 }).Validate);
        Assert.Throws<GameSettingsException>((AnyTheme with { List = (GameListScope)9 }).Validate);
    }

    // ---- on the wire ----

    [Fact]
    public void ABodyReadsWithEnumsByNameAndTheSharedFieldsBesideTheTheme()
    {
        const string body = """
            {"yearFrom":1998,"yearTo":null,"mediaTypes":["tv"],"list":"OnlyMyList","hideHentai":true,"themes":"Endings"}
            """;

        var settings = JsonSerializer.Deserialize<GuessTheThemeSettings>(body, Wire)!;

        Assert.Equal(1998, settings.YearFrom);
        Assert.Null(settings.YearTo);
        Assert.Equal(["tv"], settings.MediaTypes);
        Assert.Equal(GameListScope.OnlyMyList, settings.List);
        Assert.True(settings.HideHentai);
        Assert.Equal(GameThemeChoice.Endings, settings.Themes);
    }

    [Fact]
    public void TheSharedHalfIsNotSerialisedAsAFieldOfItsOwn()
    {
        var json = JsonSerializer.Serialize(AnyTheme, Wire);

        Assert.DoesNotContain("\"game\"", json);
        Assert.Contains("\"themes\":\"All\"", json);
    }
}
