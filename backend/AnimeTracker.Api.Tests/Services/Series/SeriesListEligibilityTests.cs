using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Ranking;
using AnimeTracker.Api.Services.Series;
using Microsoft.EntityFrameworkCore;
using SeriesModel = AnimeTracker.Api.Models.Series;

namespace AnimeTracker.Api.Tests.Services.Series;

// Series page eligibility (design.md D3, spec "The Series page lists every
// series with a member in my list"): membership alone, deliberately not
// EligibleSeries()'s two-aired-main-line-entries coverage rule.
public class SeriesListEligibilityTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static void AddAnime(
        AnimeTrackerDbContext db, int id, string airingStatus = "finished_airing", int? totalEpisodes = null) =>
        db.AnimeMetadata.Add(new AnimeMetadata { Id = id, Title = $"Anime {id}", AiringStatus = airingStatus, TotalEpisodes = totalEpisodes });

    private static void AddMember(AnimeTrackerDbContext db, int seriesId, int animeId, bool isMainLine, int order) =>
        db.SeriesMembers.Add(new SeriesMember { AnimeId = animeId, SeriesId = seriesId, IsMainLine = isMainLine, Order = order });

    private static void AddEntry(AnimeTrackerDbContext db, int animeId, WatchStatus status) =>
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = animeId, Status = status });

    [Fact]
    public async Task EmptyStore_ReturnsEmptyList()
    {
        using var db = CreateDb();
        await db.SaveChangesAsync(); // no Series rows at all -> the cheap empty path

        var index = await new SeriesRankingLookup(db).LoadAsync();

        Assert.Empty(index.ListedSeries([], AnimeRankingSnapshot.Empty));
    }

    [Fact]
    public async Task OneMemberInMyList_SeriesIsListed()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 1, RootAnimeId = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100);
        AddAnime(db, 101);
        AddAnime(db, 102);
        AddAnime(db, 103);
        AddAnime(db, 104);
        AddMember(db, 1, 100, isMainLine: true, order: 0);
        AddMember(db, 1, 101, isMainLine: true, order: 1);
        AddMember(db, 1, 102, isMainLine: true, order: 2);
        AddMember(db, 1, 103, isMainLine: true, order: 3);
        AddMember(db, 1, 104, isMainLine: true, order: 4);
        AddEntry(db, 102, WatchStatus.Watching); // exactly one of five in my list
        await db.SaveChangesAsync();

        var index = await new SeriesRankingLookup(db).LoadAsync();
        var listed = Assert.Single(index.ListedSeries([], AnimeRankingSnapshot.Empty));

        Assert.Equal(1, listed.SeriesId);
    }

    [Fact]
    public async Task NoMemberInMyList_SeriesIsNotListed()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 1, RootAnimeId = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100);
        AddMember(db, 1, 100, isMainLine: true, order: 0);
        await db.SaveChangesAsync();

        var index = await new SeriesRankingLookup(db).LoadAsync();

        Assert.Empty(index.ListedSeries([], AnimeRankingSnapshot.Empty));
    }

    [Fact]
    public async Task ExtraOnlyMembership_SeriesIsStillListed()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 1, RootAnimeId = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100);
        AddAnime(db, 101);
        AddMember(db, 1, 100, isMainLine: true, order: 0);
        AddMember(db, 1, 101, isMainLine: false, order: 0); // extra
        AddEntry(db, 101, WatchStatus.Completed); // only the extra is in my list
        await db.SaveChangesAsync();

        var index = await new SeriesRankingLookup(db).LoadAsync();
        var listed = Assert.Single(index.ListedSeries([], AnimeRankingSnapshot.Empty));

        Assert.Equal(1, listed.SeriesId);
    }

    // EligibleSeries() (Top series) requires at least two of a three-or-more
    // aired-main-line-entry franchise to be in my list; ListedSeries()
    // deliberately doesn't apply that rule (design.md D3).
    [Fact]
    public async Task FranchiseWithOnlyOneOfThreeAiredMainLineEntriesInMyList_IsListedHereButNotInTopSeries()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 1, RootAnimeId = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100);
        AddAnime(db, 101);
        AddAnime(db, 102);
        AddMember(db, 1, 100, isMainLine: true, order: 0);
        AddMember(db, 1, 101, isMainLine: true, order: 1);
        AddMember(db, 1, 102, isMainLine: true, order: 2);
        AddEntry(db, 100, WatchStatus.Completed); // only one of three aired main-line entries
        await db.SaveChangesAsync();

        var index = await new SeriesRankingLookup(db).LoadAsync();

        Assert.Empty(index.EligibleSeries());
        Assert.Single(index.ListedSeries([], AnimeRankingSnapshot.Empty));
    }
}
