namespace AnimeTracker.Api.Services.Updates;

/// <summary>The read side of the anime-updates feed: which recorded
/// <c>AnimeUpdate</c> rows are eligible to be shown (design.md D4, widened
/// past D5 by user request — an anime that is itself a non-Dropped list
/// entry, or is connected by <em>any</em> non-Contradicted relation edge to
/// one, not just a same-story one) and what each renders as. Both the Home
/// section's 30-day window and the full history go through this one method,
/// so the two can never disagree about which updates exist.</summary>
public interface IAnimeUpdateService
{
    /// <summary>Every eligible update detected at or after
    /// <paramref name="since"/>, newest first.</summary>
    Task<List<AnimeUpdateDto>> GetRecentAsync(DateTimeOffset since, CancellationToken ct = default);

    /// <summary>Every eligible update ever recorded, newest first.</summary>
    Task<List<AnimeUpdateDto>> GetHistoryAsync(CancellationToken ct = default);
}
