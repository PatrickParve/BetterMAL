using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Mal.Auth;

/// <summary>EF Core-backed token store. Scoped — safe to inject directly in
/// request-scoped code (controllers); singleton-ish callers (the pacing
/// handler's token provider, the background refresh service) must resolve it
/// from a fresh IServiceScope instead of holding it long-lived.</summary>
public class MalTokenStore(AnimeTrackerDbContext db) : IMalTokenStore
{
    public Task<OAuthToken?> GetAsync(CancellationToken ct = default) =>
        db.OAuthTokens.AsNoTracking().OrderByDescending(t => t.UpdatedAt).FirstOrDefaultAsync(ct);

    public async Task SaveAsync(string accessToken, string refreshToken, DateTimeOffset expiresAt, CancellationToken ct = default)
    {
        var existing = await db.OAuthTokens.OrderByDescending(t => t.UpdatedAt).FirstOrDefaultAsync(ct);
        var now = DateTimeOffset.UtcNow;

        if (existing is null)
        {
            db.OAuthTokens.Add(new OAuthToken
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = expiresAt,
                UpdatedAt = now,
            });
        }
        else
        {
            existing.AccessToken = accessToken;
            existing.RefreshToken = refreshToken;
            existing.ExpiresAt = expiresAt;
            existing.UpdatedAt = now;
        }

        await db.SaveChangesAsync(ct);
    }
}
