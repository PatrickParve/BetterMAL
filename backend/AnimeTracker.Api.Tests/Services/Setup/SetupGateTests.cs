using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Setup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AnimeTracker.Api.Tests.Services.Setup;

// first-run-setup spec, "Setup finishing is recorded once", and design D2/D3:
// the gate is loaded at startup, finishes once, and its one stored value
// survives a restart (a second gate on the same database).
public class SetupGateTests
{
    private static ServiceProvider BuildProvider(string dbName)
    {
        var services = new ServiceCollection();
        services.AddDbContext<AnimeTrackerDbContext>(o => o.UseInMemoryDatabase(dbName));
        return services.BuildServiceProvider();
    }

    private static SetupGate NewGate(ServiceProvider provider) =>
        new(provider.GetRequiredService<IServiceScopeFactory>());

    private static async Task<List<SetupState>> StoredRowsAsync(ServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AnimeTrackerDbContext>().SetupStates.AsNoTracking().ToListAsync();
    }

    [Fact]
    public async Task AnUnfinishedGatesWhenFinishedIsPending()
    {
        using var provider = BuildProvider(Guid.NewGuid().ToString());
        var gate = NewGate(provider);

        await gate.LoadAsync();

        Assert.False(gate.IsFinished);
        Assert.False(gate.WhenFinished.IsCompleted);
    }

    [Fact]
    public async Task MarkFinishedStoresTheRowAndCompletesTheTask()
    {
        using var provider = BuildProvider(Guid.NewGuid().ToString());
        var gate = NewGate(provider);
        await gate.LoadAsync();
        var before = DateTimeOffset.UtcNow;

        await gate.MarkFinishedAsync();

        Assert.True(gate.IsFinished);
        Assert.True(gate.WhenFinished.IsCompletedSuccessfully);
        var row = Assert.Single(await StoredRowsAsync(provider));
        Assert.Equal(1, row.Id);
        Assert.NotNull(row.CompletedAt);
        Assert.InRange(row.CompletedAt!.Value, before, DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task ASecondCallChangesNothing()
    {
        using var provider = BuildProvider(Guid.NewGuid().ToString());
        var gate = NewGate(provider);
        await gate.MarkFinishedAsync();
        var first = Assert.Single(await StoredRowsAsync(provider)).CompletedAt;

        await Task.Delay(20);
        await gate.MarkFinishedAsync();

        var row = Assert.Single(await StoredRowsAsync(provider));
        Assert.Equal(first, row.CompletedAt);
        Assert.True(gate.IsFinished);
    }

    [Fact]
    public async Task ConcurrentCallsWriteOneRow()
    {
        using var provider = BuildProvider(Guid.NewGuid().ToString());
        var gate = NewGate(provider);

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => gate.MarkFinishedAsync()));

        Assert.Single(await StoredRowsAsync(provider));
        Assert.True(gate.IsFinished);
    }

    [Fact]
    public async Task LoadingAStoredFinishedRowStartsCompleted()
    {
        var dbName = Guid.NewGuid().ToString();
        using var provider = BuildProvider(dbName);
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AnimeTrackerDbContext>();
            db.SetupStates.Add(new SetupState { Id = 1, CompletedAt = DateTimeOffset.UtcNow.AddDays(-3) });
            await db.SaveChangesAsync();
        }
        var gate = NewGate(provider);

        await gate.LoadAsync();

        Assert.True(gate.IsFinished);
        Assert.True(gate.WhenFinished.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task AFinishSurvivesARestart()
    {
        using var provider = BuildProvider(Guid.NewGuid().ToString());
        await NewGate(provider).MarkFinishedAsync();

        // A new process is a new gate over the same database.
        var afterRestart = NewGate(provider);
        Assert.False(afterRestart.IsFinished);
        await afterRestart.LoadAsync();

        Assert.True(afterRestart.IsFinished);
    }

    [Fact]
    public async Task ARowWithNoInstantLoadsAsUnfinishedAndIsCompletedInPlace()
    {
        using var provider = BuildProvider(Guid.NewGuid().ToString());
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AnimeTrackerDbContext>();
            db.SetupStates.Add(new SetupState { Id = 1, CompletedAt = null });
            await db.SaveChangesAsync();
        }
        var gate = NewGate(provider);
        await gate.LoadAsync();
        Assert.False(gate.IsFinished);

        await gate.MarkFinishedAsync();

        var row = Assert.Single(await StoredRowsAsync(provider));
        Assert.NotNull(row.CompletedAt);
        Assert.True(gate.IsFinished);
    }

    [Fact]
    public async Task AWaiterIsReleasedWhenTheGateFinishes()
    {
        using var provider = BuildProvider(Guid.NewGuid().ToString());
        var gate = NewGate(provider);
        var waiter = gate.WhenFinished.ContinueWith(_ => "released", TaskScheduler.Default);
        Assert.False(waiter.IsCompleted);

        await gate.MarkFinishedAsync();

        Assert.Equal("released", await waiter.WaitAsync(TimeSpan.FromSeconds(5)));
    }
}
