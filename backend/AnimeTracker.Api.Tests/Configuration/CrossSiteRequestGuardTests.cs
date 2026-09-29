using System.Reflection;
using AnimeTracker.Api.Controllers;
using AnimeTracker.Api.Services.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Tests.Configuration;

// design.md D7: drives CrossSiteRequestGuard.Invoke directly against a
// DefaultHttpContext, in the shape of HostFilteringTests next to it.
// Asserting nextInvoked (not just the status code) is the point — it's what
// encodes "the controller never ran" (design.md D6/D7).
public class CrossSiteRequestGuardTests
{
    [Fact]
    public async Task AMutationWithTheHeaderReachesTheNextDelegate()
    {
        var nextInvoked = false;
        var context = CreateContext("POST", "/api/sync/held/accept", CrossSiteRequestGuard.HeaderValue);

        await CrossSiteRequestGuard.Invoke(context, _ => { nextInvoked = true; return Task.CompletedTask; });

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.True(nextInvoked);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-betterMAL")]
    public async Task AMutationWithoutTheHeaderIsRefusedBeforeTheNextDelegateRuns(string? headerValue)
    {
        var nextInvoked = false;
        var context = CreateContext("POST", "/api/sync/held/accept", headerValue);

        await CrossSiteRequestGuard.Invoke(context, _ => { nextInvoked = true; return Task.CompletedTask; });

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.False(nextInvoked);
    }

    [Fact]
    public async Task AGetUnderApiReachesTheNextDelegateWithNoHeader()
    {
        var nextInvoked = false;
        var context = CreateContext("GET", "/api/sync/held", headerValue: null);

        await CrossSiteRequestGuard.Invoke(context, _ => { nextInvoked = true; return Task.CompletedTask; });

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.True(nextInvoked);
    }

    [Fact]
    public async Task AnUppercasePathIsStillMatched()
    {
        var nextInvoked = false;
        var context = CreateContext("POST", "/API/SYNC/HELD/ACCEPT", headerValue: null);

        await CrossSiteRequestGuard.Invoke(context, _ => { nextInvoked = true; return Task.CompletedTask; });

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.False(nextInvoked);
    }

    [Theory]
    [InlineData("POST", "/callback")]
    [InlineData("POST", "/apifoo/notapi")]
    [InlineData("DELETE", "/apifoo/notapi")]
    public async Task ANonApiPathPassesThroughRegardlessOfMethod(string method, string path)
    {
        var nextInvoked = false;
        var context = CreateContext(method, path, headerValue: null);

        await CrossSiteRequestGuard.Invoke(context, _ => { nextInvoked = true; return Task.CompletedTask; });

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.True(nextInvoked);
    }

    // design.md D11: the GET exemption rests on no GET under /api pushing a
    // change to MyAnimeList, deleting a list entry, or discarding unsent
    // local edits (spec requirement, task 3.7). Reflecting over the live
    // route table, rather than restating it, is what catches a new or
    // renamed GET before it silently joins the exemption.
    [Fact]
    public void TheGetRoutesUnderApiMatchTheReviewedList()
    {
        var actual = DiscoverGetRoutesUnderApi();

        var added = actual.Except(ExpectedGetRoutesUnderApi).OrderBy(r => r, StringComparer.Ordinal).ToList();
        var removed = ExpectedGetRoutesUnderApi.Except(actual).OrderBy(r => r, StringComparer.Ordinal).ToList();

        Assert.True(added.Count == 0 && removed.Count == 0,
            "The GET routes under /api changed. This list exists so a new or renamed read endpoint is " +
            "reviewed for destructive effects (pushing a change to MyAnimeList, deleting a list entry, or " +
            "discarding unsent local edits) before it joins the CSRF guard's GET exemption — see " +
            "design.md D10/D11. Update ExpectedGetRoutesUnderApi once the review is done.\n" +
            $"Added: [{string.Join(", ", added)}]\n" +
            $"Removed: [{string.Join(", ", removed)}]");
    }

    private static List<string> DiscoverGetRoutesUnderApi()
    {
        var routes = new List<string>();

        foreach (var controllerType in typeof(MyListController).Assembly.GetTypes())
        {
            if (!typeof(ControllerBase).IsAssignableFrom(controllerType) || controllerType.IsAbstract)
                continue;

            var controllerRoute = controllerType.GetCustomAttribute<RouteAttribute>()?.Template;

            foreach (var method in controllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                var getAttribute = method.GetCustomAttribute<HttpGetAttribute>();
                if (getAttribute is null)
                    continue;

                var template = string.IsNullOrEmpty(getAttribute.Template) ? controllerRoute : getAttribute.Template;
                if (template is not null && template.StartsWith("api/", StringComparison.Ordinal))
                    routes.Add(template);
            }
        }

        return routes;
    }

    // Reviewed at 0cd2d77 (task 3.7): every route below is a plain read
    // except the ones marked "writes", which fill a cache or reconcile state
    // as a documented side effect (design D10 — permitted by the spec).
    // None of them, marked or not, pushes a change to MyAnimeList, deletes a
    // list entry, or discards an unsent local edit.
    private static readonly string[] ExpectedGetRoutesUnderApi =
    [
        "api/airing",
        "api/anime/{animeId:int}",
        "api/anime/search",                  // writes: may enqueue a background series build (AnimeSearchService.ScheduleSeriesBuildForTopMatch)
        "api/anime/search/page",             // writes: may enqueue a background series build (AnimeSearchService.ScheduleSeriesBuildForTopMatch)
        "api/app-status",
        "api/dashboard",
        "api/health",
        "api/mal-auth/start",
        "api/mal-auth/status",
        "api/my-list",
        "api/my-list/backup",
        "api/profile",
        "api/profile/activity",
        "api/profile/rewatched",
        "api/profile/rewatched-series",
        "api/profile/time-spent-series",
        "api/profile/top-anime",
        "api/profile/top-series",            // writes: enqueues a background series build (ProfileService/GetTopSeriesSectionAsync)
        "api/rankings",
        "api/recap",
        "api/recap/availability",
        "api/season/bounds",
        "api/season/{year:int}/{season}",
        "api/series/by-anime/{animeId:int}", // writes: builds/refreshes the series from MAL when missing, partial, or stale (SeriesService.GetSeriesAsync)
        "api/series/list",
        "api/setup/status",                  // read-only: counts and the coordinator's in-memory state, no outside call (SetupStatusService)
        "api/sync/held",                  // writes: clears held changes MAL already agrees with (HeldChangeService.GetHeldAsync, design D8a)
        "api/sync/reconcile/pending",
        "api/top-anime",                      // cache-only read; the refresh that used to run inline is now the separate POST api/top-anime/refresh
        "api/transfer/export",
        "api/transfer/import/status",
        "api/updates/history",
        "api/updates/recent",
        "api/year/{year:int}",
    ];

    private static DefaultHttpContext CreateContext(string method, string path, string? headerValue)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        if (headerValue is not null)
            context.Request.Headers[CrossSiteRequestGuard.HeaderName] = headerValue;
        return context;
    }
}
