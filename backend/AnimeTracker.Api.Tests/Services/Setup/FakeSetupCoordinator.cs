using AnimeTracker.Api.Services.Setup;

namespace AnimeTracker.Api.Tests.Services.Setup;

/// <summary>A stand-in for the setup coordinator: whatever its state is set to, and a
/// count of Retry now presses.</summary>
public sealed class FakeSetupCoordinator : ISetupCoordinator
{
    public bool IsDraining { get; set; }
    public SetupSnapshot Snapshot { get; set; } = SetupSnapshot.Idle;
    public int RetryNowCalls { get; private set; }

    public void RetryNow() => RetryNowCalls++;

    public SetupSnapshot GetSnapshot() => Snapshot;
}
