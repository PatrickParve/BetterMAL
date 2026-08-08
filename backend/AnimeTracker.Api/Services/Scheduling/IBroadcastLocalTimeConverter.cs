namespace AnimeTracker.Api.Services.Scheduling;

/// <summary>Converts UTC instants and local calendar dates to/from local
/// (Europe/Helsinki) wall-clock time. DST-aware. Pure/stateless — no DB
/// access. Feeds the main-dashboard "Airing today" filter and the
/// airing-schedule weekly grouping.</summary>
public interface IBroadcastLocalTimeConverter
{
    /// <summary>The Monday that starts the local (Europe/Helsinki) week
    /// containing referenceDate. Backs the airing-schedule week grouping,
    /// where callers need the actual local calendar date behind a resolved
    /// day-of-week slot, not just the day-of-week itself.</summary>
    DateOnly GetStartOfWeek(DateOnly referenceDate);

    /// <summary>The local (Europe/Helsinki) calendar date for instantUtc.
    /// Backs "what day is it locally right now" checks — the airing
    /// schedule's default week and the season page's once-per-local-day
    /// re-fetch both need this rather than the UTC calendar date.</summary>
    DateOnly GetLocalDate(DateTimeOffset instantUtc);

    /// <summary>The local (Europe/Helsinki) time-of-day for instantUtc. Used to
    /// render an exact per-episode air time from AniList's UTC timestamps.</summary>
    TimeOnly GetLocalTime(DateTimeOffset instantUtc);

    /// <summary>The UTC instant of local (Europe/Helsinki) midnight at the
    /// start of localDate — the inverse of <see cref="GetLocalDate"/>. Backs
    /// the day/week range queries behind "Airing today" and the airing-schedule
    /// grid, so a stored row range query stays in exact local-day bounds
    /// through a DST transition.</summary>
    DateTimeOffset LocalMidnightUtc(DateOnly localDate);
}
