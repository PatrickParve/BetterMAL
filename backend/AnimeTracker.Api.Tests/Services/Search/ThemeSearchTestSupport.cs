using System.Reflection;
using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Search;
using AnimeTracker.Api.Tests.Services.AnimeThemes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AnimeTracker.Api.Tests.Services.Search;

/// <summary>Shared by the OP/ED search tests: a scope factory that hands out one in-memory
/// context (how the singleton index reaches the catalogue in the tests, as it reaches a scoped
/// one in the app), a context builder, and a seed.</summary>
internal static class ThemeSearchTestKit
{
    public static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    /// <summary>Stores the rows, and marks the catalogue as having finished a refresh unless told
    /// otherwise: that is what makes it ready.</summary>
    public static async Task SeedAsync(AnimeTrackerDbContext db, IEnumerable<AnimeTheme> rows, bool ready = true)
    {
        if (ready)
            StoredThemes.MarkSynced(db);
        db.AnimeThemes.AddRange(rows);
        await db.SaveChangesAsync();
    }

    public sealed class DbScopeFactory(AnimeTrackerDbContext db) : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new Scope(db);

        private sealed class Scope(AnimeTrackerDbContext db) : IServiceScope
        {
            public IServiceProvider ServiceProvider { get; } = new Provider(db);
            public void Dispose() { }
        }

        private sealed class Provider(AnimeTrackerDbContext db) : IServiceProvider
        {
            public object? GetService(Type serviceType) => serviceType == typeof(AnimeTrackerDbContext) ? db : null;
        }
    }
}

/// <summary>Stands in for a service the OP/ED search must never call: every call is recorded and
/// then refused. Made for any public interface, so the clients need no hand-written fake.</summary>
public class Tripwire : DispatchProxy
{
    public List<string> Calls { get; } = [];

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        Calls.Add($"{targetMethod?.DeclaringType?.Name}.{targetMethod?.Name}");
        throw new InvalidOperationException($"The OP/ED search must not call {targetMethod?.DeclaringType?.Name}.{targetMethod?.Name}.");
    }

    public static T Create<T>(out Tripwire tripwire) where T : class
    {
        var proxy = Create<T, Tripwire>();
        tripwire = (Tripwire)(object)proxy;
        return proxy;
    }
}

/// <summary>Stands in for the in-memory catalogue index where a test is about something else (the
/// refresh that invalidates it), and counts the invalidations. <c>OnInvalidate</c> runs at the moment
/// of each, so a test can look at what had been committed by then.</summary>
internal sealed class FakeThemeSearchIndex : IThemeSearchIndex
{
    public int InvalidateCallCount { get; private set; }
    public Action? OnInvalidate { get; set; }

    public Task<IReadOnlyList<ThemeSearchEntry>> GetAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ThemeSearchEntry>>([]);

    public void Invalidate()
    {
        InvalidateCallCount++;
        OnInvalidate?.Invoke();
    }
}
