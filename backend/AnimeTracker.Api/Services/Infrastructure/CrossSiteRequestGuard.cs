namespace AnimeTracker.Api.Services.Infrastructure;

/// <summary>Refuses any non-<c>GET</c> request under <c>/api</c> that does not carry
/// <c>X-Requested-With: BetterMAL</c>, closing CSRF with no server-side state
/// (design.md D2). A cross-site <c>fetch</c> that sets a custom header requires a
/// preflight, and no <c>Access-Control-Allow-Origin</c> is ever sent, so the header
/// is only ever present on a request the app itself made.</summary>
public static class CrossSiteRequestGuard
{
    public const string HeaderName = "X-Requested-With";
    public const string HeaderValue = "BetterMAL";

    public static async Task Invoke(HttpContext context, RequestDelegate next)
    {
        var request = context.Request;
        var isGuarded = !HttpMethods.IsGet(request.Method) && request.Path.StartsWithSegments("/api");

        if (isGuarded && !HasValidHeader(request))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { error = "This request must come from the BetterMAL app." });
            return;
        }

        await next(context);
    }

    private static bool HasValidHeader(HttpRequest request)
    {
        return request.Headers.TryGetValue(HeaderName, out var values)
            && values.Any(value => string.Equals(value, HeaderValue, StringComparison.Ordinal));
    }
}
