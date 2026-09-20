using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Relations;
using AnimeTracker.Api.Services.Updates;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Services.Updates;

// IAnimeUpdateRecorder (anime-updates spec "Nothing is recorded for an anime
// outside my list and its direct relations"; scope-updates-to-my-list tasks
// 6.3): the relevance gate (design.md D1) runs ahead of the existing
// dedupe/schedule-change rules, for every kind a caller can hand it.
public class AnimeUpdateRecorderTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static AnimeUpdateRecorder CreateRecorder(AnimeTrackerDbContext db) =>
        new(db, new AnimeUpdateRelevance(db, new RelationResolver(db)));

    // One representative of each family: Announced and EpisodeCountReleased
    // are both becoming-known kinds (deduped for life per anime),
    // BroadcastSlotChanged is a schedule-change kind (never deduped) — so no
    // family slips past the gate untested.
    [Theory]
    [InlineData(AnimeUpdateKinds.Announced)]
    [InlineData(AnimeUpdateKinds.EpisodeCountReleased)]
    [InlineData(AnimeUpdateKinds.BroadcastSlotChanged)]
    public async Task AnIrrelevantAnimeQueuesNothingWhateverTheKind(AnimeUpdateKinds kinds)
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "Stranger" };
        db.AnimeMetadata.Add(anime); // no UserAnimeEntries, no relation edge
        await db.SaveChangesAsync();

        await CreateRecorder(db).RecordAsync(anime, kinds, new ScheduleMoveDetails(), DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();

        Assert.Empty(await db.AnimeUpdates.ToListAsync());
    }

    [Theory]
    [InlineData(AnimeUpdateKinds.Announced)]
    [InlineData(AnimeUpdateKinds.EpisodeCountReleased)]
    [InlineData(AnimeUpdateKinds.BroadcastSlotChanged)]
    public async Task ARelevantAnimeQueuesAsBefore(AnimeUpdateKinds kinds)
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "My show" };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Status = WatchStatus.Watching });
        await db.SaveChangesAsync();

        await CreateRecorder(db).RecordAsync(anime, kinds, new ScheduleMoveDetails(), DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();

        var update = Assert.Single(await db.AnimeUpdates.AsNoTracking().ToListAsync());
        Assert.Equal(kinds, update.Kinds);
    }

    // The CLR default (store-seen-updates-on-server design.md D1) — the
    // recorder needs no change to leave a new row unseen.
    [Fact]
    public async Task ARecordedRowHasSeenFalse()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "My show" };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Status = WatchStatus.Watching });
        await db.SaveChangesAsync();

        await CreateRecorder(db).RecordAsync(anime, AnimeUpdateKinds.Announced, new ScheduleMoveDetails(), DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();

        var update = Assert.Single(await db.AnimeUpdates.AsNoTracking().ToListAsync());
        Assert.False(update.Seen);
    }

    // record-both-ends-of-a-schedule-move design.md D2/tasks 1.4: both ends
    // are written under the same kind-bit guard the Previous* fields already
    // use, so no value is stored for a kind the row does not carry.
    [Fact]
    public async Task ARecordedPremiereChangeStoresBothDates()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "My show" };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Status = WatchStatus.Watching });
        await db.SaveChangesAsync();

        var moves = new ScheduleMoveDetails(
            PreviousStartDate: new DateOnly(2026, 10, 8),
            NewStartDate: new DateOnly(2026, 10, 1));
        await CreateRecorder(db).RecordAsync(anime, AnimeUpdateKinds.StartDateChanged, moves, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();

        var update = Assert.Single(await db.AnimeUpdates.AsNoTracking().ToListAsync());
        Assert.Equal(new DateOnly(2026, 10, 8), update.PreviousStartDate);
        Assert.Equal(new DateOnly(2026, 10, 1), update.NewStartDate);
    }

    [Fact]
    public async Task ARecordedSlotChangeStoresBothSlots()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "My show" };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Status = WatchStatus.Watching });
        await db.SaveChangesAsync();

        var moves = new ScheduleMoveDetails(
            PreviousBroadcastDayOfWeek: "mondays",
            PreviousBroadcastTime: new TimeOnly(12, 0),
            NewBroadcastDayOfWeek: "tuesdays",
            NewBroadcastTime: new TimeOnly(13, 30));
        await CreateRecorder(db).RecordAsync(anime, AnimeUpdateKinds.BroadcastSlotChanged, moves, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();

        var update = Assert.Single(await db.AnimeUpdates.AsNoTracking().ToListAsync());
        Assert.Equal("mondays", update.PreviousBroadcastDayOfWeek);
        Assert.Equal(new TimeOnly(12, 0), update.PreviousBroadcastTime);
        Assert.Equal("tuesdays", update.NewBroadcastDayOfWeek);
        Assert.Equal(new TimeOnly(13, 30), update.NewBroadcastTime);
    }

    [Fact]
    public async Task ASlotChangeStoresNoPremiereValuesAndViceVersa()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "My show" };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Status = WatchStatus.Watching });
        await db.SaveChangesAsync();

        // One ScheduleMoveDetails carrying values for both kinds, but only
        // BroadcastSlotChanged in Kinds — the premiere fields must not leak
        // into the row despite being present on the struct handed in.
        var moves = new ScheduleMoveDetails(
            PreviousStartDate: new DateOnly(2026, 10, 8),
            NewStartDate: new DateOnly(2026, 10, 1),
            PreviousBroadcastDayOfWeek: "mondays",
            PreviousBroadcastTime: new TimeOnly(12, 0),
            NewBroadcastDayOfWeek: "tuesdays",
            NewBroadcastTime: new TimeOnly(13, 30));
        await CreateRecorder(db).RecordAsync(anime, AnimeUpdateKinds.BroadcastSlotChanged, moves, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();

        var update = Assert.Single(await db.AnimeUpdates.AsNoTracking().ToListAsync());
        Assert.Null(update.PreviousStartDate);
        Assert.Null(update.NewStartDate);
        Assert.NotNull(update.NewBroadcastDayOfWeek);
    }
}
