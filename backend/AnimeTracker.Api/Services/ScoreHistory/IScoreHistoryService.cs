namespace AnimeTracker.Api.Services.ScoreHistory;

/// <summary>Reads of the score histories <see cref="IAnimeScoreRecorder"/> keeps
/// (add-score-history-graph design.md D6). Every read is a stored-data read:
/// none makes a MyAnimeList request, so opening a history draws nothing from
/// the outside and cannot move the rate-limit budget.</summary>
public interface IScoreHistoryService
{
    /// <summary>One anime's points, oldest first, with no markers. An anime with
    /// no records — or no such anime — has no points.</summary>
    Task<ScoreHistoryDto> GetAnimeHistoryAsync(int animeId, CancellationToken ct = default);

    /// <summary>One series' line over its main-line entries (extras never
    /// count), with a marker for each entry that first aired while the line was
    /// recorded. Throws <see cref="Series.SeriesIdNotFoundException"/> for an
    /// unknown series.</summary>
    Task<ScoreHistoryDto> GetSeriesHistoryAsync(int seriesId, CancellationToken ct = default);

    /// <summary>Whether the anime's points hold at least two different scores —
    /// what decides if its page offers a Score history control. Answered by the
    /// same builder the overlay's read uses.</summary>
    Task<bool> AnimeHasChangedAsync(int animeId, CancellationToken ct = default);

    /// <summary>The same question for a series' line. False for an unknown
    /// series.</summary>
    Task<bool> SeriesHasChangedAsync(int seriesId, CancellationToken ct = default);
}
