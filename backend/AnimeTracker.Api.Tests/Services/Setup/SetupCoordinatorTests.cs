using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Setup;

namespace AnimeTracker.Api.Tests.Services.Setup;

// add-first-run-setup task 8.1 (design D1, D5): what the coordinator does at start, before any
// of its steps run. The steps themselves, and the run as a whole, are covered by the step tests
// and by SetupCoordinatorRunTests.
public class SetupCoordinatorTests : IDisposable
{
    private readonly SetupRunKit _kit = new();

    public void Dispose() => _kit.Dispose();

    private static async Task<bool> CompletesQuicklyAsync(Task task) =>
        await Task.WhenAny(task, Task.Delay(TimeSpan.FromMilliseconds(300))) == task;

    // A hosted service's ExecuteAsync is started on the thread pool, so a stop that comes
    // straight after the start can cancel it before it has run at all. A test about stopping
    // waits until the coordinator has reached its waiting point.
    private Task WaitForLogAsync(string fragment) =>
        SetupRunKit.WaitUntilAsync(() => _kit.Log.Lines.Any(l => l.Contains(fragment)), $"the log line '{fragment}'");

    [Fact]
    public async Task AFinishedInstallHasNoSetup_TheCoordinatorReturnsAtOnce()
    {
        _kit.Tokens.Token = null;
        await _kit.Gate.MarkFinishedAsync();

        var coordinator = await _kit.StartAsync();

        Assert.True(await CompletesQuicklyAsync(coordinator.ExecuteTask!));
        Assert.Equal(0, _kit.Tokens.Reads);
    }

    [Theory]
    [InlineData("", "secret")]
    [InlineData("id", "")]
    [InlineData("id", "   ")]
    [InlineData("", "")]
    public async Task WithACredentialMissingItIdlesAndSendsNothing(string clientId, string secret)
    {
        _kit.MalOptions.ClientId = clientId;
        _kit.MalOptions.ClientSecret = secret;
        _kit.Mal.Edges.Add(SetupRunKit.Edge(1));
        var coordinator = await _kit.StartAsync();
        await WaitForLogAsync("Setup cannot start");

        Assert.False(await CompletesQuicklyAsync(coordinator.ExecuteTask!)); // still idling
        _kit.Trigger.Signal(); // not even a sign-in wakes it: fixing this needs a restart
        Assert.False(await CompletesQuicklyAsync(coordinator.ExecuteTask!));
        Assert.Equal(0, _kit.Tokens.Reads);
        Assert.Equal(0, _kit.Mal.ListReads);
        Assert.Equal(0, _kit.Mal.CountCalls);
        Assert.Empty(_kit.Airing.Batches);
        Assert.False(coordinator.GetSnapshot().ListReadThisRun);
    }

    [Fact]
    public async Task WithNoLoginItWaitsForTheTriggerAndStartsWhenALoginIsStored()
    {
        _kit.Tokens.Token = null;
        var coordinator = await _kit.StartAsync();

        await SetupRunKit.WaitUntilAsync(() => _kit.Tokens.Reads == 1, "the first look for a login"); // found none, and is waiting
        Assert.False(await CompletesQuicklyAsync(coordinator.ExecuteTask!));
        Assert.Equal(0, _kit.Mal.ListReads); // nothing is asked of MyAnimeList without a login

        _kit.Tokens.Token = FakeTokenStore.StoredToken(); // the OAuth callback stores the login, then signals
        _kit.Trigger.Signal();

        // Setup starts by itself: the list is read, and with nothing on it setup finishes.
        await SetupRunKit.WaitUntilAsync(() => _kit.Mal.ListReads == 1, "the list read to start");
        await SetupRunKit.WaitUntilAsync(() => _kit.Gate.IsFinished, "setup to finish");
    }

    [Fact]
    public async Task ASpuriousSignalWithStillNoLoginKeepsItWaiting()
    {
        _kit.Tokens.Token = null;
        var coordinator = await _kit.StartAsync();
        await SetupRunKit.WaitUntilAsync(() => _kit.Tokens.Reads == 1, "the first look for a login");

        _kit.Trigger.Signal();
        await SetupRunKit.WaitUntilAsync(() => _kit.Tokens.Reads == 2, "the second look");

        Assert.False(await CompletesQuicklyAsync(coordinator.ExecuteTask!));
        Assert.Equal(0, _kit.Mal.ListReads);
        _kit.Trigger.Signal();
        await SetupRunKit.WaitUntilAsync(() => _kit.Tokens.Reads == 3, "the third look"); // and each signal is checked again
    }

    [Fact]
    public async Task ALoginMyAnimeListHasRefusedStillStartsSetup_ReconnectIsAStepOfItsScreen()
    {
        _kit.Tokens.Token = FakeTokenStore.StoredToken(lostAt: DateTimeOffset.UtcNow);
        _kit.Mal.ListFailure = new MalAuthorizationRequiredException(); // the read finds the login refused
        var coordinator = await _kit.StartAsync();

        await SetupRunKit.WaitUntilAsync(() => _kit.State.Snapshot().WaitingForReconnect, "the list step to wait for Reconnect");

        Assert.False(await CompletesQuicklyAsync(coordinator.ExecuteTask!));
        Assert.Equal(1, _kit.Tokens.Reads); // it did not go back to waiting for a first sign-in
    }

    [Fact]
    public async Task StoppingWhileItWaitsForALoginEndsCleanly()
    {
        _kit.Tokens.Token = null;
        var coordinator = await _kit.StartAsync();
        await SetupRunKit.WaitUntilAsync(() => _kit.Tokens.Reads == 1, "the first look for a login");

        await coordinator.StopAsync(CancellationToken.None);

        Assert.Equal(TaskStatus.RanToCompletion, coordinator.ExecuteTask!.Status);
    }

    [Fact]
    public async Task StoppingWhileIdlingForACredentialEndsCleanly()
    {
        _kit.MalOptions.ClientId = "";
        _kit.MalOptions.ClientSecret = "";
        var coordinator = await _kit.StartAsync();
        await WaitForLogAsync("Setup cannot start");

        await coordinator.StopAsync(CancellationToken.None);

        Assert.Equal(TaskStatus.RanToCompletion, coordinator.ExecuteTask!.Status);
    }

    // --- what it exposes ---

    [Fact]
    public void RetryNowMakesBothServicesPausedQueuesDueAtOnce_AndLeavesAThrottleAlone()
    {
        var coordinator = _kit.NewCoordinator();
        for (var i = 0; i < ServiceHealth.DownAfterFailures; i++)
        {
            _kit.MalHealth.RecordTemporaryFailure();
            _kit.AniListHealth.RecordTemporaryFailure();
        }
        var throttledUntil = _kit.Now.AddSeconds(45);
        _kit.MalHealth.SetThrottledUntil(throttledUntil);
        Assert.True(_kit.MalHealth.NextTryAt > _kit.Now);

        coordinator.RetryNow();

        Assert.True(_kit.MalHealth.NextTryAt <= _kit.Now);
        Assert.True(_kit.AniListHealth.NextTryAt <= _kit.Now);
        Assert.Equal(throttledUntil, _kit.MalHealth.ThrottledUntil); // a wait the service asked for is not cut short
    }

    [Fact]
    public void RetryNowChangesNothingForAServiceThatIsUp()
    {
        var coordinator = _kit.NewCoordinator();

        coordinator.RetryNow();

        Assert.Null(_kit.MalHealth.NextTryAt);
        Assert.False(_kit.MalHealth.IsDown);
    }

    [Fact]
    public void IsDrainingAndTheSnapshotReadTheRunState()
    {
        var coordinator = _kit.NewCoordinator();
        Assert.False(coordinator.IsDraining);
        Assert.False(coordinator.GetSnapshot().AiringDraining);

        _kit.State.SetDraining(true);
        _kit.State.SetPhase(SetupStep.Airing, SetupStepPhase.Running);

        Assert.True(coordinator.IsDraining);
        var snapshot = coordinator.GetSnapshot();
        Assert.True(snapshot.AiringDraining);
        Assert.Equal(SetupStepPhase.Running, snapshot.Airing.Phase);
    }
}
