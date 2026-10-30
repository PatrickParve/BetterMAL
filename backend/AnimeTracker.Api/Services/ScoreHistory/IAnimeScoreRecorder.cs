using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Services.ScoreHistory;

/// <summary>Keeps an anime's MyAnimeList score history: queues an
/// <see cref="AnimeScoreRecord"/> when the score <c>ApplyTo</c> or
/// <c>ApplyLeanTo</c> just wrote differs from the anime's latest record, and
/// once an episode is known to have aired (add-score-history-graph design.md
/// D3, D4). Every writer of a MAL score reaches it, either through
/// <see cref="Updates.IAnimeMetadataChangeDetector"/> or directly where the
/// detector is skipped.</summary>
public interface IAnimeScoreRecorder
{
    /// <summary>Queues at most one record of <paramref name="anime"/>'s current
    /// score, stamped <paramref name="now"/>, or nothing at all: no score, no
    /// reported airing status, a score equal to the latest record's, or no
    /// episode yet known to have aired. Adds to the tracked context only — the
    /// caller owns <c>SaveChangesAsync</c>, mirroring
    /// <see cref="Updates.IAnimeUpdateRecorder"/>, so a record lands in the same
    /// save as the score it records and a failed save stores neither.
    /// Makes no MyAnimeList request.</summary>
    Task RecordAsync(AnimeMetadata anime, DateTimeOffset now, CancellationToken ct = default);
}
