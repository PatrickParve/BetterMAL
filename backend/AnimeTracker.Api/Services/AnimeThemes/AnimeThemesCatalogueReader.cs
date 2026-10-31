using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Services.AnimeThemes;

/// <summary>Turns what AnimeThemes sent into the rows the catalogue stores
/// (spec `anime-themes`, add-anime-themes-and-guess-the-theme design.md D1 and
/// D4). A pure function of the pages it is given, so the whole catalogue can be
/// built in memory before the database is touched.
/// <list type="bullet">
/// <item><description>Only openings and endings are kept: an insert song is dropped, and so is an anime with no MyAnimeList id.</description></item>
/// <item><description>An AnimeThemes anime with several MAL ids is held under each of them, and a MAL id that
/// several anime name holds all their themes.</description></item>
/// <item><description>A row with no song, no video or no audio is still held: the page lists it, only the
/// game needs all three.</description></item>
/// <item><description>Whether the app has stored the anime makes no difference.</description></item>
/// </list></summary>
public static class AnimeThemesCatalogueReader
{
    public static List<AnimeTheme> Read(IEnumerable<AnimeThemesPage> pages)
    {
        var rows = new List<AnimeTheme>();

        // A page boundary can repeat an anime when AnimeThemes changes between two
        // requests, and a primary key can't be added twice.
        var seen = new HashSet<(int AnimeId, int ThemeId)>();

        foreach (var anime in pages.SelectMany(p => p.Anime))
        {
            if (anime.MalIds.Count == 0)
                continue;

            foreach (var theme in anime.Themes)
            {
                if (KindOf(theme.Type) is not { } kind)
                    continue;

                var chosen = AnimeThemeChoice.Choose(theme.Versions);
                foreach (var malId in anime.MalIds.Distinct())
                {
                    if (!seen.Add((malId, theme.Id)))
                        continue;

                    rows.Add(new AnimeTheme
                    {
                        AnimeId = malId,
                        ThemeId = theme.Id,
                        Kind = kind,
                        Sequence = theme.Sequence ?? 1,
                        Label = theme.Slug,
                        SongId = theme.Song?.Id,
                        SongTitle = NullIfBlank(theme.Song?.Title),
                        Artists = ArtistsOf(theme.Song),
                        Episodes = chosen.Episodes,
                        Nsfw = chosen.Nsfw,
                        Spoiler = chosen.Spoiler,
                        Clean = chosen.Clean,
                        VideoUrl = chosen.VideoUrl,
                        AudioUrl = chosen.AudioUrl,
                    });
                }
            }
        }

        return rows;
    }

    private static AnimeThemeKind? KindOf(string type) => type switch
    {
        "OP" => AnimeThemeKind.Opening,
        "ED" => AnimeThemeKind.Ending,
        _ => null,
    };

    /// <summary>The names credited on a song, in AnimeThemes' order of relevance:
    /// the alias an artist is credited under on this song where one is set, else
    /// their own name. A name is listed once: a group credited once for each of its
    /// members would otherwise read "BiSH, BiSH".</summary>
    private static List<string> ArtistsOf(AnimeThemesSong? song)
    {
        if (song is null)
            return [];

        var names = new List<string>();
        foreach (var credit in song.Credits)
        {
            var name = NullIfBlank(credit.Alias) ?? NullIfBlank(credit.ArtistName);
            if (name is not null && !names.Contains(name, StringComparer.Ordinal))
                names.Add(name);
        }

        return names;
    }

    private static string? NullIfBlank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
