using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Series;

/// <summary>Loads the SeriesMembers ⋈ AnimeMetadata ⋈ UserEntry projection the
/// Top series ranking needs — once per profile-section request, the same
/// shape of work as SeriesSearchLookup on every keystroke (design.md decision
/// 1). Every member of every stored series is loaded (not just main-line
/// rows) since eligibility looks at the whole series while the averages look
/// at the main line only (design.md decision 2); matching, averaging, and the
/// reveal rule all happen in-memory over the loaded rows (see
/// SeriesRankingIndex).</summary>
public class SeriesRankingLookup(AnimeTrackerDbContext db)
{
    public async Task<SeriesRankingIndex> LoadAsync(CancellationToken ct = default)
    {
        // A fresh install (or one where no series has ever been built) has
        // nothing to rank — skip the join entirely rather than running it
        // against an empty table on every profile read (task 2.2).
        if (!await db.Series.AsNoTracking().AnyAsync(ct))
            return SeriesRankingIndex.Empty;

        var members = await db.SeriesMembers.AsNoTracking()
            .Join(db.Series.AsNoTracking(),
                member => member.SeriesId,
                series => series.Id,
                (member, series) => new SeriesRankingMemberProjection(
                    member.SeriesId,
                    series.RootAnimeId,
                    member.AnimeId,
                    member.IsMainLine,
                    member.Anime.Title,
                    member.Anime.EnglishTitle,
                    member.Anime.PictureUrl,
                    member.Anime.MalScore,
                    member.Anime.UserEntry != null ? member.Anime.UserEntry.MyScore : null,
                    member.Anime.UserEntry != null ? member.Anime.UserEntry.Status : (WatchStatus?)null,
                    member.Anime.AiringStatus,
                    member.Anime.UserEntry != null ? member.Anime.UserEntry.RewatchCount : (int?)null,
                    member.Anime.UserEntry != null ? member.Anime.UserEntry.EpisodesWatched : (int?)null,
                    member.Anime.TotalEpisodes,
                    member.Anime.AverageEpisodeDurationSeconds))
            .ToListAsync(ct);

        return new SeriesRankingIndex(members);
    }
}

/// <summary>One series member row joined with its anime's title/picture,
/// score, airing status, and my entry for it (if any) — the unit
/// SeriesRankingIndex computes eligibility, averages, and rewatch time over.
/// RewatchCount/EpisodesWatched are null-guarded off UserEntry like
/// MyScore/EntryStatus above; TotalEpisodes/AverageEpisodeDurationSeconds are
/// the anime's own published fields, not tied to list membership.</summary>
internal sealed record SeriesRankingMemberProjection(
    int SeriesId, int RootAnimeId, int AnimeId, bool IsMainLine,
    string Title, string? EnglishTitle, string? PictureUrl,
    double? MalScore, int? MyScore, WatchStatus? EntryStatus, string? AiringStatus,
    int? RewatchCount, int? EpisodesWatched, int? TotalEpisodes, int? AverageEpisodeDurationSeconds);
