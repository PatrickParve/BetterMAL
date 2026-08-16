using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
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
    RefreshGate refreshGate,
    IEpisodeScheduleService scheduleService) : ISeriesService
{
    private static readonly TimeSpan StaleAfter = TimeSpan.FromDays(30);

    public Task<SeriesDto> GetSeriesAsync(int animeId, CancellationToken ct = default) =>
        ResolveAsync(animeId, forceRebuild: false, ct);

    public Task<SeriesDto> RebuildSeriesAsync(int animeId, CancellationToken ct = default) =>
        ResolveAsync(animeId, forceRebuild: true, ct);

    public async Task SetFavouriteOrderAsync(int seriesId, List<int> animeIds, CancellationToken ct = default)
    {
        var members = await db.SeriesMembers.Where(m => m.SeriesId == seriesId).ToListAsync(ct);
        if (members.Count == 0 && !await db.Series.AsNoTracking().AnyAsync(s => s.Id == seriesId, ct))
            throw new SeriesIdNotFoundException(seriesId);

        var memberIds = members.Select(m => m.AnimeId).ToHashSet();
        var unknownIds = animeIds.Where(id => !memberIds.Contains(id)).ToList();
        if (unknownIds.Count > 0)
            throw new UnknownSeriesMemberIdsException(unknownIds);

        var rankByAnimeId = animeIds
            .Select((animeId, rank) => (animeId, rank))
            .ToDictionary(x => x.animeId, x => x.rank);

        foreach (var member in members)
            member.FavouriteRank = rankByAnimeId.TryGetValue(member.AnimeId, out var rank) ? rank : null;

        await db.SaveChangesAsync(ct);
    }

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

    // Deliberately does not check IsTruncated: a series genuinely over the
    // cap would then re-traverse and spend fetch budget on every single
    // visit, forever. A truncated series instead picks up a raised cap on
    // the next explicitly requested rebuild or the next staleness-triggered
    // one (design.md decision 3). The ClassificationRevisedAt clause is the
    // same "row predates X, heal it on first read" pattern, one-shot rather
    // than recurring: it rebuilds every series built under superseded
    // main-line rules exactly once, on its next read, without a schema
    // change or a manual migration.
    private static bool NeedsBuild(SeriesEntity? series) =>
        series is null || series.IsPartial || series.BuiltAt < DateTimeOffset.UtcNow - StaleAfter
        || series.BuiltAt < SeriesGraphBuilder.ClassificationRevisedAt;

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
        var rootAniListId = await db.AnimeAiringSyncs.AsNoTracking()
            .Where(s => s.AnimeId == series.RootAnimeId)
            .Select(s => (int?)s.AniListId)
            .FirstOrDefaultAsync(ct);
        var airedEpisodesByAnimeId = await AiredEpisodesByAnimeIdAsync(allAnime, ct);
        var mainLineAiredEpisodes = MainLineAiredEpisodesFromMap(mainLineMembers, airedEpisodesByAnimeId);

        return new SeriesDto(
            series.Id,
            series.RootAnimeId,
            rootAniListId,
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
            BuildStats(mainLineMembers, extraMembers, allAnime, mainLineAiredEpisodes, airedEpisodesByAnimeId),
            mainLineMembers.Select(m => ToEntryDto(m, memberAnimeIds, airedEpisodesByAnimeId)).ToList(),
            extraMembers.Select(m => ToEntryDto(m, memberAnimeIds, airedEpisodesByAnimeId)).ToList());
    }

    // Per-member AiredEpisodes (design.md decision 1) for every series member,
    // main line and extras alike, so both the entry rows and the aggregate
    // stat below read from one map instead of disagreeing. Only a
    // currently-airing member hits IEpisodeScheduleService — typically zero
    // or one per series.
    private async Task<Dictionary<int, int?>> AiredEpisodesByAnimeIdAsync(List<AnimeMetadata> allAnime, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var result = new Dictionary<int, int?>();

        foreach (var anime in allAnime)
        {
            result[anime.Id] = anime.AiringStatus switch
            {
                "finished_airing" => anime.TotalEpisodes,
                "currently_airing" => await CurrentlyAiringEpisodesAsync(anime, now, ct),
                "not_yet_aired" => 0,
                _ => null,
            };
        }

        return result;
    }

    private async Task<int?> CurrentlyAiringEpisodesAsync(AnimeMetadata anime, DateTimeOffset now, CancellationToken ct)
    {
        var aired = await scheduleService.EpisodesAiredAsOfAsync(anime, now, ct);
        if (aired is null)
            return null;

        return anime.TotalEpisodes is { } total ? Math.Min(aired.Value, total) : aired;
    }

    // Kept distinct from the per-entry AiredEpisodes above: a member with an
    // unknown TotalEpisodes contributes 0 here even when its own
    // AiredEpisodes is known, which is what keeps aired ≤ total on the bar
    // (design.md decision 1).
    private static int MainLineAiredEpisodesFromMap(List<SeriesMember> mainLineMembers, Dictionary<int, int?> airedEpisodesByAnimeId)
    {
        var airedEpisodes = 0;

        foreach (var member in mainLineMembers)
        {
            var anime = member.Anime;
            if (anime.TotalEpisodes is not { } total)
                continue;

            airedEpisodes += anime.AiringStatus switch
            {
                "finished_airing" => total,
                "currently_airing" => Math.Min(airedEpisodesByAnimeId.GetValueOrDefault(anime.Id) ?? 0, total),
                _ => 0,
            };
        }

        return airedEpisodes;
    }

    private static SeriesEntryDto ToEntryDto(SeriesMember member, HashSet<int> memberAnimeIds, Dictionary<int, int?> airedEpisodesByAnimeId)
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
            anime.AiredTo,
            anime.MalScore,
            RelationTypeWithinSeries(anime, memberAnimeIds),
            member.Order,
            airedEpisodesByAnimeId.GetValueOrDefault(anime.Id),
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
            MalMain: SeriesAverages.Mal(mainLineMembers.Select(m => m.Anime.MalScore)),
            MalAll: SeriesAverages.Mal(allMembers.Select(m => m.Anime.MalScore)),
            MineMain: SeriesAverages.Mine(mainLineMembers.Select(m => m.Anime.UserEntry?.MyScore)),
            MineAll: SeriesAverages.Mine(allMembers.Select(m => m.Anime.UserEntry?.MyScore)));

    // --- Stats (3.3) ---

    // Internal (not private) so tests can exercise the aired-count fallback
    // and tied-stat computations directly against plain in-memory models,
    // without standing up a database.
    internal static SeriesStatsDto BuildStats(
        List<SeriesMember> mainLineMembers, List<SeriesMember> extraMembers, List<AnimeMetadata> allAnime,
        int mainLineAiredEpisodes, Dictionary<int, int?> airedEpisodesByAnimeId)
    {
        var mainLineAnime = mainLineMembers.Select(m => m.Anime).ToList();
        var extraAnime = extraMembers.Select(m => m.Anime).ToList();

        var (mainEpisodes, mainRuntimeSeconds, hasUnknown) = EpisodesAndRuntime(mainLineAnime, airedEpisodesByAnimeId);
        var (extraEpisodes, extraRuntimeSeconds, _) = EpisodesAndRuntime(extraAnime, airedEpisodesByAnimeId);

        var myWatchedEpisodes = mainLineAnime.Sum(a => a.UserEntry?.EpisodesWatched ?? 0);
        var myWatchedSeconds = mainLineAnime.Sum(a => (long)(a.UserEntry?.EpisodesWatched ?? 0) * EpisodeSeconds(a));
        var entriesCompleted = mainLineAnime.Count(a => a.UserEntry?.Status == WatchStatus.Completed);
        var mainLineCompletedByMe = SeriesAverages.MainLineCompletedByMe(
            mainLineAnime.Select(a => (a.AiringStatus, a.UserEntry?.Status)));

        var (gapDays, gapFromId, gapToId) = LongestGap(mainLineAnime);

        // Watch order for tie-breaking: main line first (already Order-sorted),
        // then extras (already media-group-then-Order-sorted) — design.md
        // decision 8.
        var orderedMembers = mainLineMembers.Concat(extraMembers).ToList();
        var highestMalIds = TiedTopIds(
            orderedMembers.Where(m => m.Anime.MalScore is not null),
            m => m.Anime.MalScore!.Value);
        var highestMineIds = TiedTopIds(
            orderedMembers.Where(m => m.Anime.UserEntry?.MyScore is > 0),
            m => m.Anime.UserEntry!.MyScore!.Value,
            // Unranked (null) entries sort after every ranked one; ties within
            // that (including two unranked entries) keep watch order, since
            // OrderBy is a stable sort over the already watch-ordered sequence.
            m => m.FavouriteRank ?? int.MaxValue);
        var mostRewatchedIds = TiedTopIds(
            orderedMembers.Where(m => m.Anime.UserEntry?.RewatchCount is > 0),
            m => m.Anime.UserEntry!.RewatchCount);

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
            mainLineAiredEpisodes,
            extraEpisodes,
            extraRuntimeSeconds,
            myWatchedEpisodes,
            myWatchedSeconds,
            entriesCompleted,
            mainLineCompletedByMe,
            mainLineMembers.Count,
            extraMembers.Count,
            gapDays,
            gapFromId,
            gapToId,
            highestMalIds,
            highestMineIds,
            mostRewatchedIds,
            studios,
            genres);
    }

    // Every member whose comparable key ties the maximum, in the order given
    // (already watch order) unless a tieBreakKey is supplied — used for
    // favourites, which order by FavouriteRank first (design.md decision 8).
    private static List<int> TiedTopIds<TKey>(
        IEnumerable<SeriesMember> candidates, Func<SeriesMember, TKey> scoreKey, Func<SeriesMember, int>? tieBreakKey = null)
        where TKey : IComparable<TKey>
    {
        var list = candidates.ToList();
        if (list.Count == 0)
            return [];

        var max = list.Max(scoreKey);
        var tied = list.Where(m => scoreKey(m).CompareTo(max) == 0);
        if (tieBreakKey is not null)
            tied = tied.OrderBy(tieBreakKey);

        return tied.Select(m => m.AnimeId).ToList();
    }

    // An entry with an unknown TotalEpisodes still contributes its known
    // aired-so-far count (0 when that's unknown too) and flips HasUnknown,
    // so the resulting total is a tighter lower bound rather than dropping
    // the entry entirely.
    private static (int Episodes, long RuntimeSeconds, bool HasUnknown) EpisodesAndRuntime(
        List<AnimeMetadata> anime, Dictionary<int, int?> airedEpisodesByAnimeId)
    {
        var episodes = 0;
        var runtimeSeconds = 0L;
        var hasUnknown = false;

        foreach (var a in anime)
        {
            if (a.TotalEpisodes is not { } total)
            {
                hasUnknown = true;
                var aired = airedEpisodesByAnimeId.GetValueOrDefault(a.Id) ?? 0;
                episodes += aired;
                runtimeSeconds += (long)aired * EpisodeSeconds(a);
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
