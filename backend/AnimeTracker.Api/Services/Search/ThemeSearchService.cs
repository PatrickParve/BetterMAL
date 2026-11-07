using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.AnimeThemes;

namespace AnimeTracker.Api.Services.Search;

/// <summary>One song in the OP/ED search, with the anime it belongs to as the app calls
/// it: for an anime the app has saved, its stored title, English title and the picture it
/// displays for it (the one I chose, where I chose one); for one it hasn't, AnimeThemes'
/// name for it as <c>Title</c> and no English title or picture. <c>Theme</c> is exactly
/// what the anime and series pages show for the same row.</summary>
public record ThemeSearchResultDto(int AnimeId, string Title, string? EnglishTitle, string? PictureUrl, AnimeThemeDto Theme);

/// <summary>The answer to an OP/ED search. <c>Ready</c> is whether the catalogue has ever
/// finished a refresh, which separates "the songs aren't here yet" from "nothing matches":
/// both would otherwise be an empty list.</summary>
public record ThemeSearchResponseDto(bool Ready, List<ThemeSearchResultDto> Results);

public interface IThemeSearchService
{
    /// <summary>Up to <paramref name="limit"/> songs matching <paramref name="query"/>, in
    /// the order spec <c>theme-search</c> gives. Reads only what the app has stored: no
    /// request to AnimeThemes or MyAnimeList, no background work, nothing written.</summary>
    Task<ThemeSearchResponseDto> SearchAsync(string query, int limit, CancellationToken ct = default);
}

/// <summary>Searches the stored catalogue of openings and endings (spec <c>theme-search</c>,
/// improve-search-page design.md D6 and D7). It takes the query the way every search does
/// (<see cref="SearchQuery"/>) and matches it the way every search does
/// (<see cref="SearchTextMatch"/>), over the catalogue index and the anime index, both in
/// memory. It makes no outside request and starts no work: it has no way to, since it
/// is given neither client.</summary>
public class ThemeSearchService(
    IThemeSearchIndex themeIndex,
    IAnimeSearchIndex animeIndex,
    AnimeTrackerDbContext db) : IThemeSearchService
{
    /// <summary>How well one text matches. The order is the ranking: exact first.</summary>
    private enum Quality { Exact = 0, Prefix = 1, Contains = 2 }

    /// <summary>Which text matched. The order is the ranking among equal qualities: a
    /// song-title match ahead of an artist match, ahead of an anime-title match.</summary>
    private enum Field { Song = 0, Artist = 1, Anime = 2 }

    private readonly record struct Hit(Quality Quality, Field Field);

    /// <summary>A saved anime's title as the query sees it, worked out once per query for each
    /// anime that has a song, not once per song.</summary>
    private sealed record SavedTitle(Quality? Quality, string DisplayTitle);

    private readonly record struct Found(
        ThemeSearchEntry Entry, AnimeTitleProjection? Saved, Hit Hit, int PopularityKey, string DisplayTitle);

    public async Task<ThemeSearchResponseDto> SearchAsync(string query, int limit, CancellationToken ct = default)
    {
        var ready = await AnimeThemesReads.IsCatalogueReadyAsync(db, ct);

        var (term, exact) = SearchQuery.Parse(query);
        var key = SearchTextMatch.Normalize(term);
        // A query that normalizes to nothing ("!!!") matches nothing, not everything;
        // checked before the indexes are read, as the anime search does.
        if (!ready || key.Length == 0 || limit < 1)
            return new ThemeSearchResponseDto(ready, []);

        var entries = await themeIndex.GetAsync(ct);
        var saved = await animeIndex.GetByIdAsync(ct);

        var savedTitles = new Dictionary<int, SavedTitle>();
        var matches = new List<Found>();

        foreach (var entry in entries)
        {
            var row = entry.Row;
            Quality? animeQuality;
            string displayTitle;
            int popularityKey;

            if (saved.TryGetValue(row.AnimeId, out var anime))
            {
                if (!savedTitles.TryGetValue(row.AnimeId, out var title))
                    savedTitles[row.AnimeId] = title = SavedTitleOf(anime, key, exact);

                animeQuality = title.Quality;
                displayTitle = title.DisplayTitle;
                popularityKey = SearchTextMatch.PopularityKey(anime.PopularityRank);
            }
            else
            {
                // An anime the app hasn't saved is called what AnimeThemes calls it; with no
                // name there is nothing to call it, and the song is left out (spec
                // `theme-search`, "A song's anime is named even when the app hasn't saved it").
                if (row.AnimeName is null)
                    continue;

                animeQuality = QualityOf(entry.AnimeNameKey, key, exact);
                displayTitle = row.AnimeName;
                popularityKey = int.MaxValue; // unranked: after every ranked anime
            }

            if (BestHit(entry, animeQuality, key, exact) is not { } hit)
                continue;

            matches.Add(new Found(entry, anime, hit, popularityKey, displayTitle));
        }

        // The comparison ends in the row's own key, so the order is total: the same
        // query over the same data gives the same order on every run.
        matches.Sort(Compare);

        var results = matches.Take(limit).Select(ToResult).ToList();
        return new ThemeSearchResponseDto(true, results);
    }

    /// <summary>The best of the three texts, and which one it was. Ties go to the earlier
    /// field (song, then artist, then anime). Null when none matches.</summary>
    private static Hit? BestHit(ThemeSearchEntry entry, Quality? animeQuality, string key, bool exact)
    {
        var bestQuality = QualityOf(entry.SongTitleKey, key, exact);
        var bestField = Field.Song;

        var artistQuality = BestArtistQuality(entry.ArtistKeys, key, exact);
        if (artistQuality is { } artist && (bestQuality is null || artist < bestQuality.Value))
        {
            bestQuality = artist;
            bestField = Field.Artist;
        }

        if (animeQuality is { } anime && (bestQuality is null || anime < bestQuality.Value))
        {
            bestQuality = anime;
            bestField = Field.Anime;
        }

        return bestQuality is { } quality ? new Hit(quality, bestField) : null;
    }

    private static Quality? BestArtistQuality(IReadOnlyList<string> artistKeys, string key, bool exact)
    {
        Quality? best = null;
        foreach (var artistKey in artistKeys)
        {
            if (QualityOf(artistKey, key, exact) is { } quality && (best is null || quality < best.Value))
                best = quality;
        }

        return best;
    }

    /// <summary>Exact when the normalized texts are equal; otherwise, unless the query was
    /// quoted, prefix when the text starts with the query and contains when it holds it. Both
    /// sides are already normalized.</summary>
    private static Quality? QualityOf(string? textKey, string key, bool exact)
    {
        if (textKey is null)
            return null;
        if (SearchTextMatch.EqualsPrenormalized(textKey, key))
            return Quality.Exact;
        if (exact)
            return null;
        if (SearchTextMatch.StartsWithPrenormalized(textKey, key))
            return Quality.Prefix;

        return SearchTextMatch.ContainsPrenormalized(textKey, key) ? Quality.Contains : null;
    }

    /// <summary>An anime's title matches when its stored title or its stored English title
    /// does, whichever matches better. The title shown with its songs, and sorted by, is the
    /// one the app displays everywhere: the English title where there is one.</summary>
    private static SavedTitle SavedTitleOf(AnimeTitleProjection anime, string key, bool exact)
    {
        var hasEnglish = !string.IsNullOrWhiteSpace(anime.EnglishTitle);
        var title = QualityOf(SearchTextMatch.Normalize(anime.Title), key, exact);
        var english = hasEnglish ? QualityOf(SearchTextMatch.Normalize(anime.EnglishTitle!), key, exact) : null;

        Quality? best = title;
        if (english is { } e && (best is null || e < best.Value))
            best = e;

        return new SavedTitle(best, hasEnglish ? anime.EnglishTitle! : anime.Title);
    }

    /// <summary>The ranking (spec <c>theme-search</c>, "OP/ED matches are ranked by how well and
    /// where they match"; design.md D7). Keys 7 to 10 repeat the order of an anime's own
    /// openings-and-endings box, <c>AnimeThemesReads.Ordered</c>: openings then endings, each
    /// by number, then label (ordinal), then AnimeThemes' own id.</summary>
    private static int Compare(Found a, Found b)
    {
        int result;
        if ((result = a.Hit.Quality.CompareTo(b.Hit.Quality)) != 0) return result;
        if ((result = a.Hit.Field.CompareTo(b.Hit.Field)) != 0) return result;
        if ((result = a.PopularityKey.CompareTo(b.PopularityKey)) != 0) return result;
        // false sorts before true: an anime the app has saved comes before one it hasn't.
        if ((result = (a.Saved is null).CompareTo(b.Saved is null)) != 0) return result;
        if ((result = StringComparer.OrdinalIgnoreCase.Compare(a.DisplayTitle, b.DisplayTitle)) != 0) return result;

        var left = a.Entry.Row;
        var right = b.Entry.Row;
        if ((result = left.AnimeId.CompareTo(right.AnimeId)) != 0) return result;
        if ((result = left.Kind.CompareTo(right.Kind)) != 0) return result;
        if ((result = left.Sequence.CompareTo(right.Sequence)) != 0) return result;
        if ((result = string.CompareOrdinal(left.Label, right.Label)) != 0) return result;
        return left.ThemeId.CompareTo(right.ThemeId);
    }

    private static ThemeSearchResultDto ToResult(Found match)
    {
        var row = match.Entry.Row;
        var theme = AnimeThemesReads.ToDto(row);

        return match.Saved is { } anime
            ? new ThemeSearchResultDto(row.AnimeId, anime.Title, NullIfBlank(anime.EnglishTitle), anime.PictureUrl, theme)
            : new ThemeSearchResultDto(row.AnimeId, row.AnimeName!, null, null, theme);
    }

    private static string? NullIfBlank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text;
}
