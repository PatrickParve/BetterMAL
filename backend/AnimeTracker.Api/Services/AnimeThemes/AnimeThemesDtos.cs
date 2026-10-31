using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Services.AnimeThemes;

/// <summary>One opening or ending as a page shows it
/// (add-anime-themes-and-guess-the-theme design.md D5). <c>Id</c> is AnimeThemes' own
/// theme id, which names a row within an anime. <c>Kind</c> travels as its name.
/// <c>SongTitle</c>, <c>Episodes</c> and the two links are null where
/// AnimeThemes doesn't know them, and <c>Artists</c> is empty for a song with no
/// credited artist, so a row leaves out what isn't known rather than showing a
/// gap. <c>Nsfw</c> and <c>Spoiler</c> are the flags of the version that plays, and
/// <c>Clean</c> says its video has no credits over it. The links are the stored
/// ones: only `https` on AnimeThemes' two media hosts, and read by the browser
/// straight from there.</summary>
public record AnimeThemeDto(
    int Id,
    AnimeThemeKind Kind,
    string Label,
    string? SongTitle,
    List<string> Artists,
    string? Episodes,
    bool Nsfw,
    bool Spoiler,
    bool Clean,
    string? VideoUrl,
    string? AudioUrl);

/// <summary>An anime's openings, then its endings, each by number, then label.
/// <c>Ready</c> is whether a catalogue refresh has ever finished: it separates
/// "the songs aren't here yet" from "this anime has none", which would both
/// otherwise be an empty list.</summary>
public record AnimeThemesDto(bool Ready, List<AnimeThemeDto> Themes);

/// <summary>The themes of one anime among a series' main line, in the same order
/// as <see cref="AnimeThemesDto.Themes"/>.</summary>
public record SeriesThemeGroupDto(int AnimeId, List<AnimeThemeDto> Themes);

/// <summary>The themes of every anime a series page can show on its main line,
/// one group each, in main-line order. An anime with no themes has no group.</summary>
public record SeriesThemesDto(bool Ready, List<SeriesThemeGroupDto> Groups);
