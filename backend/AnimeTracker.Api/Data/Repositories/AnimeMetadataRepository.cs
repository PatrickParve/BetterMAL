using AnimeTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Data.Repositories;

public class AnimeMetadataRepository(AnimeTrackerDbContext db) : IAnimeMetadataRepository
{
    public Task<AnimeMetadata?> GetByIdAsync(int id, CancellationToken ct = default) =>
        db.AnimeMetadata.AsNoTracking()
            .Include(a => a.UserEntry)
            .Include(a => a.RelatedAnime.OrderBy(r => r.SortOrder))
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<List<AnimeMetadata>> GetAllAsync(CancellationToken ct = default) =>
        db.AnimeMetadata.AsNoTracking()
            .Include(a => a.UserEntry)
            .ToListAsync(ct);

    public Task<List<AnimeTitleProjection>> GetSearchIndexAsync(CancellationToken ct = default) =>
        db.AnimeMetadata.AsNoTracking()
            .Select(a => new AnimeTitleProjection(a.Id, a.Title, a.EnglishTitle, a.PictureUrl, a.PopularityRank))
            .ToListAsync(ct);

    public Task<List<AnimeSearchFallbackProjection>> GetSearchFallbackIndexAsync(CancellationToken ct = default) =>
        db.AnimeMetadata.AsNoTracking()
            .Select(a => new AnimeSearchFallbackProjection(
                a.Id, a.Title, a.EnglishTitle, a.PictureUrl, a.PopularityRank,
                a.MediaType, a.TotalEpisodes, a.MalScore))
            .ToListAsync(ct);
}
