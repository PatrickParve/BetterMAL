using AnimeTracker.Api.Data;
using AnimeTracker.Api.Services.Setup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AnimeTracker.Api.Tests.Services.Setup;

/// <summary>A <see cref="SetupGate"/> for the tests of the jobs it holds back. Each
/// one reads and writes its <c>SetupStates</c> row in an in-memory database of its
/// own, so a job's fake scope factory needs nothing for it.</summary>
internal static class TestSetupGates
{
    /// <summary>A gate on an install where setup has not finished. Finishing it
    /// later, with <see cref="SetupGate.MarkFinishedAsync"/>, releases the jobs.</summary>
    public static SetupGate Unfinished()
    {
        var dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContext<AnimeTrackerDbContext>(o => o.UseInMemoryDatabase(dbName));
        return new SetupGate(services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>());
    }

    /// <summary>A gate on an install that finished setup before this process started.</summary>
    public static SetupGate Finished()
    {
        var gate = Unfinished();
        gate.MarkFinishedAsync().GetAwaiter().GetResult();
        return gate;
    }

    /// <summary>Polls until <paramref name="condition"/> holds, for at most five
    /// seconds, and fails the test rather than hanging when it never does.</summary>
    public static async Task WaitForAsync(Func<bool> condition)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition() && !cts.IsCancellationRequested)
            await Task.Delay(10, CancellationToken.None);

        Assert.True(condition());
    }

    /// <summary>Time for a job that was wrongly not held back to act: far longer
    /// than anything a job does the moment it starts.</summary>
    public static Task LetItRunAsync() => Task.Delay(150, CancellationToken.None);
}
