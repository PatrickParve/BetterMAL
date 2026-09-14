using System.Text.Json;
using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Dashboard;
using AnimeTracker.Api.Services.Entries;
using AnimeTracker.Api.Services.Library;
using AnimeTracker.Api.Services.Profile;
using AnimeTracker.Api.Services.Ranking;
using AnimeTracker.Api.Services.Recap;
using AnimeTracker.Api.Services.Scheduling;
using AnimeTracker.Api.Services.Season;
using AnimeTracker.Api.Services.Series;
using AnimeTracker.Api.Services.Sync;
using AnimeTracker.Api.Tests.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimeTracker.Api.Tests.Services.Library;

/// <summary>my-list-dashboard-read-performance design.md D3: guards
/// <c>UserAnimeEntryRepository.GetAllForListViewAsync</c>'s field list for the
/// four pages that call it — My List, the dashboard, Profile and Recap. Each
/// fact runs one page's read twice over identically seeded data: once
/// through the lean projection, once through <see cref="FullReadRepository"/>,
/// which sends the same call to the full <c>Include</c> read instead. A
/// failure here means one of those pages started reading an
/// <see cref="AnimeMetadata"/> column the projection leaves at its default —
/// the fix is to add that field to <c>GetAllForListViewAsync</c>'s projection
/// and to its doc comment (design.md D2), not to loosen this test. A fifth
/// service moved onto the lean read belongs here too.</summary>
public class ListViewReadParityTests
{
    private const int ReopenCandidateAnimeId = 1;
    private const int CompleteCandidateAnimeId = 2;
    private const int HandOrderedAnimeId = 3;

    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task MyListReadIsUnaffectedByTheLeanProjection()
    {
        var now = DateTimeOffset.UtcNow;
        var schedule = BuildScheduleService(now);

        var (lean, full) = await RunBothAsync(now, async (db, repo, sync, _) =>
        {
            var service = new MyListService(
                repo, new TopAnimeSelectionRepository(db), schedule,
                new AiringWatchStatusService(db, sync, NullLogger<AiringWatchStatusService>.Instance));
            return await service.GetMyListAsync();
        });

        AssertParity(lean, full);

        // The re-open and complete candidates actually moved on both reads,
        // so the batched settle (R5) ran here too, not just the projection
        // (N4).
        Assert.Equal(WatchStatus.Watching, lean.Entries.Single(e => e.AnimeId == ReopenCandidateAnimeId).Status);
        Assert.Equal(WatchStatus.Completed, lean.Entries.Single(e => e.AnimeId == CompleteCandidateAnimeId).Status);
    }

    [Fact]
    public async Task DashboardReadIsUnaffectedByTheLeanProjection()
    {
        var now = DateTimeOffset.UtcNow;
        var schedule = BuildScheduleService(now);

        var (lean, full) = await RunBothAsync(now, async (db, repo, sync, _) =>
        {
            var service = new MainDashboardService(
                repo, schedule,
                new AiringWatchStatusService(db, sync, NullLogger<AiringWatchStatusService>.Instance),
                new FakeBroadcastLocalTimeConverter());
            return await service.GetDashboardAsync();
        });

        AssertParity(lean, full);

        Assert.Equal(WatchStatus.Watching, lean.Entries.Single(e => e.AnimeId == ReopenCandidateAnimeId).Status);
        Assert.Equal(WatchStatus.Completed, lean.Entries.Single(e => e.AnimeId == CompleteCandidateAnimeId).Status);
    }

    [Fact]
    public async Task ProfileReadIsUnaffectedByTheLeanProjection()
    {
        var now = DateTimeOffset.UtcNow;
        var schedule = BuildScheduleService(now);

        var (lean, full) = await RunBothAsync(now, async (db, repo, _, trigger) =>
            await BuildProfileService(db, repo, schedule, trigger).GetProfileAsync());

        AssertParity(lean, full);
    }

    [Fact]
    public async Task TopAnimeSectionAllReadIsUnaffectedByTheLeanProjection()
    {
        var now = DateTimeOffset.UtcNow;
        var schedule = BuildScheduleService(now);

        var (lean, full) = await RunBothAsync(now, async (db, repo, _, trigger) =>
            await BuildProfileService(db, repo, schedule, trigger).GetTopAnimeSectionAsync(TopAnimeMediaTypeScope.All));

        AssertParity(lean, full);
    }

    [Fact]
    public async Task TopAnimeSectionMovieReadIsUnaffectedByTheLeanProjection()
    {
        var now = DateTimeOffset.UtcNow;
        var schedule = BuildScheduleService(now);

        var (lean, full) = await RunBothAsync(now, async (db, repo, _, trigger) =>
            await BuildProfileService(db, repo, schedule, trigger).GetTopAnimeSectionAsync("movie"));

        AssertParity(lean, full);
    }

    [Fact]
    public async Task RewatchedSectionReadIsUnaffectedByTheLeanProjection()
    {
        var now = DateTimeOffset.UtcNow;
        var schedule = BuildScheduleService(now);

        var (lean, full) = await RunBothAsync(now, async (db, repo, _, trigger) =>
            await BuildProfileService(db, repo, schedule, trigger).GetRewatchedSectionAsync(TopAnimeMediaTypeScope.All));

        AssertParity(lean, full);
    }

    // Also exercises the on-read series-build backfill (ScheduleMissingSeriesBuildsAsync),
    // which now reads through the lean projection too — AssertParity compares
    // the enqueued series-build ids as well as the section itself.
    [Fact]
    public async Task TopSeriesSectionReadIsUnaffectedByTheLeanProjection()
    {
        var now = DateTimeOffset.UtcNow;
        var schedule = BuildScheduleService(now);

        var (lean, full) = await RunBothAsync(now, async (db, repo, _, trigger) =>
            await BuildProfileService(db, repo, schedule, trigger).GetTopSeriesSectionAsync());

        AssertParity(lean, full);
    }

    [Fact]
    public async Task RecapMultiYearAiredReadIsUnaffectedByTheLeanProjection()
    {
        var now = DateTimeOffset.UtcNow;
        var currentYear = DateOnly.FromDateTime(now.UtcDateTime).Year;
        var period = RecapPeriod.MultiYear(currentYear - 12, currentYear);

        var (lean, full) = await RunBothAsync(now, async (db, repo, _, _) =>
            await BuildRecapService(db, repo).GetRecapAsync(period, RecapTimeFilter.Aired));

        AssertParity(lean, full);
    }

    [Fact]
    public async Task RecapYearlyWatchedReadIsUnaffectedByTheLeanProjection()
    {
        var now = DateTimeOffset.UtcNow;
        var currentYear = DateOnly.FromDateTime(now.UtcDateTime).Year;
        var period = RecapPeriod.Yearly(currentYear);

        var (lean, full) = await RunBothAsync(now, async (db, repo, _, _) =>
            await BuildRecapService(db, repo).GetRecapAsync(period, RecapTimeFilter.Watched));

        AssertParity(lean, full);
    }

    [Fact]
    public async Task RecapSeasonReadIsUnaffectedByTheLeanProjection()
    {
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var (year, season) = SeasonCalendar.GetSeasonFor(today);
        var period = RecapPeriod.OfSeason(year, season);

        var (lean, full) = await RunBothAsync(now, async (db, repo, _, _) =>
            await BuildRecapService(db, repo).GetRecapAsync(period, RecapTimeFilter.Aired));

        AssertParity(lean, full);
    }

    // my-list-dashboard-read-performance design.md D3 (tasks.md 2.5): every
    // public writable AnimeMetadata property except RelatedAnime/UserEntry
    // must reach a non-default value somewhere in SeedAsync's fixture, or a
    // read that starts leaning on a column the lean projection leaves out
    // could slip past every fact above without ever producing a mismatch.
    [Fact]
    public async Task FixtureSetsEveryColumn()
    {
        using var db = CreateDb();
        await SeedAsync(db, DateTimeOffset.UtcNow);
        var animeList = await db.AnimeMetadata.AsNoTracking().ToListAsync();

        foreach (var property in AnimeMetadataFixture.CheckedProperties)
        {
            var reachesNonDefault = animeList.Any(a => !AnimeMetadataFixture.IsDefaultOrEmpty(property.GetValue(a)));
            Assert.True(
                reachesNonDefault,
                $"No anime in ListViewReadParityTests's fixture sets {property.Name} to a non-default value. " +
                "Extend SeedAsync to cover it, so a future read of this column would actually be caught above.");
        }
    }

    private static FakeEpisodeScheduleService BuildScheduleService(DateTimeOffset now) => new(
        airedSoFarByAnimeId: new Dictionary<int, int> { [ReopenCandidateAnimeId] = 6, [9] = 5, [17] = 6 },
        nextAiringInstantByAnimeId: new Dictionary<int, DateTimeOffset> { [ReopenCandidateAnimeId] = now.AddDays(2) },
        airingTodayByAnimeId: new Dictionary<int, ResolvedEpisode> { [9] = new(new TimeOnly(20, 0), 6) });

    private static ProfileService BuildProfileService(
        AnimeTrackerDbContext db, IUserAnimeEntryRepository repo, IEpisodeScheduleService schedule, ISeriesBuildTrigger trigger) =>
        new(
            repo,
            new ActivityLogRepository(db),
            new TopAnimeSelectionRepository(db),
            new SeriesRankingLookup(db),
            trigger,
            schedule,
            new AnimeRankingService(repo, new TopAnimeSelectionRepository(db)));

    private static RecapService BuildRecapService(AnimeTrackerDbContext db, IUserAnimeEntryRepository repo) =>
        new(repo, new ActivityLogRepository(db), new TopAnimeSelectionRepository(db), new FakeBroadcastLocalTimeConverter());

    private static void AssertParity<TResult>(StoreResult<TResult> lean, StoreResult<TResult> full)
    {
        Assert.Equal(JsonSerializer.Serialize(full.Result), JsonSerializer.Serialize(lean.Result));
        Assert.Equal(full.Entries, lean.Entries);
        Assert.Equal(full.ActivityLogs, lean.ActivityLogs);
        Assert.Equal(full.ScheduledSyncIds, lean.ScheduledSyncIds);
        Assert.Equal(full.EnqueuedSeriesBuildIds, lean.EnqueuedSeriesBuildIds);
    }

    private static async Task<(StoreResult<TResult> Lean, StoreResult<TResult> Full)> RunBothAsync<TResult>(
        DateTimeOffset now,
        Func<AnimeTrackerDbContext, IUserAnimeEntryRepository, RecordingEntrySyncScheduler, RecordingSeriesBuildTrigger, Task<TResult>> run)
    {
        var lean = await RunOneAsync(now, useFullRead: false, run);
        var full = await RunOneAsync(now, useFullRead: true, run);
        return (lean, full);
    }

    private static async Task<StoreResult<TResult>> RunOneAsync<TResult>(
        DateTimeOffset now,
        bool useFullRead,
        Func<AnimeTrackerDbContext, IUserAnimeEntryRepository, RecordingEntrySyncScheduler, RecordingSeriesBuildTrigger, Task<TResult>> run)
    {
        using var db = CreateDb();
        await SeedAsync(db, now);

        var inner = new UserAnimeEntryRepository(db);
        IUserAnimeEntryRepository repository = useFullRead ? new FullReadRepository(inner) : inner;
        var syncScheduler = new RecordingEntrySyncScheduler();
        var seriesBuildTrigger = new RecordingSeriesBuildTrigger();

        var result = await run(db, repository, syncScheduler, seriesBuildTrigger);

        var entries = (await db.UserAnimeEntries.AsNoTracking().OrderBy(e => e.AnimeId).ToListAsync())
            .Select(e => (e.AnimeId, e.Status, e.CompletedAt, e.PendingSync))
            .ToList();
        var activityLogs = (await db.ActivityLogs.AsNoTracking().OrderBy(l => l.AnimeId).ThenBy(l => l.ChangeType).ToListAsync())
            .Select(l => (l.AnimeId, l.ChangeType, l.ChangeDetail))
            .ToList();

        return new StoreResult<TResult>(
            result, entries, activityLogs,
            syncScheduler.Scheduled.OrderBy(id => id).ToList(),
            seriesBuildTrigger.Enqueued.OrderBy(id => id).ToList());
    }

    /// <summary>The fixture every fact above seeds into two fresh InMemory
    /// databases (design.md D3). Ids and dates are deliberately relative to
    /// <paramref name="now"/> rather than fixed calendar dates, so the recap
    /// periods each fact resolves (also relative to <paramref name="now"/>)
    /// keep covering the same entries whenever this runs.</summary>
    private static async Task SeedAsync(AnimeTrackerDbContext db, DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var (currentYear, currentSeason) = SeasonCalendar.GetSeasonFor(today);
        var currentSeasonStart = SeasonCalendar.SeasonStart(currentYear, currentSeason);

        // R5: re-opens on this read (Completed, still currently airing, below its total).
        var reopenCandidate = new AnimeMetadata
        {
            Id = ReopenCandidateAnimeId, Title = "Reopen Candidate", MediaType = "tv",
            AiringStatus = "currently_airing", TotalEpisodes = 24, AiredFrom = today.AddYears(-1),
        };
        // R5: completes on this read (Watching, finished airing, at its total).
        var completeCandidate = new AnimeMetadata
        {
            Id = CompleteCandidateAnimeId, Title = "Complete Candidate", MediaType = "tv",
            AiringStatus = "finished_airing", TotalEpisodes = 12, AverageEpisodeDurationSeconds = 1400,
            AiredFrom = today.AddYears(-10),
        };
        // RankBand.HandOrdered, with a stored TopAnimeSelection position.
        var handOrdered = new AnimeMetadata
        {
            Id = HandOrderedAnimeId, Title = "Hand Ordered", MediaType = "tv", AiringStatus = "finished_airing",
            TotalEpisodes = 24, AverageEpisodeDurationSeconds = 1400, MalScore = 8.5,
            AiredFrom = today.AddYears(-9).AddMonths(3),
        };
        // RankBand.ShortForm.
        var musicShortForm = new AnimeMetadata
        {
            Id = 4, Title = "Music Short Form", MediaType = "music", AiringStatus = "finished_airing",
            TotalEpisodes = 1, AverageEpisodeDurationSeconds = 90, MalScore = 7.0, AiredFrom = today.AddYears(-6),
        };
        // RankBand.Dropped.
        var droppedRanked = new AnimeMetadata
        {
            Id = 5, Title = "Dropped Ranked", MediaType = "tv", AiringStatus = "finished_airing",
            TotalEpisodes = 12, AverageEpisodeDurationSeconds = 1400, MalScore = 6.0, AiredFrom = today.AddYears(-4),
        };
        // Unranked via PlanToWatch, despite carrying a score.
        var planToWatchScored = new AnimeMetadata
        {
            Id = 6, Title = "Plan To Watch Scored", MediaType = "tv", AiringStatus = "finished_airing",
            TotalEpisodes = 12, AverageEpisodeDurationSeconds = 1400, MalScore = 6.5, AiredFrom = today.AddYears(-9),
        };
        // Unranked via not_yet_aired, despite carrying a score.
        var notYetAiredScored = new AnimeMetadata
        {
            Id = 7, Title = "Not Yet Aired Scored", MediaType = "tv", AiringStatus = "not_yet_aired",
            MalScore = 7.0,
        };
        // A movie with a stored duration.
        var movieWithDuration = new AnimeMetadata
        {
            Id = 8, Title = "Movie With Duration", MediaType = "movie", AiringStatus = "finished_airing",
            TotalEpisodes = 1, AverageEpisodeDurationSeconds = 7200, MalScore = 7.8, AiredFrom = today.AddYears(-3),
        };
        // A tv entry with a null duration (the 24-minute WatchMath fallback);
        // also airs "today" and carries a next-airing instant.
        var currentlyAiringKnownTotal = new AnimeMetadata
        {
            Id = 9, Title = "Currently Airing Known Total", MediaType = "tv", AiringStatus = "currently_airing",
            TotalEpisodes = 24, AiredFrom = currentSeasonStart,
        };
        // A rewatched entry.
        var rewatched = new AnimeMetadata
        {
            Id = 10, Title = "Rewatched", MediaType = "tv", AiringStatus = "finished_airing",
            TotalEpisodes = 13, AverageEpisodeDurationSeconds = 1380, MalScore = 9.0, AiredFrom = today.AddYears(-7),
        };
        // Reaches the dashboard's AiredFrom-in-current-season branch without
        // being currently_airing itself.
        var currentSeasonFinished = new AnimeMetadata
        {
            Id = 11, Title = "Current Season Finished", MediaType = "tv", AiringStatus = "finished_airing",
            TotalEpisodes = 1, AverageEpisodeDurationSeconds = 1400, MalScore = 5.5, AiredFrom = currentSeasonStart,
        };
        // Filler scored entries, purely to clear ScoreDivergence.MinimumRatedPairs
        // and to spread AiredFrom across several distinct years.
        var fillerA = new AnimeMetadata
        {
            Id = 12, Title = "Filler Scored A", MediaType = "tv", AiringStatus = "finished_airing",
            TotalEpisodes = 12, AverageEpisodeDurationSeconds = 1400, MalScore = 8.2, AiredFrom = today.AddYears(-9),
        };
        var fillerB = new AnimeMetadata
        {
            Id = 13, Title = "Filler Scored B", MediaType = "tv", AiringStatus = "finished_airing",
            TotalEpisodes = 12, AverageEpisodeDurationSeconds = 1400, MalScore = 4.0, AiredFrom = today.AddYears(-6),
        };
        var fillerC = new AnimeMetadata
        {
            Id = 14, Title = "Filler Scored C", MediaType = "tv", AiringStatus = "finished_airing",
            TotalEpisodes = 12, AverageEpisodeDurationSeconds = 1400, MalScore = 3.0, AiredFrom = today.AddYears(-8),
        };
        var fillerD = new AnimeMetadata
        {
            Id = 15, Title = "Filler Scored D", MediaType = "tv", AiringStatus = "finished_airing",
            TotalEpisodes = 12, AverageEpisodeDurationSeconds = 1400, MalScore = 8.6, AiredFrom = today.AddYears(-5),
        };
        // Recap's "watched" filter via CompletedAt inside the current year.
        var watchedPeriodCompletion = new AnimeMetadata
        {
            Id = 16, Title = "Watched Period Completion", MediaType = "tv", AiringStatus = "finished_airing",
            TotalEpisodes = 10, AverageEpisodeDurationSeconds = 1400, AiredFrom = today.AddYears(-10),
        };
        // Recap's "watched" filter via a logged episode-progress row, not a
        // completion date.
        var loggedProgress = new AnimeMetadata
        {
            Id = 17, Title = "Logged Progress", MediaType = "tv", AiringStatus = "currently_airing",
            TotalEpisodes = 24, AiredFrom = today.AddYears(-1),
        };
        // Profile's unresolved-episode list: currently airing, unknown total,
        // and deliberately absent from the fake schedule service's aired dictionary.
        var unresolvedTotal = new AnimeMetadata
        {
            Id = 18, Title = "Unresolved Total", MediaType = "tv", AiringStatus = "currently_airing",
            AiredFrom = currentSeasonStart.AddDays(5),
        };
        // Every checked AnimeMetadata property at a distinctive value —
        // FixtureSetsEveryColumn's whole reason to pass.
        var fullyPopulated = AnimeMetadataFixture.FullyPopulatedAnime(19);

        db.AnimeMetadata.AddRange(
            reopenCandidate, completeCandidate, handOrdered, musicShortForm, droppedRanked, planToWatchScored,
            notYetAiredScored, movieWithDuration, currentlyAiringKnownTotal, rewatched, currentSeasonFinished,
            fillerA, fillerB, fillerC, fillerD, watchedPeriodCompletion, loggedProgress, unresolvedTotal, fullyPopulated);

        db.UserAnimeEntries.AddRange(
            new UserAnimeEntry { AnimeId = ReopenCandidateAnimeId, Anime = reopenCandidate, Status = WatchStatus.Completed, EpisodesWatched = 5 },
            new UserAnimeEntry { AnimeId = CompleteCandidateAnimeId, Anime = completeCandidate, Status = WatchStatus.Watching, EpisodesWatched = 12 },
            new UserAnimeEntry
            {
                AnimeId = HandOrderedAnimeId, Anime = handOrdered, Status = WatchStatus.Completed, EpisodesWatched = 24,
                MyScore = 9, CompletedAt = handOrdered.AiredFrom!.Value.AddMonths(2),
            },
            new UserAnimeEntry
            {
                AnimeId = 4, Anime = musicShortForm, Status = WatchStatus.Completed, EpisodesWatched = 1,
                MyScore = 7, CompletedAt = musicShortForm.AiredFrom,
            },
            new UserAnimeEntry { AnimeId = 5, Anime = droppedRanked, Status = WatchStatus.Dropped, EpisodesWatched = 3, MyScore = 4 },
            new UserAnimeEntry { AnimeId = 6, Anime = planToWatchScored, Status = WatchStatus.PlanToWatch, EpisodesWatched = 0, MyScore = 6 },
            new UserAnimeEntry { AnimeId = 7, Anime = notYetAiredScored, Status = WatchStatus.Watching, EpisodesWatched = 0, MyScore = 7 },
            new UserAnimeEntry
            {
                AnimeId = 8, Anime = movieWithDuration, Status = WatchStatus.Completed, EpisodesWatched = 1,
                MyScore = 8, CompletedAt = movieWithDuration.AiredFrom,
            },
            new UserAnimeEntry { AnimeId = 9, Anime = currentlyAiringKnownTotal, Status = WatchStatus.Watching, EpisodesWatched = 3 },
            new UserAnimeEntry
            {
                AnimeId = 10, Anime = rewatched, Status = WatchStatus.Completed, EpisodesWatched = 13,
                MyScore = 10, RewatchCount = 2, CompletedAt = rewatched.AiredFrom!.Value.AddMonths(1),
            },
            new UserAnimeEntry
            {
                AnimeId = 11, Anime = currentSeasonFinished, Status = WatchStatus.Completed, EpisodesWatched = 1,
                MyScore = 5, CompletedAt = currentSeasonStart.AddDays(1),
            },
            new UserAnimeEntry
            {
                AnimeId = 12, Anime = fillerA, Status = WatchStatus.Completed, EpisodesWatched = 12,
                MyScore = 3, CompletedAt = fillerA.AiredFrom!.Value.AddMonths(1),
            },
            new UserAnimeEntry
            {
                AnimeId = 13, Anime = fillerB, Status = WatchStatus.Completed, EpisodesWatched = 12,
                MyScore = 9, CompletedAt = fillerB.AiredFrom!.Value.AddMonths(1),
            },
            new UserAnimeEntry
            {
                AnimeId = 14, Anime = fillerC, Status = WatchStatus.Completed, EpisodesWatched = 12,
                MyScore = 2, CompletedAt = fillerC.AiredFrom!.Value.AddMonths(1),
            },
            new UserAnimeEntry
            {
                AnimeId = 15, Anime = fillerD, Status = WatchStatus.Completed, EpisodesWatched = 12,
                MyScore = 8, CompletedAt = fillerD.AiredFrom!.Value.AddMonths(1),
            },
            new UserAnimeEntry
            {
                AnimeId = 16, Anime = watchedPeriodCompletion, Status = WatchStatus.Completed, EpisodesWatched = 10,
                CompletedAt = new DateOnly(currentYear, 1, 15),
            },
            new UserAnimeEntry { AnimeId = 17, Anime = loggedProgress, Status = WatchStatus.Watching, EpisodesWatched = 5 },
            new UserAnimeEntry { AnimeId = 18, Anime = unresolvedTotal, Status = WatchStatus.Watching, EpisodesWatched = 5 },
            new UserAnimeEntry
            {
                AnimeId = 19, Anime = fullyPopulated, Status = WatchStatus.Completed, EpisodesWatched = 31,
                MyScore = 6, StartedAt = new DateOnly(2018, 1, 20), CompletedAt = new DateOnly(2018, 3, 1),
            });

        db.TopAnimeSelections.Add(new TopAnimeSelection { AnimeId = HandOrderedAnimeId, Position = 0 });

        // Recap's "watched" filter via logged progress rather than a
        // completion date (RecapWatchLog.BuildMap): a +3 increase, safely
        // inside the current year's UTC instant range.
        db.ActivityLogs.Add(new ActivityLog
        {
            AnimeId = 17, Anime = loggedProgress, Timestamp = new DateTimeOffset(currentYear, 2, 1, 0, 0, 0, TimeSpan.Zero),
            ChangeType = ActivityChangeType.EpisodeIncremented, ChangeDetail = "Episode 5", PreviousEpisodesWatched = 2,
        });

        await db.SaveChangesAsync();
    }

    private sealed record StoreResult<TResult>(
        TResult Result,
        List<(int AnimeId, WatchStatus Status, DateOnly? CompletedAt, bool PendingSync)> Entries,
        List<(int AnimeId, ActivityChangeType ChangeType, string? ChangeDetail)> ActivityLogs,
        List<int> ScheduledSyncIds,
        List<int> EnqueuedSeriesBuildIds);

    // design.md D3: delegates everything, but sends GetAllForListViewAsync to
    // the inner repository's full GetAllAsync read instead of its own lean
    // projection — the "full read" side of every parity comparison above.
    private sealed class FullReadRepository(UserAnimeEntryRepository inner) : IUserAnimeEntryRepository
    {
        public Task<UserAnimeEntry?> GetByAnimeIdAsync(int animeId, CancellationToken ct = default) =>
            inner.GetByAnimeIdAsync(animeId, ct);
        public Task<List<UserAnimeEntry>> GetAllAsync(CancellationToken ct = default) => inner.GetAllAsync(ct);
        public Task<List<UserAnimeEntry>> GetAllForListViewAsync(CancellationToken ct = default) => inner.GetAllAsync(ct);
        public Task<(int PendingCount, int HeldCount, DateTimeOffset? LastSyncedAt)> GetSyncStatusAsync(CancellationToken ct = default) =>
            inner.GetSyncStatusAsync(ct);
    }

    private sealed class RecordingEntrySyncScheduler : IEntrySyncScheduler
    {
        public List<int> Scheduled { get; } = [];
        public void ScheduleSync(int animeId) => Scheduled.Add(animeId);
    }

    private sealed class RecordingSeriesBuildTrigger : ISeriesBuildTrigger
    {
        public List<int> Enqueued { get; } = [];
        public void Enqueue(int animeId) => Enqueued.Add(animeId);
        public Task<int> WaitAsync(CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class FakeEpisodeScheduleService(
        IReadOnlyDictionary<int, int> airedSoFarByAnimeId,
        IReadOnlyDictionary<int, DateTimeOffset> nextAiringInstantByAnimeId,
        IReadOnlyDictionary<int, ResolvedEpisode> airingTodayByAnimeId) : IEpisodeScheduleService
    {
        public Task<ResolvedEpisode?> ResolveOnLocalDateAsync(AnimeMetadata anime, DateOnly localDate, CancellationToken ct = default) =>
            Task.FromResult(airingTodayByAnimeId.TryGetValue(anime.Id, out var episode) ? episode : null);
        public Task<DateTimeOffset?> NextAiringInstantAsync(AnimeMetadata anime, DateTimeOffset afterUtc, CancellationToken ct = default) =>
            Task.FromResult(nextAiringInstantByAnimeId.TryGetValue(anime.Id, out var instant) ? instant : (DateTimeOffset?)null);
        public Task<int?> EpisodesAiredAsOfAsync(AnimeMetadata anime, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            Task.FromResult(airedSoFarByAnimeId.TryGetValue(anime.Id, out var aired) ? (int?)aired : null);
        public Task<Dictionary<int, int>> EpisodesAiredAsOfAsync(IReadOnlyCollection<AnimeMetadata> anime, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            Task.FromResult(anime.Where(a => airedSoFarByAnimeId.ContainsKey(a.Id)).ToDictionary(a => a.Id, a => airedSoFarByAnimeId[a.Id]));
        public Task<Dictionary<int, int>> EpisodesAiredAsOfAsync(IReadOnlyCollection<int> animeIds, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            Task.FromResult(animeIds.Where(airedSoFarByAnimeId.ContainsKey).ToDictionary(id => id, id => airedSoFarByAnimeId[id]));
    }

    private sealed class FakeBroadcastLocalTimeConverter : IBroadcastLocalTimeConverter
    {
        public DateOnly GetStartOfWeek(DateOnly referenceDate) => referenceDate;
        public DateOnly GetLocalDate(DateTimeOffset instantUtc) => DateOnly.FromDateTime(instantUtc.UtcDateTime);
        public TimeOnly GetLocalTime(DateTimeOffset instantUtc) => TimeOnly.FromDateTime(instantUtc.UtcDateTime);
        public DateTimeOffset LocalMidnightUtc(DateOnly localDate) => new(localDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        public (DayOfWeek LocalDayOfWeek, TimeOnly LocalTime) ConvertBroadcastSlot(DayOfWeek jstDayOfWeek, TimeOnly jstTime, DateTimeOffset referenceUtc) =>
            (jstDayOfWeek, jstTime);
    }
}
