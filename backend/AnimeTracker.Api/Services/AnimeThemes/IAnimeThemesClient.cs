namespace AnimeTracker.Api.Services.AnimeThemes;

/// <summary>AnimeThemes answered with something the catalogue can't use: a
/// rate limit it won't lift in time, a status other than 200, a body that isn't
/// what the query asked for, or GraphQL errors. Only the weekly sync reads
/// AnimeThemes' API, and it treats this, like any other failure, as a refresh
/// that didn't happen (spec `anime-themes`).</summary>
public class AnimeThemesException(string message, Exception? innerException = null)
    : Exception(message, innerException);

/// <summary>The one question the catalogue sync asks AnimeThemes: a page of its
/// anime with their openings, endings, versions and files
/// (add-anime-themes-and-guess-the-theme design.md D2).</summary>
public interface IAnimeThemesClient
{
    /// <summary>Page <paramref name="page"/> (from 1) of the whole list, 100
    /// anime to a page, in id order. Throws <see cref="AnimeThemesException"/>
    /// when AnimeThemes can't be understood, and lets a transport failure or a
    /// timeout through as it is.</summary>
    Task<AnimeThemesPage> GetPageAsync(int page, CancellationToken ct = default);
}
