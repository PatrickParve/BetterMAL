namespace AnimeTracker.Api.Services.Ranking;

public record AnimeRankingScoreCountDto(int Score, int Count);

public record AnimeRankingMemberDto(int AnimeId, string Title, string? EnglishTitle, string? PictureUrl, int Rank);

/// <summary>One score's full hand-orderable membership, in ranking order —
/// the ranking editor's (design.md D7) unit of work. Short-form and dropped
/// members of the score are never included: their place follows from their
/// band and title alone, and nothing the editor does could move
/// them.</summary>
public record AnimeRankingTierDto(int Score, List<AnimeRankingMemberDto> Members);

public record AnimeRankingTierOrderRequest(int Score, List<int> AnimeIds);

public interface IAnimeRankingService
{
    /// <summary>The whole ranking, loaded fresh from the database — the
    /// DB-loading wrapper over <see cref="AnimeRankingSnapshot.Build"/> for
    /// callers that don't already hold the entry list and stored
    /// order.</summary>
    Task<AnimeRankingSnapshot> GetSnapshotAsync(CancellationToken ct = default);

    /// <summary>Every score from 10 down to 1 that holds at least one
    /// hand-orderable anime under <paramref name="mediaTypeScope"/>, with how
    /// many — the ranking editor's score selector (design.md D7). A score
    /// with nothing hand-orderable is omitted, not returned with a zero
    /// count.</summary>
    Task<List<AnimeRankingScoreCountDto>> GetScoreCountsAsync(string mediaTypeScope, CancellationToken ct = default);

    /// <summary>One score's full hand-orderable membership under
    /// <paramref name="mediaTypeScope"/>, in ranking order, each member
    /// carrying its overall rank — null when that score holds no
    /// hand-orderable anime under the scope.</summary>
    Task<AnimeRankingTierDto?> GetTierAsync(int score, string mediaTypeScope, CancellationToken ct = default);

    /// <summary>Applies an edited tier order — as displayed under some scope,
    /// which may omit tier members that scope's filter hides — via the
    /// slot-preserving merge: every edited tier's full hand-orderable
    /// membership becomes explicitly ordered, with hidden members kept in
    /// their existing relative positions. Throws
    /// <see cref="AnimeRankingTierScoreMismatchException"/> if any anime id
    /// isn't actually a hand-orderable member of the score its tier
    /// states.</summary>
    Task ApplyTierOrderAsync(List<AnimeRankingTierOrderRequest> tiers, CancellationToken ct = default);

    /// <summary>Repositions <paramref name="demotedAnimeId"/> to sit
    /// immediately after <paramref name="promotedAnimeId"/> within the score
    /// tier they share — the series page's tied-favourite reorder
    /// (polish-... design.md D?): moves only the entry whose position
    /// disagrees with the newly expressed preference, shifting the members
    /// between the two, rather than swapping the pair's own (possibly far
    /// apart) positions and leaving everyone between them untouched. A no-op
    /// when either anime is unknown, the two don't share a score, or either
    /// isn't currently hand-orderable — nothing here has a stored position to
    /// move.</summary>
    Task MoveAdjacentAsync(int promotedAnimeId, int demotedAnimeId, CancellationToken ct = default);

    /// <summary>design.md D6: places <paramref name="animeId"/> at the end of
    /// <paramref name="score"/>'s hand-ordered band — materialising that
    /// tier's full hand-ordered order (placed members, then never-placed
    /// alphabetically) and moving the anime to its end — then persists it
    /// through the same slot-preserving write <see cref="ApplyTierOrderAsync"/>
    /// uses. Called whenever a score is set or changed (design.md
    /// D6/list-editing capability), never when it's left unchanged.</summary>
    Task PlaceLastInTierAsync(int animeId, int score, CancellationToken ct = default);
}
