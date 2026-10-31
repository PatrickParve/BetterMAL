using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Games;

/// <summary>Which stored anime and songs Guess the Opening/Ending can use (spec
/// `guess-the-theme-selection`, add-anime-themes-and-guess-the-theme design.md D8).
/// <see cref="UsableSongs"/> is the one definition of a usable song, and
/// <see cref="Query"/> is <see cref="GamePool.Query"/> narrowed to the anime that have one, so
/// what the setup screen counts is exactly what a game draws from, as in the other two games.</summary>
public static class GuessTheThemePool
{
    /// <summary>Every opening or ending a game could play under these settings. A song is
    /// usable when it:
    /// <list type="bullet">
    /// <item><description>is an opening or an ending with a known song, a video and an audio
    /// file;</description></item>
    /// <item><description>isn't flagged spoiler in the version the catalogue uses, because the
    /// reveal plays its video by itself, for a show I may be halfway through;</description></item>
    /// <item><description>isn't flagged NSFW in that version while Hide NSFW is on;</description></item>
    /// <item><description>suits the Theme setting.</description></item>
    /// </list>
    /// The catalogue holds these flags for the version it uses (design D4), so they describe
    /// what would really play. Not tracked, and a rule written so the database can run it:
    /// the pool query and the draw both build on this query.</summary>
    public static IQueryable<AnimeTheme> UsableSongs(AnimeTrackerDbContext db, GuessTheThemeSettings settings)
    {
        var songs = db.AnimeThemes.AsNoTracking()
            .Where(t => t.SongId != null && t.VideoUrl != null && t.AudioUrl != null)
            .Where(t => !t.Spoiler);

        if (settings.HideHentai)
            songs = songs.Where(t => !t.Nsfw);

        return settings.Themes switch
        {
            GameThemeChoice.Openings => songs.Where(t => t.Kind == AnimeThemeKind.Opening),
            GameThemeChoice.Endings => songs.Where(t => t.Kind == AnimeThemeKind.Ending),
            _ => songs,
        };
    }

    /// <summary>Usable songs not in <paramref name="playedSongIds"/>: a song plays once in a
    /// game, even through another anime that uses it. Null or empty leaves the usable songs
    /// as they are.</summary>
    public static IQueryable<AnimeTheme> SongsLeft(
        AnimeTrackerDbContext db, GuessTheThemeSettings settings, IReadOnlyCollection<int>? playedSongIds)
    {
        var songs = UsableSongs(db, settings);
        if (playedSongIds is not { Count: > 0 })
            return songs;

        var played = playedSongIds.ToArray();
        return songs.Where(t => !played.Contains(t.SongId!.Value));
    }

    /// <summary>Every anime a game of Higher or Lower with the same release years, types,
    /// Anime setting and Hide NSFW could show, that also has a usable song not yet played.
    /// Not tracked, so callers project it. With <paramref name="playedSongIds"/> left out,
    /// it is the count's pool.</summary>
    public static IQueryable<AnimeMetadata> Query(
        AnimeTrackerDbContext db,
        GuessTheThemeSettings settings,
        DateTimeOffset nowUtc,
        IReadOnlyCollection<int>? playedSongIds = null)
    {
        var songs = SongsLeft(db, settings, playedSongIds);
        return GamePool.Query(db, settings.Game, nowUtc).Where(a => songs.Any(t => t.AnimeId == a.Id));
    }
}
