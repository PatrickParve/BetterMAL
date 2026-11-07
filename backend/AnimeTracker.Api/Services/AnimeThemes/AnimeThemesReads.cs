using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.AnimeThemes;

/// <summary>Reads the stored catalogue for the two pages that show it
/// (add-anime-themes-and-guess-the-theme design.md D5). Never touches
/// AnimeThemes.</summary>
public class AnimeThemesReads(AnimeTrackerDbContext db) : IAnimeThemesReads
{
    public async Task<AnimeThemesDto> GetForAnimeAsync(int animeId, CancellationToken ct = default)
    {
        var ready = await IsReadyAsync(ct);
        var rows = await db.AnimeThemes.AsNoTracking()
            .Where(t => t.AnimeId == animeId)
            .ToListAsync(ct);

        return new AnimeThemesDto(ready, Ordered(rows).Select(ToDto).ToList());
    }

    public async Task<SeriesThemesDto> GetForSeriesAsync(IReadOnlyList<int> animeIds, CancellationToken ct = default)
    {
        var ready = await IsReadyAsync(ct);
        var ids = animeIds.Distinct().ToList();
        var rows = ids.Count == 0
            ? []
            : await db.AnimeThemes.AsNoTracking().Where(t => ids.Contains(t.AnimeId)).ToListAsync(ct);

        var byAnime = rows.GroupBy(t => t.AnimeId).ToDictionary(g => g.Key, g => g.ToList());
        var groups = ids
            .Where(byAnime.ContainsKey)
            .Select(id => new SeriesThemeGroupDto(id, Ordered(byAnime[id]).Select(ToDto).ToList()))
            .ToList();

        return new SeriesThemesDto(ready, groups);
    }

    /// <summary>Whether a refresh has ever finished. A failed first attempt leaves
    /// a state row with no <c>LastSyncedAt</c>, which is not ready. The one definition:
    /// the pages' "not loaded yet" note and Guess the Opening/Ending's "songs on their
    /// way" both ask it.</summary>
    public static Task<bool> IsCatalogueReadyAsync(AnimeTrackerDbContext db, CancellationToken ct = default) =>
        db.AnimeThemesSyncStates.AsNoTracking().AnyAsync(s => s.LastSyncedAt != null, ct);

    private Task<bool> IsReadyAsync(CancellationToken ct) => IsCatalogueReadyAsync(db, ct);

    // Openings, then endings; each by number, then label, then AnimeThemes' own id so the
    // order never depends on how the database happened to return the rows.
    private static IEnumerable<AnimeTheme> Ordered(IEnumerable<AnimeTheme> rows) =>
        rows.OrderBy(t => t.Kind)
            .ThenBy(t => t.Sequence)
            .ThenBy(t => t.Label, StringComparer.Ordinal)
            .ThenBy(t => t.ThemeId);

    /// <summary>One stored row as a page shows it. Internal so the OP/ED search can send
    /// exactly the theme the anime and series pages send for the same row, not a second copy of
    /// the mapping (improve-search-page task 3.5).</summary>
    internal static AnimeThemeDto ToDto(AnimeTheme theme) =>
        new(
            theme.ThemeId,
            theme.Kind,
            theme.Label,
            theme.SongTitle,
            theme.Artists,
            theme.Episodes,
            theme.Nsfw,
            theme.Spoiler,
            theme.Clean,
            theme.VideoUrl,
            theme.AudioUrl);
}
