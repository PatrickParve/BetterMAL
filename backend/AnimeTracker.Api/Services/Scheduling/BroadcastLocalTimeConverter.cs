namespace AnimeTracker.Api.Services.Scheduling;

public class BroadcastLocalTimeConverter : IBroadcastLocalTimeConverter
{
    private static readonly TimeZoneInfo LocalZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Helsinki");

    public DateOnly GetStartOfWeek(DateOnly referenceDate) => StartOfWeek(referenceDate);

    public DateOnly GetLocalDate(DateTimeOffset instantUtc) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instantUtc, LocalZone).DateTime);

    public TimeOnly GetLocalTime(DateTimeOffset instantUtc) =>
        TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(instantUtc, LocalZone).DateTime);

    public DateTimeOffset LocalMidnightUtc(DateOnly localDate)
    {
        var localWallClock = DateTime.SpecifyKind(localDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localWallClock, LocalZone), TimeSpan.Zero);
    }

    private static DateOnly StartOfWeek(DateOnly date)
    {
        var daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-daysSinceMonday);
    }
}
