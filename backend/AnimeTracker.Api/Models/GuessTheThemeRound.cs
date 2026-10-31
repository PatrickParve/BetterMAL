namespace AnimeTracker.Api.Models;

/// <summary>One song a Guess the Opening/Ending game has drawn: the one being
/// played and, once a right guess has drawn it, the next one
/// (add-anime-themes-and-guess-the-theme design.md D7 and D10). Two are held at
/// once so the next clip can load while a reveal plays, which is why rounds are
/// a table of their own. Everything about the song is copied from the catalogue
/// at the draw, so a refresh landing mid-game can't change or remove what is
/// being played. A finished round's row is deleted, and the run's cascades the
/// rest.
/// <para>None of this reaches the browser before the song is revealed: not the
/// anime, not the song, not the label, not a link.</para></summary>
public class GuessTheThemeRound
{
    public int RunId { get; set; }

    /// <summary>Which song of the game this is, from 1: <see cref="GuessTheThemeRun.Round"/>
    /// names the one being played.</summary>
    public int Number { get; set; }

    /// <summary>The anime the song was drawn for, the one the reveal shows.</summary>
    public int AnimeId { get; set; }

    public int ThemeId { get; set; }
    public AnimeThemeKind Kind { get; set; }
    public string Label { get; set; } = "";

    /// <summary>A usable song always has an id, a video and an audio file
    /// (spec `guess-the-theme-selection`), so none of these three is null.</summary>
    public int SongId { get; set; }

    public string? SongTitle { get; set; }
    public List<string> Artists { get; set; } = [];

    /// <summary>The file the backend relays for the clip. Checked against the
    /// host allow-list again before every read.</summary>
    public string AudioUrl { get; set; } = "";

    /// <summary>The reveal's video, read ahead through the relay under the same check.</summary>
    public string VideoUrl { get; set; } = "";

    /// <summary>Every anime a right guess can name: the drawn one and each other
    /// anime the catalogue lists with the same song, stored or not, inside the
    /// game's settings or not. Filled at the draw by a local query on
    /// <see cref="SongId"/>.</summary>
    public List<int> AcceptedAnimeIds { get; set; } = [];
}
