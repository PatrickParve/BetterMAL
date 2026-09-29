namespace AnimeTracker.Api.Services.Setup;

/// <summary>What the rest of the app asks of the setup coordinator: whether its
/// leftover airing work is still running, Retry now, and a copy of its in-memory
/// state for the status read (design D1).</summary>
public interface ISetupCoordinator
{
    /// <summary>True while airing work left over when setup finished is still being
    /// fetched in this process. The hourly airing catch-up skips its turn meanwhile.</summary>
    bool IsDraining { get; }

    /// <summary>Makes everything that is waiting due at once: every anime waiting to
    /// retry and both services' paused queues. A throttle wait is left alone, since the
    /// service has asked to be left be.</summary>
    void RetryNow();

    SetupSnapshot GetSnapshot();
}
