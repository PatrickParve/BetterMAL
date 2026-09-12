using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Services.Mal.Auth;

/// <summary>What MyAnimeList's token endpoint answered to a refresh attempt —
/// tells a refusal (this login won't be accepted again until re-authorized)
/// apart from an outage (nothing to record; try again next time it's needed)
/// (design.md D11).</summary>
public abstract record MalRefreshResult
{
    public sealed record Refreshed(OAuthToken Token) : MalRefreshResult;
    public sealed record Refused(int StatusCode, string? Error) : MalRefreshResult;
    public sealed record Unavailable(string Detail) : MalRefreshResult;
}
