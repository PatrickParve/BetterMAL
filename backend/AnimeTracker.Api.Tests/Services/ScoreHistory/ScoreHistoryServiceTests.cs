using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Scheduling;
using AnimeTracker.Api.Services.ScoreHistory;
using AnimeTracker.Api.Services.Series;
using Microsoft.EntityFrameworkCore;
using SeriesModel = AnimeTracker.Api.Models.Series;

namespace AnimeTracker.Api.Tests.Services.ScoreHistory;

// add-score-history-graph tasks 4.2/4.5: ScoreHistoryService's reads over
// stored records on InMemory — an anime's points, a series' main-line line,
// and the has-changed answers the page reads carry.
public class ScoreHistoryServiceTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly D0 = new(2026, 10, 30);

    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ScoreHistoryService Service(AnimeTrackerDbContext db) => ScoreHistoryServiceTestKit.Create(db);

    private static void AddAnime(
        AnimeTrackerDbContext db, int id, string status = "finished_airing", DateOnly? airedFrom = null) =>
        db.AnimeMetadata.Add(new AnimeMetadata
        {
            Id = id, Title = $"Anime {id}", EnglishTitle = $"English {id}", PictureUrl = $"https://img/{id}.jpg",
            AiringStatus = status, AiredFrom = airedFrom,
        });

    private static void Record(AnimeTrackerDbContext db, int animeId, int dayOffset, double score, int hour = 12) =>
        db.AnimeScoreRecords.Add(new AnimeScoreRecord
        {
            AnimeId = animeId, Score = score, DetectedAt = T0.AddDays(dayOffset).AddHours(hour - 12),
        });

    private static void AddSeries(AnimeTrackerDbContext db, int seriesId, int[] mainLine, int[]? extras = null)
    {
        db.Series.Add(new SeriesModel { Id = seriesId, BuiltAt = T0 });
        var order = 0;
        foreach (var animeId in mainLine)
            db.SeriesMembers.Add(new SeriesMember { SeriesId = seriesId, AnimeId = animeId, IsMainLine = true, IsPrimary = true, Order = order++ });
        foreach (var animeId in extras ?? [])
            db.SeriesMembers.Add(new SeriesMember { SeriesId = seriesId, AnimeId = animeId, IsMainLine = false, IsPrimary = false, Order = order++, RelationGroup = "SideStory" });
    }

    // ---- an anime ----

    [Fact]
    public async Task AnAnimesPointsComeOldestFirstOnTheLocalDate()
    {
        using var db = CreateDb();
        AddAnime(db, 1);
        Record(db, 1, 3, 8.33);
        Record(db, 1, 0, 8.31);
        await db.SaveChangesAsync();

        var history = await Service(db).GetAnimeHistoryAsync(1);

        Assert.Equal([new ScoreHistoryPointDto(D0, 8.31), new ScoreHistoryPointDto(D0.AddDays(3), 8.33)], history.Points);
        Assert.Empty(history.Markers);
    }

    [Fact]
    public async Task APointsDateIsTheAppsLocalDayNotTheInstantsUtcDate()
    {
        using var db = CreateDb();
        AddAnime(db, 1);
        Record(db, 1, 0, 8.31, hour: 23); // 23:00 UTC on the 30th is already the 31st in Helsinki
        await db.SaveChangesAsync();

        var history = await Service(db).GetAnimeHistoryAsync(1);

        Assert.Equal(D0.AddDays(1), Assert.Single(history.Points).Date);
    }

    [Fact]
    public async Task AnUnknownOrUnrecordedAnimeHasNoPoints()
    {
        using var db = CreateDb();
        AddAnime(db, 1);
        await db.SaveChangesAsync();

        Assert.Empty((await Service(db).GetAnimeHistoryAsync(1)).Points);
        Assert.Empty((await Service(db).GetAnimeHistoryAsync(999)).Points);
        Assert.False(await Service(db).AnimeHasChangedAsync(999));
    }

    [Fact]
    public async Task OtherAnimesRecordsAreNeverRead()
    {
        using var db = CreateDb();
        AddAnime(db, 1);
        AddAnime(db, 2);
        Record(db, 1, 0, 8.31);
        Record(db, 2, 0, 5.00);
        Record(db, 2, 4, 5.50);
        await db.SaveChangesAsync();

        var history = await Service(db).GetAnimeHistoryAsync(1);

        Assert.Equal(8.31, Assert.Single(history.Points).Score);
        Assert.False(await Service(db).AnimeHasChangedAsync(1));
        Assert.True(await Service(db).AnimeHasChangedAsync(2));
    }

    [Fact]
    public async Task AnAnimeHasChangedOnlyWhenItsPointsHoldTwoDifferentScores()
    {
        using var db = CreateDb();
        AddAnime(db, 1);
        AddAnime(db, 2);
        AddAnime(db, 3);
        Record(db, 1, 0, 8.01); // one record
        Record(db, 2, 0, 8.01); // a move that returns within a day
        Record(db, 2, 1, 8.02, hour: 9);
        Record(db, 2, 1, 8.01, hour: 18);
        Record(db, 3, 0, 8.01); // a real change
        Record(db, 3, 5, 8.02);
        await db.SaveChangesAsync();

        Assert.False(await Service(db).AnimeHasChangedAsync(1));
        Assert.False(await Service(db).AnimeHasChangedAsync(2));
        Assert.True(await Service(db).AnimeHasChangedAsync(3));
    }

    // ---- a series ----

    [Fact]
    public async Task ASeriesLineIsTheMeanOfItsMainLinesPointsAndAnExtrasAreIgnored()
    {
        using var db = CreateDb();
        foreach (var id in new[] { 1, 2, 3 })
            AddAnime(db, id, airedFrom: new DateOnly(2020, 1, 1));
        AddSeries(db, 1, mainLine: [1, 2], extras: [3]);
        Record(db, 1, 0, 8.10);
        Record(db, 2, 0, 7.90);
        Record(db, 3, 0, 1.00); // an extra's score, and then a change to it
        Record(db, 3, 6, 2.00);
        await db.SaveChangesAsync();

        var history = await Service(db).GetSeriesHistoryAsync(1);

        Assert.Equal([new ScoreHistoryPointDto(D0, 8.00)], history.Points);
        Assert.False(await Service(db).SeriesHasChangedAsync(1)); // a change on an extra alone offers nothing
    }

    [Fact]
    public async Task EveryAlternativeOfAVersionSlotCountsWhichEverOneIsPicked()
    {
        using var db = CreateDb();
        foreach (var id in new[] { 1, 2, 3 })
            AddAnime(db, id, airedFrom: new DateOnly(2020, 1, 1));
        AddSeries(db, 1, mainLine: [1, 2, 3]);
        // Anime 2 and 3 are two alternatives of one slot; the picker is the page's, not the history's.
        foreach (var member in db.SeriesMembers.Local.Where(m => m.AnimeId is 2 or 3))
        {
            member.VersionSlotKey = 2;
            member.BranchHeadAnimeId = member.AnimeId;
        }
        Record(db, 1, 0, 8.0);
        Record(db, 2, 0, 7.0);
        Record(db, 3, 0, 6.0);
        await db.SaveChangesAsync();

        var history = await Service(db).GetSeriesHistoryAsync(1);

        Assert.Equal(7.0, Assert.Single(history.Points).Score); // (8 + 7 + 6) / 3, both alternatives included
    }

    [Fact]
    public async Task AMemberMovedOffTheMainLineStopsCountingOnTheNextRead()
    {
        using var db = CreateDb();
        foreach (var id in new[] { 1, 2 })
            AddAnime(db, id, airedFrom: new DateOnly(2020, 1, 1));
        AddSeries(db, 1, mainLine: [1, 2]);
        Record(db, 1, 0, 8.0);
        Record(db, 2, 0, 6.0);
        Record(db, 2, 4, 6.5);
        await db.SaveChangesAsync();
        var service = Service(db);
        Assert.Equal(7.0, (await service.GetSeriesHistoryAsync(1)).Points[0].Score);
        Assert.True(await service.SeriesHasChangedAsync(1));

        // A rebuild reclassifies anime 2 as an extra.
        (await db.SeriesMembers.SingleAsync(m => m.AnimeId == 2)).IsMainLine = false;
        await db.SaveChangesAsync();

        var after = await service.GetSeriesHistoryAsync(1);
        Assert.Equal([new ScoreHistoryPointDto(D0, 8.0)], after.Points); // at every date, not just from now on
        Assert.False(await service.SeriesHasChangedAsync(1));
    }

    [Fact]
    public async Task ASeriesWithNoRecordsOnItsMainLineHasNoLine()
    {
        using var db = CreateDb();
        AddAnime(db, 1);
        AddSeries(db, 1, mainLine: [1]);
        await db.SaveChangesAsync();

        var history = await Service(db).GetSeriesHistoryAsync(1);

        Assert.Empty(history.Points);
        Assert.Empty(history.Markers);
    }

    [Fact]
    public async Task AnUnknownSeriesIsNotFoundButItsHasChangedAnswerIsFalse()
    {
        using var db = CreateDb();

        await Assert.ThrowsAsync<SeriesIdNotFoundException>(() => Service(db).GetSeriesHistoryAsync(999));
        Assert.False(await Service(db).SeriesHasChangedAsync(999));
    }

    // ---- a series' markers ----

    [Fact]
    public async Task ASeasonThatAiredDuringTheHistoryIsMarkedWithItsTitlesAndPicture()
    {
        using var db = CreateDb();
        AddAnime(db, 1, airedFrom: new DateOnly(2020, 1, 1));
        AddAnime(db, 2, status: "currently_airing", airedFrom: D0.AddDays(20));
        AddSeries(db, 1, mainLine: [1, 2]);
        Record(db, 1, 0, 8.0);
        Record(db, 1, 40, 8.2);
        Record(db, 2, 22, 7.0); // its first score, two days after the first episode
        await db.SaveChangesAsync();

        var history = await Service(db).GetSeriesHistoryAsync(1);

        var marker = Assert.Single(history.Markers);
        Assert.Equal((2, "Anime 2", "English 2", "https://img/2.jpg", D0.AddDays(20)),
            (marker.AnimeId, marker.Title, marker.EnglishTitle, marker.PictureUrl, marker.Date));
        Assert.Contains(history.Points, p => p.Date == marker.Date);
    }

    [Fact]
    public async Task AStoredAiredEpisodeMarksASeasonMyAnimeListStillCallsUnaired()
    {
        using var db = CreateDb();
        AddAnime(db, 1, airedFrom: new DateOnly(2020, 1, 1));
        AddAnime(db, 2, status: "not_yet_aired", airedFrom: D0.AddDays(20));
        AddSeries(db, 1, mainLine: [1, 2]);
        Record(db, 1, 0, 8.0);
        Record(db, 1, 40, 8.2);
        db.EpisodeAirings.Add(new EpisodeAiring { AnimeId = 2, Episode = 1, AirsAtUtc = DateTimeOffset.UtcNow.AddDays(-1), FetchedAt = T0 }); // aired as of the read's own now
        await db.SaveChangesAsync();

        var history = await Service(db).GetSeriesHistoryAsync(1);

        Assert.Equal(2, Assert.Single(history.Markers).AnimeId);
    }

    [Fact]
    public async Task ASeasonThatHasNotAiredIsNotMarkedWhateverDateItIsAnnouncedFor()
    {
        using var db = CreateDb();
        AddAnime(db, 1, airedFrom: new DateOnly(2020, 1, 1));
        AddAnime(db, 2, status: "not_yet_aired", airedFrom: D0.AddDays(20));
        AddSeries(db, 1, mainLine: [1, 2]);
        Record(db, 1, 0, 8.0);
        Record(db, 1, 40, 8.2);
        await db.SaveChangesAsync();

        Assert.Empty((await Service(db).GetSeriesHistoryAsync(1)).Markers);
    }

    [Fact]
    public async Task TheHasChangedReadMakesNoAiredEpisodeRead()
    {
        using var db = CreateDb();
        AddAnime(db, 1, airedFrom: new DateOnly(2020, 1, 1));
        AddAnime(db, 2, status: "currently_airing", airedFrom: D0.AddDays(20));
        AddSeries(db, 1, mainLine: [1, 2]);
        Record(db, 1, 0, 8.0);
        Record(db, 1, 40, 8.2);
        await db.SaveChangesAsync();
        var service = new ScoreHistoryService(db, new BroadcastLocalTimeConverter(), new PoisonedScheduleService());

        Assert.True(await service.SeriesHasChangedAsync(1)); // a marker only repeats a score, so it can't decide this
        await Assert.ThrowsAsync<NotImplementedException>(() => service.GetSeriesHistoryAsync(1));
    }

    // ---- no outside calls ----

    [Fact]
    public void TheServiceDependsOnNoMyAnimeListClient()
    {
        var parameters = typeof(ScoreHistoryService).GetConstructors().Single().GetParameters();

        Assert.DoesNotContain(parameters, p => typeof(IMalClient).IsAssignableFrom(p.ParameterType) || p.ParameterType == typeof(IMalClient));
    }

    private sealed class PoisonedScheduleService : IEpisodeScheduleService
    {
        public Task<ResolvedEpisode?> ResolveOnLocalDateAsync(AnimeMetadata anime, DateOnly localDate, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<DateTimeOffset?> NextAiringInstantAsync(AnimeMetadata anime, DateTimeOffset afterUtc, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<int?> EpisodesAiredAsOfAsync(AnimeMetadata anime, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<Dictionary<int, int>> EpisodesAiredAsOfAsync(IReadOnlyCollection<AnimeMetadata> anime, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<Dictionary<int, int>> EpisodesAiredAsOfAsync(IReadOnlyCollection<int> animeIds, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }
}
