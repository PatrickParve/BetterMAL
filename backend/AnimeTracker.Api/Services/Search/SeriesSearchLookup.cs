using AnimeTracker.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Search;

/// <summary>Loads the SeriesMembers ⋈ AnimeMetadata projection search needs to
/// match series against a query — once per search, the same shape of work as
/// the type-ahead's whole-table read of the anime metadata cache
/// (IAnimeMetadataRepository.GetSearchIndexAsync). Matching itself runs
/// in-memory over the loaded rows (see SeriesSearchIndex).</summary>
public class SeriesSearchLookup(AnimeTrackerDbContext db)
{
    public async Task<SeriesSearchIndex> LoadAsync(CancellationToken ct = default)
    {
        // A fresh install (or one where no series has ever been built) has
        // nothing to match — skip the join entirely rather than running it
        // against an empty table on every search (task 1.5).
        if (!await db.Series.AsNoTracking().AnyAsync(ct))
            return SeriesSearchIndex.Empty;

        var members = await db.SeriesMembers.AsNoTracking()
            .Join(db.Series.AsNoTracking(),
                member => member.SeriesId,
                series => series.Id,
                (member, series) => new SeriesMemberProjection(
                    member.SeriesId,
                    member.AnimeId,
                    member.Anime.Title,
                    member.Anime.EnglishTitle,
                    member.Anime.PictureUrl,
                    member.IsMainLine,
                    member.Anime.PopularityRank,
                    series.RootAnimeId,
                    series.SelectedTitle,
                    series.SelectedPictureUrl))
            .ToListAsync(ct);

        return new SeriesSearchIndex(members);
    }
}

/// <summary>One series member row joined with its anime's title/picture and
/// its series' root anime id — the unit SeriesSearchIndex matches over.
/// SelectedTitle/SelectedPictureUrl are the series' own overrides (identical
/// across every row of the same series), fed to SeriesIdentity.Resolve for
/// display (design.md D7).</summary>
internal sealed record SeriesMemberProjection(
    int SeriesId, int AnimeId, string Title, string? EnglishTitle, string? PictureUrl,
    bool IsMainLine, int? PopularityRank, int RootAnimeId,
    string? SelectedTitle, string? SelectedPictureUrl);
