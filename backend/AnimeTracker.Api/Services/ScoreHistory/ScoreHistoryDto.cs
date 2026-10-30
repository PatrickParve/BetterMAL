namespace AnimeTracker.Api.Services.ScoreHistory;

/// <summary>One point of a score history: a score on a local calendar date.
/// A date-only value, so a viewer's timezone can never move a point across
/// midnight (add-score-history-graph design.md D6).</summary>
public record ScoreHistoryPointDto(DateOnly Date, double Score);

/// <summary>A main-line entry that first aired while its series' line was
/// being recorded: drawn above the line as its picture, on a point of the line
/// (design.md D11). Carries both titles so the client picks the one the user
/// reads, as every other entry does.</summary>
public record ScoreHistoryMarkerDto(int AnimeId, string Title, string? EnglishTitle, string? PictureUrl, DateOnly Date);

/// <summary>An anime's or a series' score history, oldest point first. An
/// anime's own read carries no markers, so one overlay draws both
/// (design.md D6).</summary>
public record ScoreHistoryDto(List<ScoreHistoryPointDto> Points, List<ScoreHistoryMarkerDto> Markers);
