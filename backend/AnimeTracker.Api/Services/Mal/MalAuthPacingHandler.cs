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

        for (var attempt = 1; ; attempt++)
        {
            if (attempt > 1)
                current = await CloneAsync(request, ct);

            await ApplyAuthAsync(current, ct);
            await pacer.WaitAsync(ct);

            var response = await base.SendAsync(current, ct);

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

    private async Task ApplyAuthAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (!request.Options.TryGetValue(MalRequestOptions.AuthModeKey, out var mode))
            return;

        switch (mode)
        {
            case MalAuthMode.ClientId:
                request.Headers.Remove("X-MAL-Client-ID");
                request.Headers.Add("X-MAL-Client-ID", malOptions.Value.ClientId);
                break;

            case MalAuthMode.Bearer:
                var token = await tokenProvider.GetValidAccessTokenAsync(ct);
                if (token is null)
                    throw new MalAuthorizationRequiredException();
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                break;
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
