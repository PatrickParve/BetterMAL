namespace AnimeTracker.Api.Services.Tmdb;

/// <summary>The one place a TMDB image URL is built or recognised (design.md
/// D6). The cache stores only TMDB's own file path, so every DTO projection
/// and every validation goes through this pair, and a later change of size or
/// CDN is a one-line edit here plus the D9 rewrite of stored choices — never
/// a re-fetch.</summary>
public static class TmdbImageUrl
{
    public const string OriginalBase = "https://image.tmdb.org/t/p/original";

    private const string ImagePrefix = "https://image.tmdb.org/";

    /// <summary>TMDB's file paths already begin with "/", so the brief's
    /// literal template <c>…/original/{file_path}</c> would produce
    /// <c>original//abc.jpg</c>. The slash is prepended only when it is
    /// missing.</summary>
    public static string Original(string filePath) =>
        filePath.StartsWith('/') ? OriginalBase + filePath : $"{OriginalBase}/{filePath}";

    /// <summary>The one test for "is this a TMDB picture", used to keep a
    /// TMDB choice out of the MyAnimeList option lists (D13) and to route a
    /// refused import choice to TMDB rather than MAL (D14). Null-tolerant so a
    /// nullable <c>SelectedPictureUrl</c> can be passed as it is.</summary>
    public static bool IsTmdbImage(string? url) =>
        url is not null && url.StartsWith(ImagePrefix, StringComparison.Ordinal);
}
