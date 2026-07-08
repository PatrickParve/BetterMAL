namespace AnimeTracker.Api.Services.Mal;

/// <summary>Thrown when a user-endpoint call is attempted without a valid access
/// token. The request is never sent — the caller must surface an
/// authorization-required state (e.g. prompt re-authorization).</summary>
public class MalAuthorizationRequiredException()
    : Exception("MAL authorization required — no valid access token is available.");
