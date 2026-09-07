using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Ranking;
using AnimeTracker.Api.Services.Series;
using Microsoft.EntityFrameworkCore;
using SeriesModel = AnimeTracker.Api.Models.Series;

namespace AnimeTracker.Api.Tests.Services.Series;

// A card's MAL-average reveal rule (design.md D4, spec "A card's MAL average
// obeys the series page's reveal rule") — the same composition
// EligibleSeries() uses, so a card can never reveal what the series page
// itself would blur.
public class SeriesListScoreRevealTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static void AddAnime(AnimeTrackerDbContext db, int id, string airingStatus, double? malScore = 8.0) =>
        db.AnimeMetadata.Add(new AnimeMetadata { Id = id, Title = $"Anime {id}", AiringStatus = airingStatus, MalScore = malScore });

    private static void AddMember(AnimeTrackerDbContext db, int seriesId, int animeId, int order) =>
        db.SeriesMembers.Add(new SeriesMember { AnimeId = animeId, SeriesId = seriesId, IsMainLine = true, Order = order });

    private static void AddEntry(AnimeTrackerDbContext db, int animeId, WatchStatus status) =>
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = animeId, Status = status });

    [Fact]
    public async Task SettledAndNothingAiring_MalRevealedIsTrue()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100, "finished_airing");
        AddMember(db, 100, 100, order: 0);
        AddEntry(db, 100, WatchStatus.Completed);
        await db.SaveChangesAsync();

        var index = await new SeriesRankingLookup(db).LoadAsync();
        var listed = Assert.Single(index.ListedSeries([], AnimeRankingSnapshot.Empty));

        Assert.True(listed.MalRevealed);
    }

    // Completed and Dropped count identically toward "settled" (score-visibility).
    [Fact]
    public async Task DroppedCountsTheSameAsCompleted()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100, "finished_airing");
        AddMember(db, 100, 100, order: 0);
        AddEntry(db, 100, WatchStatus.Dropped);
        await db.SaveChangesAsync();

        var index = await new SeriesRankingLookup(db).LoadAsync();
        var listed = Assert.Single(index.ListedSeries([], AnimeRankingSnapshot.Empty));

        Assert.True(listed.MalRevealed);
    }

    [Fact]
    public async Task FinishedAiringEntryAbsentFromMyList_MalRevealedIsFalse()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100, "finished_airing");
        AddAnime(db, 101, "finished_airing");
        AddMember(db, 100, 100, order: 0);
        AddMember(db, 100, 101, order: 1); // not in my list at all
        AddEntry(db, 100, WatchStatus.Completed);
        await db.SaveChangesAsync();

        var index = await new SeriesRankingLookup(db).LoadAsync();
        var listed = Assert.Single(index.ListedSeries([], AnimeRankingSnapshot.Empty));

        Assert.False(listed.MalRevealed);
    }

    [Fact]
    public async Task AMainLineEntryAiring_MalRevealedIsFalse()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100, "finished_airing");
        AddAnime(db, 101, "currently_airing");
        AddMember(db, 100, 100, order: 0);
        AddMember(db, 100, 101, order: 1);
        AddEntry(db, 100, WatchStatus.Completed);
        AddEntry(db, 101, WatchStatus.Watching);
        await db.SaveChangesAsync();

        var index = await new SeriesRankingLookup(db).LoadAsync();
        var listed = Assert.Single(index.ListedSeries([], AnimeRankingSnapshot.Empty));

        Assert.False(listed.MalRevealed);
    }
}
