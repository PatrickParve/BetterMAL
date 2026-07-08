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

    public bool AirsOnLocalDate(AnimeMetadata anime, DateOnly referenceLocalDate) =>
        ResolveForDate(anime.BroadcastDayOfWeek, anime.BroadcastTime, referenceLocalDate) is not null;

    public LocalBroadcastSlot? ResolveForWeek(AnimeMetadata anime, DateOnly weekReferenceDate)
    {
        var weekStart = StartOfWeek(weekReferenceDate);
        for (var i = 0; i < 7; i++)
        {
            var slot = ResolveForDate(anime.BroadcastDayOfWeek, anime.BroadcastTime, weekStart.AddDays(i));
            if (slot is not null)
                return slot;
        }

        return null;
    }

    private static DateOnly StartOfWeek(DateOnly date)
    {
        var daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-daysSinceMonday);
    }
}
