using AnimeTracker.Api.Services.Mal;

namespace AnimeTracker.Api.Services.Jobs;

/// <summary>Maps a job's uncaught exception to the plain-word reason the
/// Settings page shows (design.md D3). Callers are expected to have already
/// filtered out their own shutdown cancellation (an OperationCanceledException
/// from the host's stopping token) before reaching here — a TaskCanceledException
/// that does arrive here is therefore a timeout, not our own cancellation.</summary>
public static class JobFailure
{
    public static string Describe(Exception ex, string service) => ex switch
    {
        MalAuthorizationRequiredException => "The connection to MyAnimeList was lost.",
        HttpRequestException => $"{service} couldn't be reached.",
        TaskCanceledException => $"{service} couldn't be reached.",
        _ => "Something went wrong — see the backend logs.",
    };
}
