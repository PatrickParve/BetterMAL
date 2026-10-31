using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Games;

/// <summary>The two reads every game's state makes alongside its own game in
/// progress (add-games-higher-or-lower design.md D3, add-guess-the-anime design.md
/// D3/D4): the release years the eligible anime cover, for the setup screen's From
/// and To, and two posters of popular anime for the Games tile and the setup
/// screen. Stored data only; neither read touches MyAnimeList.</summary>
public class GameLobbyReads(AnimeTrackerDbContext db, Random random)
{
    // The tile's art is two posters out of this many of the most popular anime.
    private const int PopularPosterPool = 200;
    private const int PostersOnTheTile = 2;

    /// <summary>The first and last release year of the anime any game could use
    /// under <paramref name="hideHentai"/>, or null when none has a start date.</summary>
    public async Task<GameYearsDto?> YearsAsync(bool hideHentai, CancellationToken ct = default)
    {
        var eligible = GamePool.Query(
            db, new GameSettings(null, null, null, GameListScope.All, hideHentai), DateTimeOffset.UtcNow);
        var earliest = await eligible.MinAsync(a => a.AiredFrom, ct);
        var latest = await eligible.MaxAsync(a => a.AiredFrom, ct);
        return earliest is { } min && latest is { } max ? new GameYearsDto(min.Year, max.Year) : null;
    }

    /// <summary>Two posters picked at random from the most popular anime, fewer
    /// when fewer have one. Hide NSFW leaves out a confirmed <c>rx</c>.
    /// <paramref name="exceptAnimeId"/> is left out whatever its popularity: Guess
    /// the Anime passes its answer, so the poster of the anime being hidden can't
    /// turn up in its own state.</summary>
    public async Task<List<string>> PopularPostersAsync(
        bool hideHentai, int? exceptAnimeId = null, CancellationToken ct = default)
    {
        var popular = db.AnimeMetadata.AsNoTracking().Where(a => a.PictureUrl != null && a.PopularityRank != null);
        if (hideHentai)
            popular = popular.Where(a => a.Rating != GamePool.HentaiRating);
        if (exceptAnimeId is { } except)
            popular = popular.Where(a => a.Id != except);

        var candidates = await popular
            .OrderBy(a => a.PopularityRank)
            .Take(PopularPosterPool)
            .Select(a => a.PictureUrl!)
            .ToListAsync(ct);

        // With fewer than two, the tile is still drawn with what there is.
        var chosen = new List<string>();
        while (chosen.Count < PostersOnTheTile && candidates.Count > 0)
        {
            var i = random.Next(candidates.Count);
            chosen.Add(candidates[i]);
            candidates.RemoveAt(i);
        }

        return chosen;
    }
}
