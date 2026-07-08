using System.Security.Cryptography;
using System.Threading;

namespace AnimeTracker.Api.Services.Mal.Auth;

/// <summary>Holds the single in-flight PKCE authorization attempt between
/// /api/mal-auth/start and /callback. A full session/cookie store would be
/// overkill — this is a single-user, single-machine, one-flow-at-a-time app.</summary>
public class MalOAuthStateStore
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    private readonly Lock _lock = new();
    private (string State, string CodeVerifier, DateTimeOffset CreatedAt)? _pending;

    public (string State, string CodeVerifier) CreatePending()
    {
        var state = GenerateToken(16);
        var verifier = GenerateToken(64);

        lock (_lock)
        {
            _pending = (state, verifier, DateTimeOffset.UtcNow);
        }

        return (state, verifier);
    }

    /// <summary>Returns the pending code_verifier for a matching, unexpired
    /// state, consuming it so it can't be replayed. Null if there is no
    /// matching pending request.</summary>
    public string? ConsumeVerifier(string state)
    {
        lock (_lock)
        {
            if (_pending is not { } pending)
                return null;

            _pending = null;

            if (pending.CreatedAt < DateTimeOffset.UtcNow - Ttl)
                return null;

            return pending.State == state ? pending.CodeVerifier : null;
        }
    }

    // PKCE code_verifier must use only unreserved characters [A-Za-z0-9-._~];
    // base64url output is a safe subset of that.
    private static string GenerateToken(int byteLength) =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(byteLength))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
}
