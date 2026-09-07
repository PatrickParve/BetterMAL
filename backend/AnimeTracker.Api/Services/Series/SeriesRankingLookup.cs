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
                    member.AnimeId,
                    member.IsMainLine,
                    member.Order,
                    member.Anime.Title,
                    member.Anime.EnglishTitle,
                    member.Anime.MalPictureUrl,
                    member.Anime.MalScore,
                    member.Anime.UserEntry != null ? member.Anime.UserEntry.MyScore : null,
                    member.Anime.UserEntry != null ? member.Anime.UserEntry.Status : (WatchStatus?)null,
                    member.Anime.AiringStatus,
                    member.Anime.UserEntry != null ? member.Anime.UserEntry.RewatchCount : (int?)null,
                    member.Anime.UserEntry != null ? member.Anime.UserEntry.EpisodesWatched : (int?)null,
                    member.Anime.TotalEpisodes,
                    member.Anime.AverageEpisodeDurationSeconds,
                    member.Anime.AiredFrom,
                    member.Anime.AiredTo,
                    series.SelectedTitle,
                    series.SelectedPictureUrl,
                    member.MembershipKind,
                    member.Anime.PopularityRank,
                    member.VersionSlotKey,
                    member.BranchHeadAnimeId))
            .ToListAsync(ct);

        return new SeriesRankingIndex(members);
    }
}

/// <summary>One series member row joined with its anime's title/picture,
/// score, airing status, and my entry for it (if any) — the unit
/// SeriesRankingIndex computes eligibility, averages, rewatch time, and (via
/// ListedSeries, add-series-browser design.md D2) the series list's per-card
/// figures over. RewatchCount/EpisodesWatched are null-guarded off UserEntry
/// like MyScore/EntryStatus above; TotalEpisodes/AverageEpisodeDurationSeconds/
/// AiredFrom/AiredTo are the anime's own published fields, not tied to list
/// membership; Order is the member's position within its main line or extras
/// media-type group. SelectedTitle/SelectedPictureUrl are the series' own
/// overrides (identical across every row of the same series), fed to
/// SeriesIdentity.Resolve for display alongside the root's MalPictureUrl —
/// its default, deliberately not its own (resolved) displayed picture, so a
/// root anime's own pin never doubles as the series' default (design.md D7).
/// MembershipKind, PopularityRank, VersionSlotKey and BranchHeadAnimeId
/// (rebuild-series-by-story-component design.md D2/D4) let this index apply
/// the same version-neighbour eligibility rule and default-combination
/// scoping SeriesService applies on the series page itself (tasks
/// 7.5-7.6).</summary>
internal sealed record SeriesRankingMemberProjection(
    int SeriesId, int AnimeId, bool IsMainLine, int Order,
    string Title, string? EnglishTitle, string? MalPictureUrl,
    double? MalScore, int? MyScore, WatchStatus? EntryStatus, string? AiringStatus,
    int? RewatchCount, int? EpisodesWatched, int? TotalEpisodes, int? AverageEpisodeDurationSeconds,
    DateOnly? AiredFrom, DateOnly? AiredTo,
    string? SelectedTitle, string? SelectedPictureUrl,
    string MembershipKind, int? PopularityRank, int? VersionSlotKey, int? BranchHeadAnimeId);
