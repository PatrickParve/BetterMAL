namespace AnimeTracker.Api.Services.AnimeThemes;

// What the catalogue query asks AnimeThemes for, as parsed by AnimeThemesClient
// (add-anime-themes-and-guess-the-theme design.md D2). These carry AnimeThemes'
// own vocabulary and nothing is decided here: which versions and videos count is
// AnimeThemeChoice's job, and what becomes a stored row is
// AnimeThemesCatalogueReader's. Every list is empty rather than null.

/// <summary>One page of the catalogue, in AnimeThemes' id order.
/// <c>HasMorePages</c> is its own word for whether another page follows.</summary>
public record AnimeThemesPage(bool HasMorePages, IReadOnlyList<AnimeThemesAnime> Anime);

/// <summary>One AnimeThemes anime. It names no MyAnimeList id, one, or several:
/// an entry that spans more than one MAL entry carries each of them.</summary>
public record AnimeThemesAnime(int Id, IReadOnlyList<int> MalIds, IReadOnlyList<AnimeThemesTheme> Themes);

/// <summary>An opening, ending or insert song of an anime. <c>Type</c> is
/// AnimeThemes' own `OP`, `ED` or `IN`; <c>Sequence</c> is null where they give
/// no number; <c>Slug</c> is the label, such as `OP1` or `ED2`.</summary>
public record AnimeThemesTheme(
    int Id,
    string Type,
    int? Sequence,
    string Slug,
    AnimeThemesSong? Song,
    IReadOnlyList<AnimeThemesVersion> Versions);

public record AnimeThemesSong(int Id, string? Title, IReadOnlyList<AnimeThemesCredit> Credits);

/// <summary>One artist's performance of a song, in AnimeThemes' order of
/// relevance. <c>Alias</c> is the name the artist is credited under on this song,
/// where it differs from their own.</summary>
public record AnimeThemesCredit(string? Alias, string? ArtistName);

/// <summary>One version (AnimeThemes calls it an entry) of a theme: a cut that
/// plays in some episodes, flagged NSFW or spoiler, with videos of its own.</summary>
public record AnimeThemesVersion(
    int Number,
    string? Episodes,
    bool Nsfw,
    bool Spoiler,
    IReadOnlyList<AnimeThemesVideo> Videos);

/// <summary>One video file of a version, and the audio file that belongs to it.
/// <c>Clean</c> is AnimeThemes' `nc`: no credits over the picture.</summary>
public record AnimeThemesVideo(bool Clean, int Priority, string? VideoUrl, string? AudioUrl);
