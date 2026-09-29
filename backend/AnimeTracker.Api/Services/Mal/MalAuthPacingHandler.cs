using System.Net;
using System.Net.Http.Headers;
using AnimeTracker.Api.Services.Mal.Auth;
using AnimeTracker.Api.Services.Setup;
using Microsoft.Extensions.Options;

namespace AnimeTracker.Api.Services.Mal;

/// <summary>Central place for MAL cross-cutting HTTP concerns: attaches the
/// right auth header per request (X-MAL-Client-ID or bearer, per
/// MalRequestOptions.AuthModeKey), paces every dispatch, and treats 403
/// (undocumented burst throttling) as backoff-and-retry rather than failure.
/// It is also where MyAnimeList's health is measured (<see cref="MalServiceHealth"/>,
/// add-first-run-setup design D12): the moment a 403 backoff begins and ends, and
/// whether each request ended in an answer or in a temporary failure.</summary>
public class MalAuthPacingHandler(
    IOptions<MalOptions> malOptions,
    IMalTokenProvider tokenProvider,
    MalRequestPacer pacer,
    MalServiceHealth health,
    ILogger<MalAuthPacingHandler> logger,
    TimeSpan? initialBackoff = null) : DelegatingHandler
{
    private const int MaxAttempts = 5;
    private readonly TimeSpan _initialBackoff = initialBackoff ?? TimeSpan.FromSeconds(2);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var response = await SendWithBackoffAsync(request, ct);
        RecordOutcome(response);
        return response;
    }

    /// <summary>What a finished request says about MyAnimeList. Only a response
    /// reaches here — an exception was recorded where it was thrown
    /// (<see cref="DispatchAsync"/>).</summary>
    private void RecordOutcome(HttpResponseMessage response)
    {
        // The only 403 that gets this far outlasted every retry: MAL is still
        // throttling. Its throttle window is left to run out, since it is only
        // cleared by an answer that is not a throttle.
        if (response.StatusCode == HttpStatusCode.Forbidden || (int)response.StatusCode >= 500)
        {
            health.RecordTemporaryFailure();
            return;
        }

        // Any other answer, a 404 or another 4xx included, shows MAL is up and
        // not limiting us.
        health.ClearThrottle();
        health.RecordSuccess();
    }

    /// <summary>The one place a request leaves for MAL. A network error or a
    /// timeout is a temporary failure of the service, recorded here so the
    /// pacer, token and backoff work around it isn't mistaken for one.
    /// <para>A caller's cancellation looks the same as a timeout from this
    /// side: <c>HttpClient</c> hands the handler a token linked to both, so a
    /// cancellation can't be told from its timeout here. Counting it errs
    /// towards a strike, and one takes three in a row with no answer between to
    /// matter.</para></summary>
    private async Task<HttpResponseMessage> DispatchAsync(HttpRequestMessage request, CancellationToken ct)
    {
        try
        {
            return await base.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or HttpIOException or OperationCanceledException)
        {
            health.RecordTemporaryFailure();
            throw;
        }
    }

    private async Task<HttpResponseMessage> SendWithBackoffAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var delay = _initialBackoff;
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

            var response = await DispatchAsync(current, ct);

            if (response.StatusCode == HttpStatusCode.Unauthorized && bearerToken is not null && !authRetried)
            {
                authRetried = true;
                if (!await TryRefreshAfterRejectionAsync(bearerToken, response, ct))
                    return response; // unavailable — the caller fails as it does today

                response.Dispose();
                current = await CloneAsync(request, ct);
                await ApplyAuthAsync(current, ct); // re-reads the now-refreshed token
                await pacer.WaitAsync(ct);
                response = await DispatchAsync(current, ct);
                // A 401 on this retried request is returned as it is.
            }

            if (response.StatusCode != HttpStatusCode.Forbidden || attempt >= MaxAttempts)
                return response;

            logger.LogWarning(
                "MAL API returned 403 (throttled) for {Method} {Uri}; backing off {DelaySeconds}s (attempt {Attempt}/{MaxAttempts})",
                request.Method, request.RequestUri, delay.TotalSeconds, attempt, MaxAttempts);

            response.Dispose();
            // Published before the wait, so a reader sees it while it runs.
            health.SetThrottledUntil(DateTimeOffset.UtcNow + delay);
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
