using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Services.Games;

/// <summary>A song drawn before the game that will play it, so that game's first length can start as
/// Play is pressed (add-anime-themes-and-guess-the-theme design.md D10). The setup screen draws one
/// while it's open, and a lost game leaves the song it had ready, already read, for Play again. It's
/// only good for the settings it was drawn with. One at a time, in memory, as this game is one
/// person's; a restart simply means the next game draws as usual.</summary>
public sealed class GuessTheThemeOnDeck
{
    private readonly object _gate = new();
    private string? _settings;
    private GuessTheThemeRound? _song;

    public void Put(GuessTheThemeSettings settings, GuessTheThemeRound song)
    {
        lock (_gate)
        {
            _settings = KeyOf(settings);
            _song = song;
        }
    }

    /// <summary>The song ready for these settings, left where it is.</summary>
    public GuessTheThemeRound? Peek(GuessTheThemeSettings settings)
    {
        lock (_gate)
            return _settings == KeyOf(settings) ? _song : null;
    }

    /// <summary>The song ready for these settings, for a game to play. Whatever was held, for these
    /// settings or others, is gone afterwards, since a game has started.</summary>
    public GuessTheThemeRound? Take(GuessTheThemeSettings settings)
    {
        lock (_gate)
        {
            var song = _settings == KeyOf(settings) ? _song : null;
            _settings = null;
            _song = null;
            return song;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _settings = null;
            _song = null;
        }
    }

    // Records compare a list by reference, so settings are compared as text. The order types were
    // ticked in means nothing.
    private static string KeyOf(GuessTheThemeSettings settings)
    {
        var s = settings.Normalized();
        var types = s.MediaTypes is null ? "*" : string.Join(',', s.MediaTypes.Order(StringComparer.Ordinal));
        return $"{s.YearFrom}|{s.YearTo}|{types}|{s.List}|{s.HideHentai}|{s.Themes}";
    }
}
