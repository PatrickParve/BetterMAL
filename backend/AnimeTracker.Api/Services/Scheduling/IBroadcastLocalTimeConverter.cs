using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Services.Scheduling;

public record LocalBroadcastSlot(DayOfWeek DayOfWeek, TimeOnly Time);

/// <summary>Converts MAL's broadcast day/time (always JST, which has no DST) to
/// local (Europe/Helsinki) day-of-week and time-of-day. DST-aware: near a
/// transition, the same JST slot can resolve to a different local day/time
/// depending on which offset (EET/EEST) applies, so every conversion is
/// anchored to a specific reference date rather than assuming a fixed offset.
/// Pure/stateless — no DB access. Feeds the main-dashboard "Airing today"
/// filter and the airing-schedule weekly grouping.</summary>
public interface IBroadcastLocalTimeConverter
{
    /// <summary>The local day/time this JST broadcast slot resolves to, given
    /// that the result should fall on referenceLocalDate — or null if it
    /// doesn't (this slot doesn't air on that local date) or the inputs are
    /// unrecognized/missing.</summary>
    LocalBroadcastSlot? ResolveForDate(string? jstDayOfWeek, TimeOnly? jstTime, DateOnly referenceLocalDate);

    /// <summary>The next UTC instant at or after afterUtc that this anime's
    /// broadcast slot airs, or null when there is no known upcoming broadcast
    /// (not currently airing, or no broadcast day/time cached). Backs the
    /// currently-watching next-episode countdown.</summary>
    DateTimeOffset? NextBroadcastInstant(AnimeMetadata anime, DateTimeOffset afterUtc);

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

    /// <summary>The local (Europe/Helsinki) calendar date a broadcast at the
    /// given JST date + time lands on — e.g. a Monday 00:30 JST slot resolves to
    /// the preceding Sunday locally. Backs the weekly-cadence episode estimate,
    /// which anchors episode 1 to the show's local first-air date.</summary>
    DateOnly LocalDateOfJstBroadcast(DateOnly jstDate, TimeOnly jstTime);
}
