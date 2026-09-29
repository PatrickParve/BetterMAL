using System.Text.Json;
using AnimeTracker.Api.Data;
using AnimeTracker.Api.Services.Infrastructure;
using AnimeTracker.Api.Services.Setup;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AnimeTracker.Api.Tests.Configuration;

// first-run-setup spec, "The API refuses everything but setup while setup runs",
// and design D4. Drives SetupRequestGate.Invoke directly against a
// DefaultHttpContext, in the shape of CrossSiteRequestGuardTests next to it.
// Asserting nextInvoked (not just the status code) is the point — it's what
// encodes "no controller ran, so no MyAnimeList or AniList work started".
public class SetupRequestGateTests
{
    private sealed class Fixture : IDisposable
    {
        private readonly ServiceProvider _provider;
        public SetupGate Gate { get; }

        public Fixture()
        {
            var services = new ServiceCollection();
            services.AddDbContext<AnimeTrackerDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
            services.AddSingleton<SetupGate>();
            _provider = services.BuildServiceProvider();
            Gate = _provider.GetRequiredService<SetupGate>();
        }

        public async Task<(HttpContext Context, bool NextInvoked)> SendAsync(string method, string path)
        {
            var context = new DefaultHttpContext { RequestServices = _provider };
            context.Request.Method = method;
            context.Request.Path = path;
            context.Response.Body = new MemoryStream();

            var nextInvoked = false;
            await SetupRequestGate.Invoke(context, _ => { nextInvoked = true; return Task.CompletedTask; });
            return (context, nextInvoked);
        }

        public void Dispose() => _provider.Dispose();
    }

    private static async Task<JsonElement> BodyAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var doc = await JsonDocument.ParseAsync(context.Response.Body);
        return doc.RootElement.Clone();
    }

    // The kinds of request the spec names, plus reads that start work indirectly
    // (a detail read's visit refresh, a series read's build) — each a real route.
    public static TheoryData<string, string> RefusedRequests => new()
    {
        { "GET", "/api/season/2026/fall" },
        { "POST", "/api/season/2026/fall/refresh" },
        { "GET", "/api/anime/16498" },
        { "PATCH", "/api/anime/16498/entry" },
        { "DELETE", "/api/anime/16498/entry" },
        { "GET", "/api/series/list" },
        { "GET", "/api/series/by-anime/16498" },
        { "POST", "/api/series/by-anime/16498/rebuild" },
        { "POST", "/api/sync/now" },
        { "GET", "/api/my-list" },
        { "GET", "/api/anime/search" },
        { "GET", "/api/app-status" },
        { "POST", "/api/airing/refresh-all" },
    };

    [Theory]
    [MemberData(nameof(RefusedRequests))]
    public async Task ARequestOutsideTheAllowListIsRefusedBeforeAnythingRuns(string method, string path)
    {
        using var fixture = new Fixture();

        var (context, nextInvoked) = await fixture.SendAsync(method, path);

        Assert.False(nextInvoked);
        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
        Assert.True(context.Response.StatusCode < 500);
    }

    [Fact]
    public async Task TheRefusalSaysSetupHasNotFinished()
    {
        using var fixture = new Fixture();

        var (context, _) = await fixture.SendAsync("GET", "/api/season/2026/fall");

        var body = await BodyAsync(context);
        Assert.Equal("Setup has not finished.", body.GetProperty("error").GetString());
        Assert.True(body.GetProperty("setupRequired").GetBoolean());
        Assert.StartsWith("application/json", context.Response.ContentType);
    }

    [Theory]
    [InlineData("GET", "/api/setup/status")]
    [InlineData("POST", "/api/setup/retry-now")]
    [InlineData("GET", "/api/mal-auth/status")]
    [InlineData("GET", "/api/mal-auth/start")]
    [InlineData("GET", "/api/health")]
    public async Task EachAllowedRoutePasses(string method, string path)
    {
        using var fixture = new Fixture();

        var (context, nextInvoked) = await fixture.SendAsync(method, path);

        Assert.True(nextInvoked);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Theory]
    [InlineData("/API/HEALTH")]
    [InlineData("/Api/Setup/Status")]
    public async Task TheAllowListIsMatchedIgnoringCase(string path)
    {
        using var fixture = new Fixture();

        var (_, nextInvoked) = await fixture.SendAsync("GET", path);

        Assert.True(nextInvoked);
    }

    [Theory]
    [InlineData("POST", "/api/setup/status")]
    [InlineData("GET", "/api/setup/retry-now")]
    [InlineData("POST", "/api/mal-auth/status")]
    [InlineData("POST", "/api/mal-auth/start")]
    [InlineData("DELETE", "/api/health")]
    public async Task AnAllowedRouteWithTheWrongMethodIsRefused(string method, string path)
    {
        using var fixture = new Fixture();

        var (context, nextInvoked) = await fixture.SendAsync(method, path);

        Assert.False(nextInvoked);
        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
    }

    [Theory]
    [InlineData("/api")]
    [InlineData("/api/health/extra")]
    [InlineData("/api/setup/statuses")]
    [InlineData("/api/setup/status/1")]
    [InlineData("/api/mal-auth/start/again")]
    public async Task OnlyTheAllowedPathsThemselvesPass(string path)
    {
        using var fixture = new Fixture();

        var (context, nextInvoked) = await fixture.SendAsync("GET", path);

        Assert.False(nextInvoked);
        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
    }

    [Theory]
    [InlineData("GET", "/callback")]
    [InlineData("GET", "/openapi/v1.json")]
    [InlineData("GET", "/apifoo/notapi")]
    public async Task AnythingOutsideApiPasses(string method, string path)
    {
        using var fixture = new Fixture();

        var (context, nextInvoked) = await fixture.SendAsync(method, path);

        Assert.True(nextInvoked);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(RefusedRequests))]
    public async Task OnceSetupHasFinishedEverythingPasses(string method, string path)
    {
        using var fixture = new Fixture();
        await fixture.Gate.MarkFinishedAsync();

        var (context, nextInvoked) = await fixture.SendAsync(method, path);

        Assert.True(nextInvoked);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task TheGateOpensInThisProcessTheMomentSetupFinishes()
    {
        using var fixture = new Fixture();
        var (_, before) = await fixture.SendAsync("GET", "/api/my-list");

        await fixture.Gate.MarkFinishedAsync();
        var (_, after) = await fixture.SendAsync("GET", "/api/my-list");

        Assert.False(before);
        Assert.True(after);
    }
}
