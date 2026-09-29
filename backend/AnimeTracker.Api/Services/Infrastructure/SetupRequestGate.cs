using AnimeTracker.Api.Services.Setup;

namespace AnimeTracker.Api.Services.Infrastructure;

/// <summary>Until first-run setup has finished, answers every <c>/api</c> request
/// but setup's own with <c>409 Conflict</c> and
/// <c>{ error, setupRequired: true }</c>, before any controller or service runs,
/// so a refused request starts no MyAnimeList or AniList work and changes
/// nothing (add-first-run-setup design D4; spec "The API refuses everything but
/// setup while setup runs").
/// <para>It is an allow-list, not a deny-list of the endpoints that start work.
/// Many reads start MyAnimeList or AniList work indirectly (a detail read's
/// visit refresh, a series read's build, a season read's refresh, search's live
/// query), and a deny-list would have to be kept in step with every endpoint
/// added later; one miss would let work compete with setup. Nothing in the
/// frontend calls the refused endpoints while setup shows, since the app shell
/// isn't mounted then.</para>
/// <para>409 is below 500, so the frontend's connection status counts it as the
/// backend being reachable. <c>/callback</c> is outside <c>/api</c> and is never
/// refused.</para></summary>
public static class SetupRequestGate
{
    // The routes setup's screen and its sign-in need. Matched on method and
    // path, ignoring case, as routing does.
    private static readonly (string Method, string Path)[] Allowed =
    [
        ("GET", "/api/setup/status"),
        ("POST", "/api/setup/retry-now"),
        ("GET", "/api/mal-auth/status"),
        ("GET", "/api/mal-auth/start"),
        ("GET", "/api/health"),
    ];

    public static async Task Invoke(HttpContext context, RequestDelegate next)
    {
        var request = context.Request;

        if (request.Path.StartsWithSegments("/api")
            && !IsAllowed(request)
            && !context.RequestServices.GetRequiredService<SetupGate>().IsFinished)
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            await context.Response.WriteAsJsonAsync(new { error = "Setup has not finished.", setupRequired = true });
            return;
        }

        await next(context);
    }

    private static bool IsAllowed(HttpRequest request)
    {
        foreach (var (method, path) in Allowed)
        {
            if (string.Equals(request.Method, method, StringComparison.OrdinalIgnoreCase)
                && request.Path.Equals(path, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
