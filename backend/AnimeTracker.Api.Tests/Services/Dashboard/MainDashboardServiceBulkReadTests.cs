using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Dashboard;
using AnimeTracker.Api.Services.Entries;
using AnimeTracker.Api.Services.Scheduling;

namespace AnimeTracker.Api.Tests.Services.Dashboard;

// episode-airing-data / fix-score-reveal-and-nplus1 tasks.md 4.2:
// MainDashboardService must resolve all three per-entry airing facts — the
// current-season aired counts, the airing-today slots, and the
// currently-watching countdowns — through their bulk reads (design.md D6/D7),
// each exactly once, never per entry.
public class MainDashboardServiceBulkReadTests
{
    private static readonly TimeSpan NextInstantOffset = TimeSpan.FromHours(30);

    [Fact]
    public async Task AllThreeFactsComeFromTheirBulkReadsExactlyOnce()
    {
        List<UserAnimeEntry> entries =
        [
            // Behind on episodes, so it stays in Currently watching and is
            // eligible for the countdown read.
            new()
            {
                AnimeId = 1, Status = WatchStatus.Watching, EpisodesWatched = 2,
                Anime = new AnimeMetadata { Id = 1, Title = "Airing", AiringStatus = "currently_airing" },
            },
            // Already Completed, so it never reaches Currently watching, but
            // it's still currently_airing metadata-wise, so it belongs in the
            // current-season section.
            new()
            {
                AnimeId = 2, Status = WatchStatus.Completed, EpisodesWatched = 12,
                Anime = new AnimeMetadata { Id = 2, Title = "AlsoAiring", AiringStatus = "currently_airing" },
            },
        ];
        var airedSoFarByAnimeId = new Dictionary<int, int> { [1] = 5, [2] = 12 };
        var airingTodayByAnimeId = new Dictionary<int, ResolvedEpisode> { [1] = new(new TimeOnly(21, 0), 5) };
        var scheduleService = new PoisonedEpisodeScheduleService(airedSoFarByAnimeId, airingTodayByAnimeId, animeIdWithNextEpisode: 1, NextInstantOffset);

        var service = new MainDashboardService(
            new FakeUserAnimeEntryRepository(entries), scheduleService, new NullAiringWatchStatusService(), new FakeBroadcastLocalTimeConverter());

        var dashboard = await service.GetDashboardAsync();

        // Currently-watching countdown.
        var watching = Assert.Single(dashboard.CurrentlyWatching);
        Assert.Equal(5, watching.EpisodesAired);
        Assert.Equal(new NextEpisodeEtaDto(1, 6), watching.NextEpisode); // ceil(30h) = 1d 6h

        // Airing-today slot.
        var airingToday = Assert.Single(dashboard.AiringToday);
        Assert.Equal(1, airingToday.AnimeId);
        Assert.Equal(5, airingToday.EpisodeNumber);

        // Current-season aired counts.
        Assert.Equal(5, dashboard.CurrentSeason.Single(i => i.AnimeId == 1).EpisodesAired);
        Assert.Equal(12, dashboard.CurrentSeason.Single(i => i.AnimeId == 2).EpisodesAired);

        Assert.Equal(1, scheduleService.BulkAiredCallCount);
        Assert.Equal(1, scheduleService.BulkResolveCallCount);
        Assert.Equal(1, scheduleService.BulkNextInstantCallCount);
    }

    // Throws on every per-anime member; only the three bulk overloads the
    // dashboard actually needs answer. The only way this test passes is if
    // MainDashboardService resolves each fact through its bulk read.
    private sealed class PoisonedEpisodeScheduleService(
        Dictionary<int, int> airedSoFarByAnimeId,
        Dictionary<int, ResolvedEpisode> airingTodayByAnimeId,
        int animeIdWithNextEpisode,
        TimeSpan nextInstantOffset) : IEpisodeScheduleService
    {
        public int BulkAiredCallCount { get; private set; }
        public int BulkResolveCallCount { get; private set; }
        public int BulkNextInstantCallCount { get; private set; }

        public Task<ResolvedEpisode?> ResolveOnLocalDateAsync(AnimeMetadata anime, DateOnly localDate, CancellationToken ct = default) =>
            throw new InvalidOperationException("The dashboard must never resolve a single anime's airing-today slot.");
        public Task<DateTimeOffset?> NextAiringInstantAsync(AnimeMetadata anime, DateTimeOffset afterUtc, CancellationToken ct = default) =>
            throw new InvalidOperationException("The dashboard must never fetch a single anime's next-airing instant.");
        public Task<int?> EpisodesAiredAsOfAsync(AnimeMetadata anime, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            throw new InvalidOperationException("The dashboard must never resolve a single anime's aired count.");

        public Task<Dictionary<int, ResolvedEpisode>> ResolveOnLocalDateAsync(IReadOnlyCollection<AnimeMetadata> anime, DateOnly localDate, CancellationToken ct = default)
        {
            BulkResolveCallCount++;
            return Task.FromResult(anime.Where(a => airingTodayByAnimeId.ContainsKey(a.Id)).ToDictionary(a => a.Id, a => airingTodayByAnimeId[a.Id]));
        }

        public Task<Dictionary<int, DateTimeOffset>> NextAiringInstantAsync(IReadOnlyCollection<AnimeMetadata> anime, DateTimeOffset afterUtc, CancellationToken ct = default)
        {
            BulkNextInstantCallCount++;
            return Task.FromResult(anime.Where(a => a.Id == animeIdWithNextEpisode).ToDictionary(a => a.Id, _ => afterUtc + nextInstantOffset));
        }

        public Task<Dictionary<int, int>> EpisodesAiredAsOfAsync(IReadOnlyCollection<AnimeMetadata> anime, DateTimeOffset nowUtc, CancellationToken ct = default)
        {
            BulkAiredCallCount++;
            return Task.FromResult(anime.Where(a => airedSoFarByAnimeId.ContainsKey(a.Id)).ToDictionary(a => a.Id, a => airedSoFarByAnimeId[a.Id]));
        }

        public Task<Dictionary<int, int>> EpisodesAiredAsOfAsync(IReadOnlyCollection<int> animeIds, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            throw new InvalidOperationException("The dashboard must use the AnimeMetadata overload, not the id one.");
    }

    private sealed class NullAiringWatchStatusService : IAiringWatchStatusService
    {
        public Task SettleAsync(IReadOnlyCollection<UserAnimeEntry> entries, IReadOnlyDictionary<int, int> airedSoFarByAnimeId, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeUserAnimeEntryRepository(List<UserAnimeEntry> entries) : IUserAnimeEntryRepository
    {
        public Task<UserAnimeEntry?> GetByAnimeIdAsync(int animeId, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<List<UserAnimeEntry>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(entries);
        public Task<(int PendingCount, int HeldCount, DateTimeOffset? LastSyncedAt)> GetSyncStatusAsync(CancellationToken ct = default) =>
            throw new NotImplementedException();
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
