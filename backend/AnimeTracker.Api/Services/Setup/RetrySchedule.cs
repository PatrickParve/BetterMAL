namespace AnimeTracker.Api.Services.Setup;

/// <summary>The one retry schedule setup uses for everything that can wait and
/// try again: 1, 5, 15 and 60 minutes, then every 60 minutes (design D12, D13;
/// spec "A failing anime is retried and keeps its place" and "A service that
/// is down pauses its own queue"). A service's own ladder
/// (<see cref="ServiceHealth"/>) and each anime's ladder read it from here, so
/// the two can't drift apart.</summary>
public static class RetrySchedule
{
    private static readonly TimeSpan[] Steps =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromMinutes(60),
    ];

    /// <summary>The wait before the next try, after <paramref name="failures"/>
    /// failed tries in a row (0 gives the first wait). Past the last step it
    /// stays at 60 minutes.</summary>
    public static TimeSpan DelayAfter(int failures) => Steps[Math.Clamp(failures, 0, Steps.Length - 1)];
}
