using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Airing.AniList;
using AnimeTracker.Api.Services.Relations;
using AnimeTracker.Api.Services.Scheduling;
using AnimeTracker.Api.Services.Updates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimeTracker.Api.Tests.Services.Airing;

/// <summary>What the airing refresh tests share: a database that tolerates the
/// refresh's per-anime transaction, a repository that replaces rows without
/// <c>ExecuteDelete</c> (which the in-memory provider lacks), and the service
/// wired over them.</summary>
internal static class AiringRefreshTestKit
{
    public static readonly IBroadcastLocalTimeConverter LocalTime = new BroadcastLocalTimeConverter();

    // The refresh saves each anime in a transaction. The in-memory provider has
    // none, and ignores the calls once told not to complain about it.
    public static AnimeTrackerDbContext CreateDb(string? name = null) =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    public static EpisodeScheduleRefreshService CreateService(
        AnimeTrackerDbContext db, IAniListClient aniList, ILogger<EpisodeScheduleRefreshService>? logger = null) =>
        new(
            db,
            new UserAnimeEntryRepository(db),
            new InMemoryEpisodeAiringRepository(db),
            aniList,
            new AniListRelationStore(db),
            LocalTime,
            new AnimeUpdateRecorder(db, new AnimeUpdateRelevance(db, new RelationResolver(db))),
            logger ?? NullLogger<EpisodeScheduleRefreshService>.Instance);

    /// <summary>An anime in my list, with the airing sync row a refresh reads.
    /// <paramref name="fetchedAgo"/> null leaves no sync row (never looked up);
    /// <paramref name="aniListId"/> null with a <paramref name="fetchedAgo"/> records
    /// "AniList has no entry".</summary>
    public static async Task SeedAsync(
        AnimeTrackerDbContext db, int animeId, string airingStatus = "currently_airing",
        TimeSpan? fetchedAgo = null, int? aniListId = null, int? malTotal = null, bool inList = true)
    {
        var anime = new AnimeMetadata { Id = animeId, Title = $"Anime {animeId}", AiringStatus = airingStatus, MalTotalEpisodes = malTotal };
        anime.ResolveTotalEpisodes();
        db.AnimeMetadata.Add(anime);
        if (inList)
            db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = animeId, Status = WatchStatus.Watching });
        if (fetchedAgo is { } ago)
        {
            db.AnimeAiringSyncs.Add(new AnimeAiringSync
            {
                AnimeId = animeId,
                AniListId = aniListId,
                LastFetchedAt = DateTimeOffset.UtcNow - ago,
                RelationsFetchedAt = DateTimeOffset.UtcNow - ago,
            });
        }

        await db.SaveChangesAsync();
    }
}

/// <summary>Mirrors the real repository's replace-wholesale behaviour without a
/// database transaction or <c>ExecuteDelete</c>, which the in-memory provider
/// doesn't support.</summary>
internal sealed class InMemoryEpisodeAiringRepository(AnimeTrackerDbContext db) : IEpisodeAiringRepository
{
    public Task<int?> GetMaxAiredEpisodeAsync(int animeId, DateTimeOffset asOfUtc, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<Dictionary<int, int>> GetMaxAiredEpisodesAsync(IReadOnlyCollection<int> animeIds, DateTimeOffset asOfUtc, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<DateTimeOffset?> GetNextAiringInstantAsync(int animeId, DateTimeOffset afterUtc, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<Dictionary<int, DateTimeOffset>> GetNextAiringInstantsAsync(IReadOnlyCollection<int> animeIds, DateTimeOffset afterUtc, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<List<EpisodeAiring>> GetRowsInRangeAsync(IReadOnlyCollection<int> animeIds, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public async Task ReplaceForAnimeAsync(int animeId, IReadOnlyList<EpisodeAiring> rows, CancellationToken ct = default)
    {
        if (rows.Count == 0)
            return;

        var existing = await db.EpisodeAirings.Where(e => e.AnimeId == animeId).ToListAsync(ct);
        db.EpisodeAirings.RemoveRange(existing);
        db.EpisodeAirings.AddRange(rows);
        await db.SaveChangesAsync(ct);
    }
}

/// <summary>An AniList client answering from a script: one <see cref="ScriptedMedia"/>
/// per MAL id AniList "has", from which the single and the batched methods all
/// answer, so the two paths can be run over the same script and compared. The
/// batched methods follow the contract on <see cref="IAniListClient"/>, including
/// the cap on how many pages a schedule read pages through.</summary>
internal sealed class ScriptedAniListClient : IAniListClient
{
    public sealed class ScriptedMedia(int malId, int aniListId)
    {
        public int MalId { get; } = malId;
        public int AniListId { get; } = aniListId;
        public string? Status { get; set; } = "FINISHED";
        public int? Episodes { get; set; }
        public DateTimeOffset? NextAiringAt { get; set; }
        public List<AniListRelationEdge> Relations { get; set; } = [];
        public List<AniListEpisode> Rows { get; set; } = [];
    }

    private readonly Dictionary<int, ScriptedMedia> _byMalId = [];

    /// <summary>Rows a schedule page holds, and the pages a read may page through
    /// before it stops with anime left over (AniList's is 100 pages of 50).</summary>
    public int PageSize { get; set; } = 50;
    public int PageCap { get; set; } = 100;

    // What was asked, in order.
    public List<int> SingleLookups { get; } = [];
    public List<int> SingleSchedules { get; } = [];
    public List<int[]> LookupBatches { get; } = [];
    public List<int[]> MediaBatches { get; } = [];
    public List<int[]> ScheduleReads { get; } = [];

    // Failure injection. A request throws when this returns an exception for it
    // ("lookup", "media" or "schedule", and the ids it asks for).
    public Func<string, IReadOnlyList<int>, Exception?>? Fail { get; set; }

    /// <summary>A schedule read throws after this many anime have been called back.</summary>
    public int? FailScheduleReadAfterCompletions { get; set; }

    /// <summary>Awaited just before each anime is called back, with its AniList id.</summary>
    public Func<int, Task>? BeforeAnimeComplete { get; set; }

    public ScriptedMedia Add(int malId, int? aniListId = null, int rows = 0, string? status = "FINISHED", int? episodes = null)
    {
        var media = new ScriptedMedia(malId, aniListId ?? 900 + malId) { Status = status, Episodes = episodes };
        var first = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        media.Rows = Enumerable.Range(1, rows).Select(n => new AniListEpisode(n, first.AddDays(7 * (n - 1)))).ToList();
        _byMalId[malId] = media;
        return media;
    }

    private ScriptedMedia? ByAniListId(int aniListId) => _byMalId.Values.FirstOrDefault(m => m.AniListId == aniListId);

    private void MaybeFail(string what, IReadOnlyList<int> ids)
    {
        if (Fail?.Invoke(what, ids) is { } failure)
            throw failure;
    }

    private static AniListMediaLookup LookupOf(ScriptedMedia m) =>
        new(m.AniListId, m.Status, m.NextAiringAt, m.Relations, m.Episodes);

    // --- The single-anime methods ---

    public Task<AniListMediaLookup?> LookupByMalIdAsync(int malId, CancellationToken ct = default)
    {
        SingleLookups.Add(malId);
        MaybeFail("lookup", [malId]);
        return Task.FromResult(_byMalId.TryGetValue(malId, out var media) ? LookupOf(media) : null);
    }

    public Task<AniListScheduleResult> GetAiringScheduleAsync(int aniListId, CancellationToken ct = default)
    {
        SingleSchedules.Add(aniListId);
        MaybeFail("schedule", [aniListId]);
        // An id AniList doesn't have is a 404 there: no rows, and nothing about the media.
        return Task.FromResult(ByAniListId(aniListId) is { } m
            ? new AniListScheduleResult(m.Rows.OrderBy(r => r.AirsAtUtc).ToList(), m.Status, m.NextAiringAt, m.Episodes)
            : new AniListScheduleResult([], null, null, null));
    }

    public Task<IReadOnlyDictionary<int, AniListRelationsLookup>> GetRelationsBatchAsync(IReadOnlyList<int> malIds, CancellationToken ct = default) =>
        throw new NotImplementedException();

    // --- The batched methods ---

    public Task<IReadOnlyDictionary<int, AniListMediaLookup>> LookupBatchByMalIdsAsync(IReadOnlyList<int> malIds, CancellationToken ct = default)
    {
        if (malIds.Count > IAniListClient.MaxLookupBatch)
            throw new ArgumentException($"A lookup batch holds at most {IAniListClient.MaxLookupBatch} ids.");

        LookupBatches.Add(malIds.ToArray());
        MaybeFail("lookup", malIds);
        IReadOnlyDictionary<int, AniListMediaLookup> found = malIds
            .Where(_byMalId.ContainsKey)
            .ToDictionary(id => id, id => LookupOf(_byMalId[id]));
        return Task.FromResult(found);
    }

    public Task<IReadOnlyDictionary<int, AniListMediaState>> GetMediaBatchAsync(IReadOnlyList<int> aniListIds, CancellationToken ct = default)
    {
        if (aniListIds.Count > IAniListClient.MaxMediaBatch)
            throw new ArgumentException($"A media batch holds at most {IAniListClient.MaxMediaBatch} ids.");

        MediaBatches.Add(aniListIds.ToArray());
        MaybeFail("media", aniListIds);
        IReadOnlyDictionary<int, AniListMediaState> found = aniListIds
            .Select(ByAniListId)
            .OfType<ScriptedMedia>()
            .ToDictionary(m => m.AniListId, m => new AniListMediaState(m.AniListId, m.Status, m.NextAiringAt, m.Episodes));
        return Task.FromResult(found);
    }

    public async Task<IReadOnlyList<int>> GetAiringSchedulesAsync(
        IReadOnlyList<int> aniListIds, Func<int, IReadOnlyList<AniListEpisode>, Task> onAnimeComplete, CancellationToken ct = default)
    {
        ScheduleReads.Add(aniListIds.ToArray());
        MaybeFail("schedule", aniListIds);

        var requested = aniListIds.Distinct().OrderBy(id => id).ToList();
        var completions = 0;

        async Task CompleteAsync(int aniListId, IReadOnlyList<AniListEpisode> rows)
        {
            if (FailScheduleReadAfterCompletions is { } limit && completions >= limit)
                throw new HttpRequestException("AniList went away part-way through the read.");

            if (BeforeAnimeComplete is not null)
                await BeforeAnimeComplete(aniListId);
            await onAnimeComplete(aniListId, rows);
            completions++;
        }

        // Sorted by media and then time, paged at PageSize, at most PageCap pages.
        var sorted = requested
            .Select(ByAniListId)
            .OfType<ScriptedMedia>()
            .SelectMany(m => m.Rows.OrderBy(r => r.AirsAtUtc).Select(r => (m.AniListId, Row: r)))
            .ToList();
        var readable = sorted.Take(PageSize * PageCap).ToList();
        var endedEarly = sorted.Count > readable.Count;

        var completed = new HashSet<int>();
        foreach (var block in readable.GroupBy(x => x.AniListId).ToList())
        {
            var isLast = block.Key == readable[^1].AniListId;
            if (isLast && endedEarly)
                break; // its rows go on past the last page this read may take

            completed.Add(block.Key);
            await CompleteAsync(block.Key, block.Select(x => x.Row).ToList());
        }

        if (endedEarly)
            return requested.Where(id => !completed.Contains(id)).ToList();

        // Read to the end: what never appeared has no rows.
        foreach (var id in requested.Where(id => !completed.Contains(id)))
            await CompleteAsync(id, []);
        return [];
    }
}
