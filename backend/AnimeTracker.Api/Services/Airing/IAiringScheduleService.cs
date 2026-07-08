namespace AnimeTracker.Api.Services.Airing;

/// <summary>Backs the Airing page: a week of my-list anime broadcast slots,
/// grouped by converted local day. Reads only from Postgres — no live MAL
/// calls on this path.</summary>
public interface IAiringScheduleService
{
    /// <summary>The week containing weekReferenceDate (defaults to today if
    /// null), laid out as seven local day-columns.</summary>
    Task<AiringWeekDto> GetWeekAsync(DateOnly? weekReferenceDate, CancellationToken ct = default);
}
