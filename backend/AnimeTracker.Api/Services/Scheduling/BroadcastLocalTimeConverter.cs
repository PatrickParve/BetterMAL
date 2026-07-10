using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Mal;

namespace AnimeTracker.Api.Services.Scheduling;

public class BroadcastLocalTimeConverter : IBroadcastLocalTimeConverter
{
    private static readonly TimeZoneInfo LocalZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Helsinki");
    private static readonly TimeSpan JstOffset = TimeSpan.FromHours(9); // JST has no DST

    public LocalBroadcastSlot? ResolveForDate(string? jstDayOfWeek, TimeOnly? jstTime, DateOnly referenceLocalDate)
    {
        if (MalMappingExtensions.ParseMalDayOfWeek(jstDayOfWeek) is not { } targetDay || jstTime is not { } time)
            return null;

        // Helsinki trails JST by 6-7h, so the JST calendar date for this slot
        // is either referenceLocalDate itself or the day after — never
        // further away, and never before.
        foreach (var candidateJstDate in new[] { referenceLocalDate, referenceLocalDate.AddDays(1) })
        {
            if (candidateJstDate.DayOfWeek != targetDay)
                continue;

            var jstWallClock = candidateJstDate.ToDateTime(time);
            var localInstant = TimeZoneInfo.ConvertTimeFromUtc(jstWallClock - JstOffset, LocalZone);

            if (DateOnly.FromDateTime(localInstant) == referenceLocalDate)
                return new LocalBroadcastSlot(localInstant.DayOfWeek, TimeOnly.FromDateTime(localInstant));
        }

        return null;
    }

    public DateTimeOffset? NextBroadcastInstant(AnimeMetadata anime, DateTimeOffset afterUtc)
    {
        if (anime.AiringStatus != "currently_airing")
            return null;

        var afterLocal = TimeZoneInfo.ConvertTime(afterUtc, LocalZone);
        var startDate = DateOnly.FromDateTime(afterLocal.DateTime);

        for (var i = 0; i <= 7; i++)
        {
            var candidateDate = startDate.AddDays(i);
            var slot = ResolveForDate(anime.BroadcastDayOfWeek, anime.BroadcastTime, candidateDate);
            if (slot is null)
                continue;

            var localWallClock = DateTime.SpecifyKind(candidateDate.ToDateTime(slot.Time), DateTimeKind.Unspecified);
            var instantUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localWallClock, LocalZone), TimeSpan.Zero);
            if (instantUtc <= afterUtc)
                continue;

            return instantUtc;
        }

        return null;
    }

    public DateOnly GetStartOfWeek(DateOnly referenceDate) => StartOfWeek(referenceDate);

    public DateOnly GetLocalDate(DateTimeOffset instantUtc) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instantUtc, LocalZone).DateTime);

    public TimeOnly GetLocalTime(DateTimeOffset instantUtc) =>
        TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(instantUtc, LocalZone).DateTime);

    public DateOnly LocalDateOfJstBroadcast(DateOnly jstDate, TimeOnly jstTime)
    {
        var localInstant = TimeZoneInfo.ConvertTimeFromUtc(jstDate.ToDateTime(jstTime) - JstOffset, LocalZone);
        return DateOnly.FromDateTime(localInstant);
    }

    private static DateOnly StartOfWeek(DateOnly date)
    {
        var daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-daysSinceMonday);
    }
}
