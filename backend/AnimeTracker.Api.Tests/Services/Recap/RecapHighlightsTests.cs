using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Recap;
using AnimeTracker.Api.Services.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Services.Recap;

// RecapService's highlights (list-recaps "Recap highlights row", taste-stats,
// add-genre-and-studio-stats task 3.3), over a seeded period. The fakes throw for any
// read the recap has no business making, so a highlight that quietly reached for
// something else (MyAnimeList and AniList are not even constructor arguments here)
// would fail loudly.
public class RecapHighlightsTests
{
    private static readonly DateOnly In2026 = new(2026, 5, 10);

    private static UserAnimeEntry Entry(
        int id, string[]? genres = null, string[]? studios = null, int? score = null, DateOnly? airedFrom = null,
        WatchStatus status = WatchStatus.Completed, int? popularity = null, string mediaType = "tv") =>
        new()
        {
            AnimeId = id,
            Anime = new AnimeMetadata
            {
                Id = id,
                Title = $"Anime {id:D3}",
                PictureUrl = $"https://img.test/{id}.jpg",
                Genres = genres?.ToList(),
                Studios = studios?.ToList(),
                AiredFrom = airedFrom ?? In2026,
                PopularityRank = popularity,
                MediaType = mediaType,
            },
            MyScore = score,
            Status = status,
        };

    // Ten anime scored 6 that aired in 2019: outside the 2026 recap, they bring my whole-list
    // mean below the recap's own, as a real list's older, lower scores do.
    private static List<UserAnimeEntry> OlderAnimeScoredSix() =>
        Enumerable.Range(100, 10).Select(id => Entry(id, score: 6, airedFrom: new DateOnly(2019, 1, 1))).ToList();

    private static ActivityLog Log(int animeId, DateTimeOffset at, int from, int to) =>
        new()
        {
            AnimeId = animeId,
            Timestamp = at,
            ChangeType = ActivityChangeType.EpisodeIncremented,
            ChangeDetail = $"Episode {to}",
            PreviousEpisodesWatched = from,
        };

    private static RecapService CreateService(List<UserAnimeEntry> entries, List<ActivityLog>? log = null) =>
        new(new FakeUserAnimeEntryRepository(entries), new FakeActivityLogRepository(log), new FakeTopAnimeSelectionRepository(),
            new FakeBroadcastLocalTimeConverter());

    private static Task<RecapDto> Recap2026(RecapService service) =>
        service.GetRecapAsync(RecapPeriod.Yearly(2026), RecapTimeFilter.Aired, CancellationToken.None);

    [Fact]
    public async Task AYearsHighlightsCarryAllFour()
    {
        // 2026: Action is carried by three anime, Studio A (scored 9, 9, 8, 9) is liked better than Studio B
        // (one 10), an anime scored 10 sits at popularity #4,426, and the log records episodes on 15 September.
        List<UserAnimeEntry> entries =
        [
            Entry(1, ["Action", "Fantasy"], ["Studio A"], score: 9, popularity: 500),
            Entry(2, ["Action"], ["Studio A"], score: 9),
            Entry(3, ["Action", "Comedy"], ["Studio A"], score: 8),
            Entry(4, ["Comedy"], ["Studio B"], score: 10, popularity: 4426),
            Entry(5, ["Drama"], ["Studio A"], score: 9),
            .. OlderAnimeScoredSix(),
        ];
        List<ActivityLog> log = [Log(1, new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.Zero), 0, 12)];

        var highlights = (await Recap2026(CreateService(entries, log))).Highlights;

        Assert.Equal(["Action", "Comedy", "Drama", "Fantasy"], highlights.Genres.Select(g => g.Name));
        Assert.Equal(3, highlights.Genres[0].AnimeCount);
        Assert.Equal(["Studio A", "Studio B"], highlights.Studios.Select(s => s.Name));
        var gem = highlights.HiddenGem;
        Assert.NotNull(gem);
        Assert.Equal((4, 10, 4426), (gem.AnimeId, gem.MyScore, gem.PopularityRank));
        var day = highlights.BiggestDay;
        Assert.NotNull(day);
        Assert.Equal((new DateOnly(2026, 9, 15), 12), (day.Date, day.Episodes));
    }

    [Fact]
    public async Task TheGenreListHoldsEveryGenreMostCarriedFirst()
    {
        List<UserAnimeEntry> entries =
        [
            Entry(1, ["Action", "Fantasy"]), Entry(2, ["Action"]), Entry(3, ["Action", "Comedy"]), Entry(4, ["Comedy"]),
        ];

        var genres = (await Recap2026(CreateService(entries))).Highlights.Genres;

        Assert.Equal([("Action", 3), ("Comedy", 2), ("Fantasy", 1)], genres.Select(g => (g.Name, g.AnimeCount)));
    }

    [Fact]
    public async Task TheStudioListHoldsOnlyStudiosWithAScoredAnimeBestFirst()
    {
        // Studio A: four anime scored 9, 9, 8, 9. Studio B: one scored 10. Studio C: one unscored.
        List<UserAnimeEntry> entries =
        [
            Entry(1, studios: ["Studio A"], score: 9), Entry(2, studios: ["Studio A"], score: 9),
            Entry(3, studios: ["Studio A"], score: 8), Entry(4, studios: ["Studio A"], score: 9),
            Entry(5, studios: ["Studio B"], score: 10),
            Entry(6, studios: ["Studio C"]),
            .. OlderAnimeScoredSix(),
        ];

        var studios = (await Recap2026(CreateService(entries))).Highlights.Studios;

        Assert.Equal(["Studio A", "Studio B"], studios.Select(s => s.Name));
        Assert.Equal([1, 2], studios.Select(s => s.ScoreRank!.Value));
        Assert.All(studios, s => Assert.NotNull(s.WeightedScore));
    }

    [Fact]
    public async Task EveryWeightedScoreUsesTheWholeListsMeanNotTheRecapsOwn()
    {
        // The 2026 recap holds one anime scored 10; the rest of my list (other years) is scored 4,
        // so my whole-list mean is (10 + 4 * 4) / 5 = 5.2. Studio A: (1/6) * 10 + (5/6) * 5.2 = 6.0.
        // A recap-only mean of 10 would give a weighted score of 10.
        List<UserAnimeEntry> entries =
        [
            Entry(1, ["Action"], ["Studio A"], score: 10),
            Entry(2, score: 4, airedFrom: new DateOnly(2020, 1, 1)), Entry(3, score: 4, airedFrom: new DateOnly(2020, 1, 1)),
            Entry(4, score: 4, airedFrom: new DateOnly(2021, 1, 1)), Entry(5, score: 4, airedFrom: new DateOnly(2022, 1, 1)),
        ];

        var highlights = (await Recap2026(CreateService(entries))).Highlights;

        Assert.Equal(6.0, highlights.Studios.Single().WeightedScore);
        // v = 1, R = 10, m = 10 for the genre: (1/11) * 10 + (10/11) * 5.2 = 5.64.
        Assert.Equal(5.64, highlights.Genres.Single().WeightedScore);
    }

    [Fact]
    public async Task AnAnimeOfEveryMediaTypeIsCountedWhateverTheTop10IsNarrowedTo()
    {
        // The type narrowing is the page's, on its top 10 only: the highlights count the whole included set.
        List<UserAnimeEntry> entries =
        [
            Entry(1, ["Action"], mediaType: "tv"), Entry(2, ["Action"], mediaType: "movie"),
            Entry(3, ["Action"], mediaType: "ova"), Entry(4, ["Action"], mediaType: "music"),
        ];

        var dto = await Recap2026(CreateService(entries));

        Assert.Equal(4, dto.Highlights.Genres.Single().AnimeCount);
        Assert.Equal(4, dto.Items.Count);
    }

    [Fact]
    public async Task APeriodBeforeTheLogBeganHasNoBiggestDayAndTheOtherHighlightsRemain()
    {
        // A 2015 recap; the log holds a single row, in 2026.
        var aired = new DateOnly(2015, 4, 5);
        List<UserAnimeEntry> entries =
        [
            Entry(1, ["Action"], ["Studio A"], score: 9, airedFrom: aired, popularity: 2500),
            Entry(2, ["Action"], ["Studio A"], score: 8, airedFrom: aired),
        ];
        List<ActivityLog> log = [Log(1, new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.Zero), 0, 12)];

        var dto = await CreateService(entries, log).GetRecapAsync(RecapPeriod.Yearly(2015), RecapTimeFilter.Aired, CancellationToken.None);

        Assert.Null(dto.Highlights.BiggestDay);
        Assert.Equal("Action", dto.Highlights.Genres[0].Name);
        Assert.Equal("Studio A", dto.Highlights.Studios[0].Name);
        Assert.Equal(1, dto.Highlights.HiddenGem!.AnimeId);
    }

    [Fact]
    public async Task NothingScoredMeansNoStudioAndNoGem()
    {
        List<UserAnimeEntry> entries = [Entry(1, ["Action"], ["Studio A"], popularity: 9000), Entry(2, ["Comedy"], ["Studio B"])];

        var highlights = (await Recap2026(CreateService(entries))).Highlights;

        Assert.Equal(2, highlights.Genres.Count);
        Assert.Empty(highlights.Studios);
        Assert.Null(highlights.HiddenGem);
    }

    [Fact]
    public async Task OnlyTheIncludedSetsAnimeAreCounted()
    {
        // Anime 2 aired in 2020: outside the 2026 recap, so it adds to none of the figures.
        List<UserAnimeEntry> entries =
        [
            Entry(1, ["Action"], ["Studio A"], score: 9, popularity: 3000),
            Entry(2, ["Action", "Horror"], ["Studio Z"], score: 10, popularity: 9000, airedFrom: new DateOnly(2020, 1, 1)),
        ];

        var highlights = (await Recap2026(CreateService(entries))).Highlights;

        Assert.Equal(["Action"], highlights.Genres.Select(g => g.Name));
        Assert.Equal(1, highlights.Genres[0].AnimeCount);
        Assert.Equal(["Studio A"], highlights.Studios.Select(s => s.Name));
        Assert.Equal(1, highlights.HiddenGem!.AnimeId);
    }

    [Fact]
    public async Task AnEmptyPeriodHasEmptyHighlightsRatherThanNone()
    {
        var dto = await Recap2026(CreateService([]));

        Assert.NotNull(dto.Highlights);
        Assert.Empty(dto.Highlights.Genres);
        Assert.Empty(dto.Highlights.Studios);
        Assert.Null(dto.Highlights.HiddenGem);
        Assert.Null(dto.Highlights.BiggestDay);
        Assert.Null(dto.Highlights.WatchingDays);
    }

    [Fact]
    public async Task TheRecapReadCarriesTheWatchingDaysOfTheIncludedSetUnderTheAiredFilter()
    {
        // Anime 9 aired in 2020: outside the 2026 recap, so its day is not a watching day.
        List<UserAnimeEntry> entries = [Entry(1, ["Action"]), Entry(9, ["Action"], airedFrom: new DateOnly(2020, 1, 1))];
        List<ActivityLog> log =
        [
            Log(1, new DateTimeOffset(2026, 3, 2, 12, 0, 0, TimeSpan.Zero), 0, 6),
            Log(1, new DateTimeOffset(2026, 3, 3, 12, 0, 0, TimeSpan.Zero), 6, 8),
            Log(9, new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero), 0, 40),
        ];

        var highlights = (await Recap2026(CreateService(entries, log))).Highlights;

        var watching = highlights.WatchingDays;
        Assert.NotNull(watching);
        Assert.Equal([new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 3)], watching.Days.Select(d => d.Date));
        Assert.Equal(new DateOnly(2026, 3, 2), watching.TopDays[0]);
        Assert.Equal(highlights.BiggestDay!.Date, watching.TopDays[0]);
        Assert.Equal(2, watching.DaysWatched);
    }

    [Fact]
    public async Task TheRecapReadCarriesTheWatchingDaysOfTheIncludedSetUnderTheWatchedFilter()
    {
        // Under "watched" an entry counts when it completed in the period or has logged progress in it,
        // whatever it aired. Anime 3 was completed in 2025 and has no progress in 2026, so it is outside.
        var completed = Entry(1, ["Action"], airedFrom: new DateOnly(2020, 1, 1));
        completed.CompletedAt = new DateOnly(2026, 6, 1);
        var rewatched = Entry(2, ["Action"], airedFrom: new DateOnly(2020, 1, 1));
        rewatched.CompletedAt = new DateOnly(2025, 6, 1);
        var outside = Entry(3, ["Action"], airedFrom: new DateOnly(2020, 1, 1));
        outside.CompletedAt = new DateOnly(2025, 6, 1);
        List<ActivityLog> log =
        [
            Log(1, new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero), 0, 12),
            Log(2, new DateTimeOffset(2026, 6, 2, 12, 0, 0, TimeSpan.Zero), 0, 12),
            Log(3, new DateTimeOffset(2025, 6, 3, 12, 0, 0, TimeSpan.Zero), 0, 12), // before the period: never loaded
        ];

        var dto = await CreateService([completed, rewatched, outside], log).GetRecapAsync(RecapPeriod.Yearly(2026), RecapTimeFilter.Watched, CancellationToken.None);

        var watching = dto.Highlights.WatchingDays!;
        Assert.Equal([new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 2)], watching.Days.Select(d => d.Date));
        Assert.Equal([1, 2], watching.Days.SelectMany(d => d.Anime).Select(a => a.AnimeId));
        Assert.Equal(dto.Stats.EpisodesWatched, watching.Days.Sum(d => d.Episodes));
    }

    [Fact]
    public async Task APeriodBeforeTheLogStartsHasNoWatchingDays()
    {
        List<UserAnimeEntry> entries = [Entry(1, ["Action"], airedFrom: new DateOnly(2015, 4, 1))];
        List<ActivityLog> log = [Log(1, new DateTimeOffset(2026, 3, 2, 12, 0, 0, TimeSpan.Zero), 0, 6)];

        var dto = await CreateService(entries, log).GetRecapAsync(RecapPeriod.Yearly(2015), RecapTimeFilter.Aired, CancellationToken.None);

        Assert.Null(dto.Highlights.WatchingDays);
        Assert.Null(dto.Highlights.BiggestDay);
    }

    [Fact]
    public async Task TwoReadsWithNoChangeBetweenGiveIdenticalWatchingDaysEvenForRowsSharingATimestamp()
    {
        // Two rows of one anime share a timestamp. The real repository orders them by id, so 0 to 3 (id 1)
        // always comes before 3 to 6 (id 2) and they merge into one run, however they were inserted.
        using var db = new AnimeTrackerDbContext(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var at = new DateTimeOffset(2026, 3, 2, 12, 0, 0, TimeSpan.Zero);
        var second = Log(1, at, 3, 6);
        second.Id = 2;
        var first = Log(1, at, 0, 3);
        first.Id = 1;
        db.ActivityLogs.AddRange(second, first);
        await db.SaveChangesAsync();
        var service = new RecapService(
            new FakeUserAnimeEntryRepository([Entry(1, ["Action"])]), new ActivityLogRepository(db),
            new FakeTopAnimeSelectionRepository(), new FakeBroadcastLocalTimeConverter());

        var firstRead = (await Recap2026(service)).Highlights.WatchingDays!;
        var secondRead = (await Recap2026(service)).Highlights.WatchingDays!;

        var anime = Assert.Single(Assert.Single(firstRead.Days).Anime);
        Assert.Equal(6, anime.Episodes);
        Assert.Equal([(1, 6)], anime.Runs.Select(r => (r.From, r.To)));
        Assert.Equal(
            System.Text.Json.JsonSerializer.Serialize(firstRead),
            System.Text.Json.JsonSerializer.Serialize(secondRead));
    }

    [Fact]
    public async Task TheBiggestDayCountsOnlyAnimeOfTheIncludedSet()
    {
        // Anime 9 is outside the 2026 recap (aired 2020): its 40 episodes on 1 June do not make that the biggest day.
        List<UserAnimeEntry> entries = [Entry(1, ["Action"]), Entry(9, ["Action"], airedFrom: new DateOnly(2020, 1, 1))];
        List<ActivityLog> log =
        [
            Log(1, new DateTimeOffset(2026, 3, 2, 12, 0, 0, TimeSpan.Zero), 0, 6),
            Log(9, new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero), 0, 40),
        ];

        var day = (await Recap2026(CreateService(entries, log))).Highlights.BiggestDay;

        Assert.Equal((new DateOnly(2026, 3, 2), 6), (day!.Date, day.Episodes));
    }

    [Fact]
    public async Task TheBiggestDayAgreesWithThePeriodsEpisodesWatchedUnderTheWatchedFilter()
    {
        // A correction subtracts nothing from either: 12 episodes up, 3 down on another day.
        var entry = Entry(1, ["Action"]);
        entry.CompletedAt = new DateOnly(2026, 6, 1);
        List<ActivityLog> log =
        [
            Log(1, new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero), 0, 12),
            Log(1, new DateTimeOffset(2026, 6, 2, 12, 0, 0, TimeSpan.Zero), 12, 9),
        ];

        var dto = await CreateService([entry], log).GetRecapAsync(RecapPeriod.Yearly(2026), RecapTimeFilter.Watched, CancellationToken.None);

        Assert.Equal(12, dto.Stats.EpisodesWatched);
        Assert.Equal(12, dto.Highlights.BiggestDay!.Episodes);
    }

    // The collaborators a recap may use. Anything beyond them throws.

    private sealed class FakeUserAnimeEntryRepository(List<UserAnimeEntry> entries) : IUserAnimeEntryRepository
    {
        public Task<UserAnimeEntry?> GetByAnimeIdAsync(int animeId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<UserAnimeEntry>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(entries);
        public Task<(int PendingCount, int HeldCount, DateTimeOffset? LastSyncedAt)> GetSyncStatusAsync(CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    private sealed class FakeTopAnimeSelectionRepository : ITopAnimeSelectionRepository
    {
        public Task<List<int>> GetOrderedAnimeIdsAsync(CancellationToken ct = default) => Task.FromResult(new List<int>());
        public Task ReplaceOrderAsync(IReadOnlyList<int> editedIds, CancellationToken ct = default) => throw new NotImplementedException();
        public Task ReplaceAllAsync(IReadOnlyList<int> animeIds, DateTimeOffset modifiedAt, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<DateTimeOffset?> GetModifiedAtAsync(CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class FakeActivityLogRepository(List<ActivityLog>? rows = null) : IActivityLogRepository
    {
        private readonly List<ActivityLog> rows = rows ?? [];

        public Task<List<ActivityLog>> GetRecentAsync(int count, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<ActivityLog>> GetSinceAsync(DateTimeOffset cutoffUtc, int maxRows, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<ActivityLog>> GetAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<ActivityLog>> GetEpisodeProgressInRangeAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct = default) =>
            Task.FromResult(rows
                .Where(r => r.ChangeType == ActivityChangeType.EpisodeIncremented && r.Timestamp >= fromUtc && r.Timestamp < toUtc)
                .OrderBy(r => r.Timestamp)
                .ToList());
        public Task<List<ActivityLog>> GetAllOldestFirstAsync(CancellationToken ct = default) => throw new NotImplementedException();
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
