namespace AnimeTracker.Api.Services.Scheduling;

public class BroadcastLocalTimeConverter : IBroadcastLocalTimeConverter
{
    private static readonly TimeZoneInfo LocalZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Helsinki");
    private static readonly TimeZoneInfo JstZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo");

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

    public (DayOfWeek LocalDayOfWeek, TimeOnly LocalTime) ConvertBroadcastSlot(
        DayOfWeek jstDayOfWeek, TimeOnly jstTime, DateTimeOffset referenceUtc)
    {
        var referenceJstDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(referenceUtc, JstZone).DateTime);
        var daysUntilSlot = ((int)jstDayOfWeek - (int)referenceJstDate.DayOfWeek + 7) % 7;
        var slotJstDate = referenceJstDate.AddDays(daysUntilSlot);

        var slotJstWallClock = DateTime.SpecifyKind(slotJstDate.ToDateTime(jstTime), DateTimeKind.Unspecified);
        var slotInstantUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(slotJstWallClock, JstZone), TimeSpan.Zero);

        return (GetLocalDate(slotInstantUtc).DayOfWeek, GetLocalTime(slotInstantUtc));
    }

    private static DateOnly StartOfWeek(DateOnly date)
    {
        var daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-daysSinceMonday);
    }
}
