namespace AnimeTracker.Api.Services.Artwork;

/// <summary>Visit-triggered picture backfill (design.md D4b/D6) — the fetches
/// that populate <c>PictureUrls</c> for rows a free-riding full-detail fetch
/// hasn't reached yet. Never a bulk sweep: every call here is caused by a
/// page visit.</summary>
public interface IPictureRefreshService
{
    /// <summary>Backfills one anime's picture set. No-op (returns false) when
    /// the anime isn't in my list or already has one. Never throws on a MAL
    /// failure — logs and returns false so the caller's flag stays set for a
    /// retry on the next visit.</summary>
    Task<bool> RefreshOneAsync(int animeId, CancellationToken ct = default);

    /// <summary>Backfills up to <paramref name="budget"/> never-fetched,
    /// my-list main-line members of a series, in main-line order. Returns how
    /// many eligible members remain unfetched afterwards.</summary>
    Task<int> RefreshSeriesMainLineAsync(int seriesId, int budget, CancellationToken ct = default);
}
