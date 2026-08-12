using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Entries;
using AnimeTracker.Api.Services.Infrastructure;
using AnimeTracker.Api.Services.Profile;
using Microsoft.EntityFrameworkCore;
// Alias needed because this namespace's last segment ("Series") shadows the
// Models.Series type name.
using SeriesEntity = AnimeTracker.Api.Models.Series;

namespace AnimeTracker.Api.Services.Series;

/// <summary>Resolves a franchise for the read endpoint: decides whether the
/// stored series needs (re)building, runs that build single-flight through
/// <see cref="RefreshGate"/>, and projects the stored rows to
/// <see cref="SeriesDto"/> — scores and stats are computed fresh on every
/// call (design.md decision 5/task 3.9), never cached.</summary>
public class SeriesService(
    AnimeTrackerDbContext db,
    SeriesGraphBuilder graphBuilder,
    RefreshGate refreshGate) : ISeriesService
{
    private static readonly TimeSpan StaleAfter = TimeSpan.FromDays(30);

    public Task<SeriesDto> GetSeriesAsync(int animeId, CancellationToken ct = default) =>
        ResolveAsync(animeId, forceRebuild: false, ct);

    public Task<SeriesDto> RebuildSeriesAsync(int animeId, CancellationToken ct = default) =>
        ResolveAsync(animeId, forceRebuild: true, ct);

    private async Task<SeriesDto> ResolveAsync(int animeId, bool forceRebuild, CancellationToken ct)
    {
        var series = await FindSeriesAsync(animeId, ct);

        if (forceRebuild || NeedsBuild(series))
        {
            // Known series rebuilds collapse on its id; a never-built one
            // collapses on the seed anime id instead, since there's no
            // series id yet to key on (design.md decision 6).
            var key = series is not null ? $"series:{series.Id}" : $"series:anime:{animeId}";
            using (await refreshGate.LockAsync(key, ct))
            {
                // Re-check after acquiring the gate: a waiter sees the
                // winner's build and skips a redundant one.
                series = await FindSeriesAsync(animeId, ct);
                if (forceRebuild || NeedsBuild(series))
                {
                    var fetchBudget = forceRebuild ? SeriesGraphBuilder.RebuildFetchBudget : SeriesGraphBuilder.VisitFetchBudget;
                    // BuildAsync returns null when the seed's component has
                    // no other member. That's not necessarily "no series at
                    // all" if one was already stored (relations can thin out
                    // between visits) — falls back to the pre-build snapshot
                    // rather than treating a already-stored series as gone.
                    //
                    // expandLeanMembers is always on, not just on an explicit
                    // rebuild: a lean member (season/top-anime browsing) has
                    // no outgoing relations of its own, so a real season
                    // reachable only *through* it never gets discovered, and
                    // main-line classification (sequel/prequel edges only)
                    // can hand the main line to whichever chain happened to
                    // be full-fetched. Spending part of the visit budget here
                    // is what lets a first visit self-heal instead of always
                    // depending on the user clicking Rebuild.
                    series = await graphBuilder.BuildAsync(animeId, fetchBudget, expandLeanMembers: true, ct) ?? series;
                }
            }
        }

        if (series is null)
            throw new SeriesNotFoundException(animeId);

        return await ProjectAsync(series.Id, ct);
    }

    private static bool NeedsBuild(SeriesEntity? series) =>
        series is null || series.IsPartial || series.BuiltAt < DateTimeOffset.UtcNow - StaleAfter;

    private async Task<SeriesEntity?> FindSeriesAsync(int animeId, CancellationToken ct)
    {
        var seriesId = await db.SeriesMembers.AsNoTracking()
            .Where(m => m.AnimeId == animeId)
            .Select(m => (int?)m.SeriesId)
            .FirstOrDefaultAsync(ct);

        return seriesId is null
            ? null
            : await db.Series.AsNoTracking().FirstOrDefaultAsync(s => s.Id == seriesId, ct);
    }

    // --- Projection (3.1-3.4) ---

    private async Task<SeriesDto> ProjectAsync(int seriesId, CancellationToken ct)
    {
        var series = await db.Series.AsNoTracking()
            .Include(s => s.Members).ThenInclude(m => m.Anime).ThenInclude(a => a.RelatedAnime)
            .Include(s => s.Members).ThenInclude(m => m.Anime).ThenInclude(a => a.UserEntry)
            .FirstAsync(s => s.Id == seriesId, ct);

        var memberAnimeIds = series.Members.Select(m => m.AnimeId).ToHashSet();
        var mainLineMembers = series.Members.Where(m => m.IsMainLine).OrderBy(m => m.Order).ToList();
        var extraMembers = series.Members.Where(m => !m.IsMainLine)
            .OrderBy(m => SeriesMediaTypeOrder.GroupOf(m.Anime.MediaType))
            .ThenBy(m => m.Order)
            .ToList();
        var allAnime = series.Members.Select(m => m.Anime).ToList();

        var root = allAnime.First(a => a.Id == series.RootAnimeId);
        var (firstYear, lastYear) = YearSpan(allAnime);

        return new SeriesDto(
            series.Id,
            series.RootAnimeId,
            root.Title,
            root.EnglishTitle,
            root.PictureUrl,
            ComputeStatus(allAnime),
            firstYear,
            lastYear,
            series.BuiltAt,
            series.IsPartial,
            series.IsTruncated,
            BuildScores(mainLineMembers, series.Members),
            BuildStats(mainLineMembers, extraMembers, allAnime),
            mainLineMembers.Select(m => ToEntryDto(m, memberAnimeIds)).ToList(),
            extraMembers.Select(m => ToEntryDto(m, memberAnimeIds)).ToList());
    }

    private static SeriesEntryDto ToEntryDto(SeriesMember member, HashSet<int> memberAnimeIds)
    {
        var anime = member.Anime;
        return new SeriesEntryDto(
            anime.Id,
            anime.Title,
            anime.EnglishTitle,
            anime.PictureUrl,
            anime.MediaType,
            anime.AiringStatus,
            anime.TotalEpisodes,
            anime.AverageEpisodeDurationSeconds,
            anime.AiredFrom,
            anime.MalScore,
            RelationTypeWithinSeries(anime, memberAnimeIds),
            member.Order,
            anime.UserEntry is null ? null : UserAnimeEntryDto.FromEntity(anime.UserEntry));
    }

    // Only this anime's own relation rows are considered, so a member
    // reached solely by a reverse edge (its own relations were never
    // fetched) reports no relation type here rather than guessing one.
    private static string? RelationTypeWithinSeries(AnimeMetadata anime, HashSet<int> memberAnimeIds) =>
        anime.RelatedAnime
            .Where(r => SeriesRelations.IsTraversable(r.RelationType) && memberAnimeIds.Contains(r.RelatedAnimeId))
            .OrderBy(r => r.RelationType, StringComparer.Ordinal)
            .Select(r => r.RelationType)
            .FirstOrDefault();

    // --- Status pill (3.4) ---

    private static string ComputeStatus(List<AnimeMetadata> members)
    {
        if (members.Any(a => a.AiringStatus == "currently_airing"))
            return "Ongoing";

        var anyFinished = members.Any(a => a.AiringStatus == "finished_airing");
        var anyUpcoming = members.Any(a => a.AiringStatus == "not_yet_aired");

        if (!anyFinished && anyUpcoming)
            return "Upcoming";

        return anyUpcoming ? "Finished · sequel upcoming" : "Finished";
    }

    private static (int? First, int? Last) YearSpan(List<AnimeMetadata> members)
    {
        var years = members.Where(a => a.AiredFrom is not null).Select(a => a.AiredFrom!.Value.Year).ToList();
        return years.Count > 0 ? (years.Min(), years.Max()) : (null, null);
    }

    // --- Score averages (3.2) ---

    private static SeriesScoresDto BuildScores(List<SeriesMember> mainLineMembers, List<SeriesMember> allMembers) =>
        new(
            MalMain: MalAverage(mainLineMembers),
            MalAll: MalAverage(allMembers),
            MineMain: MyAverage(mainLineMembers),
            MineAll: MyAverage(allMembers));

    private static SeriesAverageDto MalAverage(List<SeriesMember> members)
    {
        var scored = members.Where(m => m.Anime.MalScore is not null).Select(m => m.Anime.MalScore!.Value).ToList();
        return new SeriesAverageDto(scored.Count > 0 ? scored.Average() : null, scored.Count, members.Count);
    }

    // MAL's score of 0 means "unscored", not a rating (design.md decision 8).
    private static SeriesAverageDto MyAverage(List<SeriesMember> members)
    {
        var scored = members
            .Select(m => m.Anime.UserEntry?.MyScore)
            .Where(score => score is > 0)
            .Select(score => (double)score!.Value)
            .ToList();
        return new SeriesAverageDto(scored.Count > 0 ? scored.Average() : null, scored.Count, members.Count);
    }

    // --- Stats (3.3) ---

    private static SeriesStatsDto BuildStats(
        List<SeriesMember> mainLineMembers, List<SeriesMember> extraMembers, List<AnimeMetadata> allAnime)
    {
        var mainLineAnime = mainLineMembers.Select(m => m.Anime).ToList();
        var extraAnime = extraMembers.Select(m => m.Anime).ToList();

        var (mainEpisodes, mainRuntimeSeconds, hasUnknown) = EpisodesAndRuntime(mainLineAnime);
        var (extraEpisodes, extraRuntimeSeconds, _) = EpisodesAndRuntime(extraAnime);

        var myWatchedEpisodes = mainLineAnime.Sum(a => a.UserEntry?.EpisodesWatched ?? 0);
        var myWatchedSeconds = mainLineAnime.Sum(a => (long)(a.UserEntry?.EpisodesWatched ?? 0) * EpisodeSeconds(a));
        var entriesCompleted = mainLineAnime.Count(a => a.UserEntry?.Status == WatchStatus.Completed);

        var (gapDays, gapFromId, gapToId) = LongestGap(mainLineAnime);

        var highestMal = allAnime.Where(a => a.MalScore is not null).OrderByDescending(a => a.MalScore).FirstOrDefault();
        var highestMine = allAnime.Where(a => a.UserEntry?.MyScore is > 0).OrderByDescending(a => a.UserEntry!.MyScore).FirstOrDefault();

        var studios = allAnime
            .Select(a => a.Studio)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct()
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .Select(s => s!)
            .ToList();
        var genres = allAnime
            .SelectMany(a => a.Genres ?? [])
            .Distinct()
            .OrderBy(g => g, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new SeriesStatsDto(
            mainEpisodes,
            mainRuntimeSeconds,
            hasUnknown,
            extraEpisodes,
            extraRuntimeSeconds,
            myWatchedEpisodes,
            myWatchedSeconds,
            entriesCompleted,
            mainLineMembers.Count,
            extraMembers.Count,
            gapDays,
            gapFromId,
            gapToId,
            highestMal?.Id,
            highestMine?.Id,
            studios,
            genres);
    }

    // An entry with an unknown TotalEpisodes contributes nothing to either
    // total and flips HasUnknown, rather than guessing at episodes aired so
    // far — the resulting total is always a true lower bound.
    private static (int Episodes, long RuntimeSeconds, bool HasUnknown) EpisodesAndRuntime(List<AnimeMetadata> anime)
    {
        var episodes = 0;
        var runtimeSeconds = 0L;
        var hasUnknown = false;

        foreach (var a in anime)
        {
            if (a.TotalEpisodes is not { } total)
            {
                hasUnknown = true;
                continue;
            }

            episodes += total;
            runtimeSeconds += (long)total * EpisodeSeconds(a);
        }

        return (episodes, runtimeSeconds, hasUnknown);
    }

    private static int EpisodeSeconds(AnimeMetadata anime) =>
        anime.AverageEpisodeDurationSeconds ?? ProfileService.AssumedMinutesPerEpisode * 60;

    // mainLineAnime is already in release order (the caller selects it from
    // members ordered by Order), so the longest gap is the largest jump
    // between consecutive dated entries in that sequence.
    private static (int? Days, int? FromAnimeId, int? ToAnimeId) LongestGap(List<AnimeMetadata> mainLineAnime)
    {
        var dated = mainLineAnime.Where(a => a.AiredFrom is not null).ToList();
        if (dated.Count < 2)
            return (null, null, null);

        int? bestGapDays = null;
        int? fromAnimeId = null;
        int? toAnimeId = null;

        for (var i = 0; i < dated.Count - 1; i++)
        {
            var gapDays = dated[i + 1].AiredFrom!.Value.DayNumber - dated[i].AiredFrom!.Value.DayNumber;
            if (bestGapDays is not null && gapDays <= bestGapDays)
                continue;

            bestGapDays = gapDays;
            fromAnimeId = dated[i].Id;
            toAnimeId = dated[i + 1].Id;
        }

        return (bestGapDays, fromAnimeId, toAnimeId);
    }
}
