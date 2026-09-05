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
}
