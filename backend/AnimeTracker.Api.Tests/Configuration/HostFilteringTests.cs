using Microsoft.AspNetCore.HostFiltering;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AnimeTracker.Api.Tests.Configuration;

// design.md D5: drives the real HostFilteringMiddleware, fed by the shipped
// appsettings.json rather than a restatement of it, so this fails against
// the old "*" and only passes once AllowedHosts is loopback-only.
public class HostFilteringTests
{
    private static readonly string AppSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

    [Fact]
    public void TheApisAppsettingsIsCopiedToTheTestOutput()
    {
        Assert.True(File.Exists(AppSettingsPath),
            $"Expected the API's appsettings.json at {AppSettingsPath} — without it this suite would silently test nothing.");
    }

    [Theory]
    [InlineData("evil.example")]
    [InlineData("attacker.test:5050")]
    public async Task AForeignHostIsRefusedBeforeTheNextDelegateRuns(string host)
    {
        var nextInvoked = false;
        var middleware = CreateMiddleware(_ => { nextInvoked = true; return Task.CompletedTask; });
        var context = CreateContext(host);

        await middleware.Invoke(context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.False(nextInvoked);
    }

    [Theory]
    [InlineData("localhost")]
    [InlineData("localhost:5050")]
    [InlineData("127.0.0.1:5050")]
    public async Task ALoopbackHostReachesTheNextDelegate(string host)
    {
        var nextInvoked = false;
        var middleware = CreateMiddleware(_ => { nextInvoked = true; return Task.CompletedTask; });
        var context = CreateContext(host);

        await middleware.Invoke(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.True(nextInvoked);
    }

    private static HostFilteringMiddleware CreateMiddleware(RequestDelegate next)
    {
        var configuration = new ConfigurationBuilder().AddJsonFile(AppSettingsPath, optional: false).Build();
        var allowedHosts = configuration["AllowedHosts"]?.Split(';', StringSplitOptions.RemoveEmptyEntries) ?? [];
        var options = new HostFilteringOptions { AllowedHosts = allowedHosts };

        return new HostFilteringMiddleware(next, NullLogger<HostFilteringMiddleware>.Instance, new StaticOptionsMonitor<HostFilteringOptions>(options));
    }

    private static DefaultHttpContext CreateContext(string host)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Host = host;
        return context;
    }
}

// HostFilteringMiddleware's third constructor argument is
// IOptionsMonitor<HostFilteringOptions>, not IOptions<> — Options.Create(...)
// does not compile against it.
file sealed class StaticOptionsMonitor<T>(T currentValue) : IOptionsMonitor<T>
{
    public T CurrentValue { get; } = currentValue;
    public T Get(string? name) => CurrentValue;
    public IDisposable OnChange(Action<T, string> listener) => NoopDisposable.Instance;

    private sealed class NoopDisposable : IDisposable
    {
        public static readonly NoopDisposable Instance = new();
        public void Dispose() { }
    }
}
