using System.Net;
using System.Net.Http.Headers;
using AnimeTracker.Api.Services.Mal.Auth;
using Microsoft.Extensions.Options;

namespace AnimeTracker.Api.Services.Mal;

/// <summary>Central place for MAL cross-cutting HTTP concerns: attaches the
/// right auth header per request (X-MAL-Client-ID or bearer, per
/// MalRequestOptions.AuthModeKey), paces every dispatch, and treats 403
/// (undocumented burst throttling) as backoff-and-retry rather than failure.</summary>
public class MalAuthPacingHandler(
    IOptions<MalOptions> malOptions,
    IMalTokenProvider tokenProvider,
    MalRequestPacer pacer,
    ILogger<MalAuthPacingHandler> logger) : DelegatingHandler
{
    private const int MaxAttempts = 5;
    private static readonly TimeSpan InitialBackoff = TimeSpan.FromSeconds(2);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var delay = InitialBackoff;
        var current = request;
        // Separate from the 403 backoff loop below (design.md D12): a 401 on
        // a Bearer request gets exactly one forced refresh-and-retry, no
        // matter how many 403 attempts happen before or after it.
        var authRetried = false;

        for (var attempt = 1; ; attempt++)
        {
            if (attempt > 1)
                current = await CloneAsync(request, ct);

            var bearerToken = await ApplyAuthAsync(current, ct);
            await pacer.WaitAsync(ct);

            var response = await base.SendAsync(current, ct);

            if (response.StatusCode == HttpStatusCode.Unauthorized && bearerToken is not null && !authRetried)
            {
                authRetried = true;
                if (!await TryRefreshAfterRejectionAsync(bearerToken, response, ct))
                    return response; // unavailable — the caller fails as it does today

                response.Dispose();
                current = await CloneAsync(request, ct);
                await ApplyAuthAsync(current, ct); // re-reads the now-refreshed token
                await pacer.WaitAsync(ct);
                response = await base.SendAsync(current, ct);
                // A 401 on this retried request is returned as it is.
            }

            if (response.StatusCode != HttpStatusCode.Forbidden || attempt >= MaxAttempts)
                return response;

            logger.LogWarning(
                "MAL API returned 403 (throttled) for {Method} {Uri}; backing off {DelaySeconds}s (attempt {Attempt}/{MaxAttempts})",
                request.Method, request.RequestUri, delay.TotalSeconds, attempt, MaxAttempts);

            response.Dispose();
            await Task.Delay(delay, ct);
            delay *= 2;
        }
    }

    /// <summary>Returns the Bearer token applied, so a later 401 can be
    /// matched against it; null for a client-id request or no auth mode.</summary>
    private async Task<string?> ApplyAuthAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (!request.Options.TryGetValue(MalRequestOptions.AuthModeKey, out var mode))
            return null;

        switch (mode)
        {
            case MalAuthMode.ClientId:
                request.Headers.Remove("X-MAL-Client-ID");
                request.Headers.Add("X-MAL-Client-ID", malOptions.Value.ClientId);
                return null;

            case MalAuthMode.Bearer:
                var token = await tokenProvider.GetValidAccessTokenAsync(ct);
                if (token is null)
                    throw new MalAuthorizationRequiredException();
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                return token;

            default:
                return null;
        }
    }

    /// <summary>True means the caller should clone and resend the request
    /// with the now-refreshed token; false means the original 401 response
    /// should be returned as an outage (design.md D12).</summary>
    private async Task<bool> TryRefreshAfterRejectionAsync(string rejectedToken, HttpResponseMessage rejection, CancellationToken ct)
    {
        var outcome = await tokenProvider.RefreshAfterRejectionAsync(rejectedToken, ct);

        switch (outcome)
        {
            case MalRefreshResult.Refreshed:
                return true;

            case MalRefreshResult.Refused:
                rejection.Dispose();
                throw new MalAuthorizationRequiredException();

            default: // Unavailable
                return false;
        }
    }

    private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage req, CancellationToken ct)
    {
        var clone = new HttpRequestMessage(req.Method, req.RequestUri) { Version = req.Version };

        foreach (var header in req.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        if (req.Content is not null)
        {
            var bytes = await req.Content.ReadAsByteArrayAsync(ct);
            var contentClone = new ByteArrayContent(bytes);
            foreach (var header in req.Content.Headers)
                contentClone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            clone.Content = contentClone;
        }

        var cloneOptions = (IDictionary<string, object?>)clone.Options;
        foreach (var kvp in (IDictionary<string, object?>)req.Options)
            cloneOptions[kvp.Key] = kvp.Value;

        return clone;
    }
}
