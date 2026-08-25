using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing.AniList;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Relations;

/// <summary>Persists the AniList relation edges fetched for one anime,
/// replacing any previously stored set for it — mirrors how a MAL full-detail
/// fetch replaces <c>AnimeRelatedAnime</c> wholesale. Deliberately narrow: it
/// only touches <see cref="AniListRelation"/> rows. Callers manage
/// <see cref="AnimeAiringSync.RelationsFetchedAt"/> and <see cref="AnimeAiringSync.AniListId"/>
/// themselves, alongside whatever else their own AniList fetch already
/// touches, and are responsible for calling <c>SaveChangesAsync</c>.</summary>
public class AniListRelationStore(AnimeTrackerDbContext db)
{
    public async Task ReplaceAsync(int animeId, IReadOnlyList<AniListRelationEdge> relations, CancellationToken ct = default)
    {
        var existing = await db.AniListRelations.Where(r => r.AnimeId == animeId).ToListAsync(ct);
        db.AniListRelations.RemoveRange(existing);

        foreach (var edge in relations)
        {
            if (edge.RelatedMalId is not { } relatedMalId)
                continue; // no MAL id on the far end — cannot be matched against a MAL edge, not evidence about one
            if (AniListRelationTypeMapper.IsIgnored(edge.RelationType))
                continue; // manga-side ADAPTATION

            db.AniListRelations.Add(new AniListRelation
            {
                AnimeId = animeId,
                RelatedAnimeId = relatedMalId,
                RelationType = edge.RelationType,
            });
        }
    }
}
