using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.ScoreHistory;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Services.ScoreHistory;

// add-score-history-graph design D2/D3: AnimeScoreRecorder compares the score
// an anime now holds with its latest record, and only starts a history once an
// episode is known to have aired. It adds to the caller's context and never
// saves, so every test saves the way a writer would.
public class AnimeScoreRecorderTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);

    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static AnimeMetadata Anime(double? score, string? status = "currently_airing", int id = 1) =>
        new() { Id = id, Title = $"Anime {id}", MalScore = score, AiringStatus = status };

    private static async Task RecordAsync(
        AnimeTrackerDbContext db, AnimeMetadata anime, DateTimeOffset now,
        int? airedSoFar = null, CountingScheduleService? schedule = null)
    {
        var recorder = new AnimeScoreRecorder(db, schedule ?? new CountingScheduleService(airedSoFar));
        await recorder.RecordAsync(anime, now);
        await db.SaveChangesAsync();
    }

    private static Task<List<AnimeScoreRecord>> RecordsOf(AnimeTrackerDbContext db, int animeId = 1) =>
        db.AnimeScoreRecords.AsNoTracking().Where(r => r.AnimeId == animeId).OrderBy(r => r.DetectedAt).ToListAsync();

    [Fact]
    public async Task AnAiredAnimesFirstScoreIsRecordedWithTheMomentOfTheWrite()
    {
        using var db = CreateDb();
        var anime = Anime(8.31);
        db.AnimeMetadata.Add(anime);

        await RecordAsync(db, anime, T0);

        var record = Assert.Single(await RecordsOf(db));
        Assert.Equal(8.31, record.Score);
        Assert.Equal(T0, record.DetectedAt);
    }

    [Fact]
    public async Task AChangedScoreIsRecordedBesideTheFirst()
    {
        using var db = CreateDb();
        var anime = Anime(8.31);
        db.AnimeMetadata.Add(anime);
        await RecordAsync(db, anime, T0);

        anime.MalScore = 8.33;
        await RecordAsync(db, anime, T0.AddDays(1));

        var records = await RecordsOf(db);
        Assert.Equal([8.31, 8.33], records.Select(r => r.Score));
        Assert.Equal([T0, T0.AddDays(1)], records.Select(r => r.DetectedAt));
    }

    [Fact]
    public async Task AnUnchangedScoreRecordsNothingAndAsksNoAiredCount()
    {
        using var db = CreateDb();
        var anime = Anime(8.31);
        db.AnimeMetadata.Add(anime);
        await RecordAsync(db, anime, T0);

        var schedule = new CountingScheduleService(airedSoFar: null);
        await RecordAsync(db, anime, T0.AddDays(1), schedule: schedule);

        Assert.Single(await RecordsOf(db));
        Assert.Equal(0, schedule.Reads); // most writes end on the latest record, before any aired-count read
    }

    [Fact]
    public async Task AnAnimeWithNoScoreRecordsNothing()
    {
        using var db = CreateDb();
        var anime = Anime(score: null);
        db.AnimeMetadata.Add(anime);

        await RecordAsync(db, anime, T0);

        Assert.Empty(await RecordsOf(db));
    }

    [Fact]
    public async Task AScoreReturningAfterNoneIsComparedWithTheLatestRecordLikeAnyOther()
    {
        using var db = CreateDb();
        var anime = Anime(8.31);
        db.AnimeMetadata.Add(anime);
        await RecordAsync(db, anime, T0);

        anime.MalScore = null; // MAL shows no score for a while...
        await RecordAsync(db, anime, T0.AddDays(1));
        anime.MalScore = 8.31; // ...and then the same one again
        await RecordAsync(db, anime, T0.AddDays(2));

        var record = Assert.Single(await RecordsOf(db)); // neither write stored a thing
        Assert.Equal(T0, record.DetectedAt);
    }

    [Fact]
    public async Task AScoreReturningAfterNoneThatDiffersIsRecorded()
    {
        using var db = CreateDb();
        var anime = Anime(8.31);
        db.AnimeMetadata.Add(anime);
        await RecordAsync(db, anime, T0);

        anime.MalScore = null;
        await RecordAsync(db, anime, T0.AddDays(1));
        anime.MalScore = 8.40;
        await RecordAsync(db, anime, T0.AddDays(2));

        Assert.Equal([8.31, 8.40], (await RecordsOf(db)).Select(r => r.Score));
    }

    [Fact]
    public async Task AScoreBeforeThePremiereRecordsNothing()
    {
        using var db = CreateDb();
        var anime = Anime(7.20, status: "not_yet_aired");
        db.AnimeMetadata.Add(anime);

        await RecordAsync(db, anime, T0, airedSoFar: null);

        Assert.Empty(await RecordsOf(db));
    }

    [Fact]
    public async Task TheFirstWriteAfterAnEpisodeAiredRecordsThatWritesScoreAndNotTheEarlierOne()
    {
        using var db = CreateDb();
        var anime = Anime(7.20, status: "not_yet_aired");
        db.AnimeMetadata.Add(anime);
        await RecordAsync(db, anime, T0);

        anime.AiringStatus = "currently_airing";
        anime.MalScore = 7.15;
        await RecordAsync(db, anime, T0.AddDays(1));

        var record = Assert.Single(await RecordsOf(db));
        Assert.Equal(7.15, record.Score); // 7.20 appears nowhere
    }

    [Fact]
    public async Task AStoredAiredEpisodeBeatsALateNotYetAiredStatus()
    {
        using var db = CreateDb();
        var anime = Anime(7.20, status: "not_yet_aired");
        db.AnimeMetadata.Add(anime);

        await RecordAsync(db, anime, T0, airedSoFar: 1);

        Assert.Equal(7.20, Assert.Single(await RecordsOf(db)).Score);
    }

    [Fact]
    public async Task AnAnimeWithNoAiringStatusRecordsNothingAndMakesNoAiredCountRead()
    {
        using var db = CreateDb();
        var anime = Anime(7.20, status: null);
        db.AnimeMetadata.Add(anime);

        var schedule = new CountingScheduleService(airedSoFar: 5);
        await RecordAsync(db, anime, T0, schedule: schedule);

        Assert.Empty(await RecordsOf(db));
        Assert.Equal(0, schedule.Reads);
    }

    [Fact]
    public async Task TheSameAnimeTwiceInOneUnitOfWorkAddsOneRecord()
    {
        using var db = CreateDb();
        var anime = Anime(8.31);
        db.AnimeMetadata.Add(anime);

        var recorder = new AnimeScoreRecorder(db, new CountingScheduleService(airedSoFar: null));
        await recorder.RecordAsync(anime, T0);
        await recorder.RecordAsync(anime, T0); // a listing naming one anime twice
        await db.SaveChangesAsync();

        // InMemory doesn't enforce the unique index, so the count is the assertion.
        Assert.Single(await RecordsOf(db));
    }

    [Fact]
    public async Task TheSameAnimeTwiceAtOneInstantWithTwoScoresKeepsTheLaterAndStillAddsOneRecord()
    {
        using var db = CreateDb();
        var anime = Anime(8.31);
        db.AnimeMetadata.Add(anime);

        var recorder = new AnimeScoreRecorder(db, new CountingScheduleService(airedSoFar: null));
        await recorder.RecordAsync(anime, T0);
        anime.MalScore = 8.32;
        await recorder.RecordAsync(anime, T0); // same instant: a second row would break the unique index
        await db.SaveChangesAsync();

        Assert.Equal(8.32, Assert.Single(await RecordsOf(db)).Score);
    }

    [Fact]
    public async Task ChangesAtTwoInstantsInOneUnitOfWorkAreTwoRecords()
    {
        using var db = CreateDb();
        var anime = Anime(8.31);
        db.AnimeMetadata.Add(anime);

        var recorder = new AnimeScoreRecorder(db, new CountingScheduleService(airedSoFar: null));
        await recorder.RecordAsync(anime, T0);
        anime.MalScore = 8.32;
        await recorder.RecordAsync(anime, T0.AddMinutes(1));
        await db.SaveChangesAsync();

        Assert.Equal([8.31, 8.32], (await RecordsOf(db)).Select(r => r.Score));
    }

    [Fact]
    public async Task RecordingNeverSavesOnItsOwn()
    {
        using var db = CreateDb();
        var anime = Anime(8.31);
        db.AnimeMetadata.Add(anime);
        await db.SaveChangesAsync();

        await new AnimeScoreRecorder(db, new CountingScheduleService(airedSoFar: null)).RecordAsync(anime, T0);

        Assert.Empty(await RecordsOf(db)); // queued on the context, not stored: the caller owns the save
        Assert.Single(db.AnimeScoreRecords.Local);
    }

    [Fact]
    public async Task OneAnimesRecordsNeverHideAnotherAnimesChange()
    {
        using var db = CreateDb();
        var first = Anime(8.31, id: 1);
        var second = Anime(8.31, id: 2);
        db.AnimeMetadata.AddRange(first, second);

        var recorder = new AnimeScoreRecorder(db, new CountingScheduleService(airedSoFar: null));
        await recorder.RecordAsync(first, T0);
        await recorder.RecordAsync(second, T0);
        await db.SaveChangesAsync();

        Assert.Single(await RecordsOf(db, 1));
        Assert.Single(await RecordsOf(db, 2));
    }

    /// <summary>Counts the aired-count reads the recorder makes, so a test can
    /// prove an early exit made none. Only the one member the recorder uses is
    /// implemented.</summary>
    internal sealed class CountingScheduleService(int? airedSoFar) : IEpisodeScheduleService
    {
        public int Reads { get; private set; }

        public Task<int?> EpisodesAiredAsOfAsync(AnimeMetadata anime, DateTimeOffset nowUtc, CancellationToken ct = default)
        {
            Reads++;
            return Task.FromResult(airedSoFar);
        }

        public Task<ResolvedEpisode?> ResolveOnLocalDateAsync(AnimeMetadata anime, DateOnly localDate, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<DateTimeOffset?> NextAiringInstantAsync(AnimeMetadata anime, DateTimeOffset afterUtc, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<Dictionary<int, int>> EpisodesAiredAsOfAsync(IReadOnlyCollection<AnimeMetadata> anime, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<Dictionary<int, int>> EpisodesAiredAsOfAsync(IReadOnlyCollection<int> animeIds, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }
}
