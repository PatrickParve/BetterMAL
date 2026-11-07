namespace AnimeTracker.Api.Models;

/// <summary>Whether a theme is an opening or an ending. Stored by name, as
/// WatchStatus is. Insert songs are never stored (spec `anime-themes`).</summary>
public enum AnimeThemeKind
{
    Opening,
    Ending,
}

/// <summary>One opening or ending of one MyAnimeList anime, copied from
/// AnimeThemes by AnimeThemesCatalogueSyncService (spec `anime-themes`,
/// add-anime-themes-and-guess-the-theme design.md D1). Text and links only: no
/// audio, video or picture is ever stored. Keyed on the MAL id and AnimeThemes'
/// own theme id, both supplied by the sync and never generated. Deliberately no
/// FK to <see cref="AnimeMetadata"/>, as with <see cref="AnimeIdMapping"/>: most
/// rows name anime the app may never store, and requiring the row to exist
/// would fail the whole sync, or show "no themes" for a week for an anime
/// stored later. One theme of an AnimeThemes anime that names two MAL ids is
/// held under both; a MAL id that two AnimeThemes anime name holds both their
/// themes.</summary>
public class AnimeTheme
{
    public int AnimeId { get; set; } // MAL id
    public int ThemeId { get; set; } // AnimeThemes' id

    public AnimeThemeKind Kind { get; set; }

    /// <summary>AnimeThemes' own number, 1 when they give none (398 of 939 in a
    /// sample). Orders a row within its kind.</summary>
    public int Sequence { get; set; } = 1;

    /// <summary>AnimeThemes' slug: OP1, ED2, OP1-BD.</summary>
    public string Label { get; set; } = "";

    /// <summary>AnimeThemes' song id, the identity two anime sharing a song are
    /// told apart by. Null for the few themes with no song; indexed.</summary>
    public int? SongId { get; set; }

    public string? SongTitle { get; set; }

    /// <summary>AnimeThemes' name for the anime this row came from, which is its
    /// romaji title. It exists to call a song of an anime the app hasn't stored
    /// something in the openings-and-endings search; wherever the app has stored the
    /// anime, its own titles are used instead (improve-search-page design.md D8). Held
    /// on every row rather than once per MAL id, so that "the name of the anime this
    /// row came from" stays exact when one AnimeThemes anime names two MAL ids or one
    /// MAL id is named by two anime. Null when AnimeThemes sent none, or a blank one,
    /// and for a row stored before names were kept, until the next refresh.</summary>
    public string? AnimeName { get; set; }

    /// <summary>Empty for about 30% of songs. Each name is the artist's alias
    /// where one is set, as it's credited on this song, else their name.</summary>
    public List<string> Artists { get; set; } = [];

    /// <summary>Every version's episodes, joined, because a row says where the
    /// song plays, not where one cut does. Null for films and most OVAs.</summary>
    public string? Episodes { get; set; }

    /// <summary>Flags of the version the row uses (design.md D4), so they describe
    /// what would really play.</summary>
    public bool Nsfw { get; set; }
    public bool Spoiler { get; set; }

    /// <summary>The video used has no credits over it.</summary>
    public bool Clean { get; set; }

    /// <summary>Only an `https` link on `v.animethemes.moe` is held, and only an
    /// `https` link on `a.animethemes.moe` for audio: the backend fetches
    /// <see cref="AudioUrl"/>, and it came from outside, so this is what stops a
    /// bad row from aiming the relay anywhere else.</summary>
    public string? VideoUrl { get; set; }
    public string? AudioUrl { get; set; }
}
