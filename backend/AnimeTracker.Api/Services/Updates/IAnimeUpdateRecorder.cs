using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Services.Updates;

/// <summary>Turns whichever <see cref="AnimeUpdateKinds"/> a caller's own
/// detection just noticed into at most one queued <see cref="AnimeUpdate"/>
/// row, applying the two families' opposite duplication rules (design.md D2):
/// a becoming-known kind already recorded for the anime is dropped, while a
/// schedule-change kind is kept unconditionally. It also enforces the
/// relevance gate (<see cref="IAnimeUpdateRelevance"/>, design.md D1) before
/// any of that: nothing is queued for an anime that is neither the user's
/// non-Dropped entry nor directly linked to one, so callers can detect
/// whatever their own data can produce without needing to know whose anime it
/// is.</summary>
public interface IAnimeUpdateRecorder
{
    /// <summary>Queues one <see cref="AnimeUpdate"/> row for
    /// <paramref name="anime"/> covering whichever of <paramref name="kinds"/>
    /// survive deduplication, or queues nothing when the anime fails the
    /// relevance gate or none of <paramref name="kinds"/> survive
    /// deduplication. Adds to the tracked context only — the caller owns
    /// <c>SaveChangesAsync</c>, mirroring <see cref="Relations.AniListRelationStore"/>'s
    /// contract.</summary>
    Task RecordAsync(
        AnimeMetadata anime,
        AnimeUpdateKinds kinds,
        ScheduleMoveDetails moves,
        DateTimeOffset now,
        CancellationToken ct = default);
}
