using AnimeTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Data.Repositories;

public class AnimeMetadataRepository(AnimeTrackerDbContext db) : IAnimeMetadataRepository
{
    public Task<AnimeMetadata?> GetByIdAsync(int id, CancellationToken ct = default) =>
        db.AnimeMetadata.AsNoTracking()
            .Include(a => a.UserEntry)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<List<AnimeMetadata>> GetAllAsync(CancellationToken ct = default) =>
        db.AnimeMetadata.AsNoTracking()
            .Include(a => a.UserEntry)
            .ToListAsync(ct);

    public Task<List<AnimeTitleProjection>> GetSearchIndexAsync(CancellationToken ct = default) =>
        db.AnimeMetadata.AsNoTracking()
            .Select(a => new AnimeTitleProjection(a.Id, a.Title, a.EnglishTitle, a.PictureUrl))
            .ToListAsync(ct);
}
