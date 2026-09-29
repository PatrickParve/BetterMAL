using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Services.Mal.Auth;

/// <summary>How MyAnimeList's redirect to /callback ended. An unknown, expired
/// or already-used state is an expected outcome — a restart during sign-in, or
/// the ten-minute limit — so it is told apart from a completed exchange rather
/// than thrown, and the controller can say "expired". A code exchange that
/// fails still throws.</summary>
public abstract record MalCallbackResult
{
    public sealed record Completed(OAuthToken Token) : MalCallbackResult;
    public sealed record StateRejected : MalCallbackResult;
}
