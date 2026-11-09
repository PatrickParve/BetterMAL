namespace AnimeTracker.Api.Services.Ranking.ThisOrThat;

/// <summary>This or That: ranks one score of mine by halving, a pair at a time
/// (add-this-or-that-ranking design.md D2-D6). A ranking in progress is stored
/// as facts and replayed on every read, and nothing here changes my ranking
/// until <see cref="SaveAsync"/>. Every operation reads stored data only; none
/// makes a MyAnimeList or AniList request. Reads write nothing.</summary>
public interface IThisOrThatService
{
    /// <summary>The scores with at least two hand-orderable anime, highest first,
    /// each with its count, its most picks and whether a ranking of it is in
    /// progress.</summary>
    Task<ThisOrThatScoresDto> GetScoresAsync(CancellationToken ct = default);

    /// <summary>One score and, if a ranking of it is in progress, where it stands
    /// as the list stands now. Null when the score isn't offered.</summary>
    Task<ThisOrThatScoreState?> GetScoreAsync(int score, CancellationToken ct = default);

    /// <summary>Starts a ranking of the score in a new random order, or starts it
    /// over when <paramref name="replacingVersion"/> is the version of the ranking
    /// in progress. Throws <see cref="ThisOrThatNotEnoughAnimeException"/> for a
    /// score with fewer than two anime and <see cref="ThisOrThatStaleException"/>
    /// when a ranking is in progress and the version doesn't match.</summary>
    Task<ThisOrThatScoreState> StartAsync(int score, int? replacingVersion, CancellationToken ct = default);

    /// <summary>Records one pick (a null <paramref name="winnerId"/> is Can't
    /// decide). Throws <see cref="ThisOrThatRunNotFoundException"/> with no ranking
    /// in progress, <see cref="ThisOrThatStaleException"/> unless the two ids are
    /// the current pair, and <see cref="ThisOrThatWinnerNotInPairException"/> for
    /// a winner that is neither.</summary>
    Task<ThisOrThatScoreState> PickAsync(int score, int leftId, int rightId, int? winnerId, CancellationToken ct = default);

    /// <summary>Takes back the last pick. Throws
    /// <see cref="ThisOrThatStaleException"/> for a version that isn't current or
    /// when there is nothing to take back.</summary>
    Task<ThisOrThatScoreState> UndoAsync(int score, int version, CancellationToken ct = default);

    /// <summary>Writes the finished order as the score's hand-ordered order, exactly
    /// as the ranking editor would, then ends the ranking. Throws
    /// <see cref="ThisOrThatNotFinishedException"/> before the last anime is
    /// placed.</summary>
    Task SaveAsync(int score, int version, CancellationToken ct = default);

    /// <summary>Ends the ranking and leaves my ranking as it was (Keep my old
    /// order, Done).</summary>
    Task DiscardAsync(int score, int version, CancellationToken ct = default);
}
