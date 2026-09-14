using System.Net;
using System.Net.Http;

namespace AnimeTracker.Api.Services.Metadata;

/// <summary>How a failed MAL call should be treated by the scheduled refresh
/// job (design D2). <see cref="Outage"/> means MAL is down or pushing back
/// and the pass should end; <see cref="NotFound"/> means MAL has no such
/// anime; <see cref="Other"/> is everything else, which skips just that
/// anime.</summary>
public enum MalCallFailureKind
{
    Outage,
    NotFound,
    Other,
}

/// <summary>Classifies a MAL call failure for the scheduled refresh job
/// alone (design D2) — not a general-purpose classifier for every
/// <see cref="AnimeTracker.Api.Services.Mal.IMalClient"/> caller, which stays
/// out of scope.</summary>
public static class MalCallFailure
{
    /// <summary>Callers must rethrow a cancellation of their own token
    /// (<c>catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }</c>)
    /// ahead of their general catch, so the only cancellation that reaches
    /// here is an <c>HttpClient</c> timeout. A 403 here has already outlasted
    /// <c>MalAuthPacingHandler</c>'s five retries, so it means MAL itself is
    /// still throttling.</summary>
    public static MalCallFailureKind Classify(Exception ex) => ex switch
    {
        AnimeMetadataNotFoundException => MalCallFailureKind.NotFound,
        HttpRequestException { StatusCode: HttpStatusCode.NotFound } => MalCallFailureKind.NotFound,
        HttpRequestException { StatusCode: null } => MalCallFailureKind.Outage,
        HttpRequestException { StatusCode: HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests } => MalCallFailureKind.Outage,
        HttpRequestException { StatusCode: { } status } when (int)status >= 500 => MalCallFailureKind.Outage,
        HttpIOException => MalCallFailureKind.Outage,
        OperationCanceledException => MalCallFailureKind.Outage,
        _ => MalCallFailureKind.Other,
    };
}
