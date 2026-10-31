using AnimeTracker.Api.Data;
using AnimeTracker.Api.Services.Infrastructure;
using AnimeTracker.Api.Services.Metadata;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Games;

/// <summary>Fetches an anime's full detail when a game needs a stat it doesn't
/// have (add-guess-the-anime design.md D6, metadata-refresh "Full detail fetch
/// reserved for import, tiered refresh, and detail view").
///
/// It is the same single-anime <see cref="IMetadataRefreshService.RefreshOneAsync"/>
/// the detail page makes, behind the same <see cref="RefreshGate"/> key,
/// <c>anime:{id}</c>, so a guess and an open detail page make one request between
/// them. Like the detail page it checks again once it holds the gate, so a caller
/// that waited behind another's fetch sees the result and makes none. An anime that
/// isn't stored gets its row, as a detail page's first visit creates one.
///
/// What counts as missing is a question about what the game has to draw, not
/// whether the anime's detail is complete: an anime MyAnimeList has nothing more
/// for is fetched each time a game needs it. That changes nothing about when a
/// detail page fetches.</summary>
public class GameAnimeDetails(
    AnimeTrackerDbContext db,
    IMetadataRefreshService refreshService,
    RefreshGate refreshGate) : IGameAnimeDetails
{
    public async Task<bool> EnsureFetchedAsync(int animeId, CancellationToken ct = default)
    {
        if (!await IsMissingAsync(animeId, ct))
            return false;

        using (await refreshGate.LockAsync($"anime:{animeId}", ct))
        {
            if (!await IsMissingAsync(animeId, ct))
                return false;

            await refreshService.RefreshOneAsync(animeId, ct);
            return true;
        }
    }

    /// <summary>Not stored, or stored with a stat missing. Read untracked, so the
    /// fetch's own tracked copy of the row is the only one in the context.</summary>
    private async Task<bool> IsMissingAsync(int animeId, CancellationToken ct)
    {
        var missing = await db.AnimeMetadata.AsNoTracking()
            .Where(a => a.Id == animeId)
            .Select(AnimeStats.IsMissing)
            .ToListAsync(ct);

        return missing.Count == 0 || missing[0];
    }
}
