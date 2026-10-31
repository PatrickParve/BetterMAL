using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Games;

/// <summary>Draws the next song of a Guess the Opening/Ending game (spec
/// `guess-the-theme-selection`, add-anime-themes-and-guess-the-theme design.md D8). The anime
/// comes first, through <see cref="GamePicker.Pick"/> on Guess the Anime's popularity curve,
/// because here too I have to name it; then a song at random from the ones that anime has
/// not played yet. Drawing the anime first is what keeps a show with twenty songs no likelier
/// on any one draw than a show with two. Nothing is held back from earlier games, so there is
/// no cooldown to pass the picker.
///
/// <para>Reads stored data only: no request goes to MyAnimeList, AniList or AnimeThemes.</para></summary>
public class GuessTheThemeDraw(AnimeTrackerDbContext db, Random random)
{
    private static readonly IReadOnlyDictionary<int, int> NothingRecent = new Dictionary<int, int>();

    /// <summary>One song, or null when the settings leave none that hasn't been played. The
    /// song is copied off the catalogue onto the returned round, so a refresh landing later
    /// can't change what is played. <see cref="GuessTheThemeRound.RunId"/> and
    /// <see cref="GuessTheThemeRound.Number"/> are the caller's to set; nothing is added to
    /// the context. The caller records the song's id as played.
    ///
    /// <para>An anime comes up again for as long as it has a song left, once for each, and
    /// is passed over once it has none. "Played" is by song id, so a song another anime
    /// shares, played for one, is gone for both.</para></summary>
    public async Task<GuessTheThemeRound?> DrawAsync(
        GuessTheThemeSettings settings, IReadOnlyCollection<int> playedSongIds, CancellationToken ct = default)
    {
        // Only anime with a song left are in the pool the picker draws from, so none is
        // drawn just to be passed over.
        var pool = await GuessTheThemePool.Query(db, settings, DateTimeOffset.UtcNow, playedSongIds)
            .ToCandidates().ToListAsync(ct);

        // An anime whose songs a refresh removed between the two reads is passed over too.
        var passedOver = new HashSet<int>();
        while (true)
        {
            var drawn = GamePicker.Pick(pool, passedOver, avoidScore: null, NothingRecent, GuessTheAnimeWeights.Popularity, random);
            if (drawn is null)
                return null;

            var songs = await GuessTheThemePool.SongsLeft(db, settings, playedSongIds)
                .Where(t => t.AnimeId == drawn.AnimeId)
                .ToListAsync(ct);
            if (songs.Count == 0)
            {
                passedOver.Add(drawn.AnimeId);
                continue;
            }

            return await ToRoundAsync(drawn.AnimeId, ChooseSong(songs), ct);
        }
    }

    /// <summary>Each song with the same chance, whatever the number of its themes: the
    /// uncommon anime that lists one song twice isn't likelier to play it. Of a song's
    /// themes the first in page order stands for it.</summary>
    private AnimeTheme ChooseSong(List<AnimeTheme> usable)
    {
        var songs = usable
            .GroupBy(t => t.SongId!.Value)
            .OrderBy(g => g.Key)
            .ToList();

        var song = songs[random.Next(songs.Count)];
        return song
            .OrderBy(t => t.Kind)
            .ThenBy(t => t.Sequence)
            .ThenBy(t => t.Label, StringComparer.Ordinal)
            .ThenBy(t => t.ThemeId)
            .First();
    }

    private async Task<GuessTheThemeRound> ToRoundAsync(int animeId, AnimeTheme theme, CancellationToken ct)
    {
        // A right guess names the drawn anime or any other the catalogue lists with the
        // same song, stored or not and inside the game's settings or not: nothing in the
        // audio separates them. Any theme counts here, usable or not.
        var sharing = await db.AnimeThemes.AsNoTracking()
            .Where(t => t.SongId == theme.SongId)
            .Select(t => t.AnimeId)
            .Distinct()
            .ToListAsync(ct);
        if (!sharing.Contains(animeId))
            sharing.Add(animeId);
        sharing.Sort();

        return new GuessTheThemeRound
        {
            AnimeId = animeId,
            ThemeId = theme.ThemeId,
            Kind = theme.Kind,
            Label = theme.Label,
            SongId = theme.SongId!.Value,
            SongTitle = theme.SongTitle,
            Artists = [.. theme.Artists],
            AudioUrl = theme.AudioUrl!,
            VideoUrl = theme.VideoUrl!,
            AcceptedAnimeIds = sharing,
        };
    }
}
