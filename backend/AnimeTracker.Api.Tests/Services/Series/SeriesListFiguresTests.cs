using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Series;
using Microsoft.EntityFrameworkCore;
using SeriesModel = AnimeTracker.Api.Models.Series;

namespace AnimeTracker.Api.Tests.Services.Series;

// The card's episode total, year span, entry count, and my-progress figures
// (design.md D7, spec "Series card content" / "Series sorting"'s My progress
// row).
public class SeriesListFiguresTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static void AddAnime(
        AnimeTrackerDbContext db, int id, string airingStatus, int? totalEpisodes = null,
        DateOnly? airedFrom = null, DateOnly? airedTo = null) =>
        db.AnimeMetadata.Add(new AnimeMetadata
        {
            Id = id,
            Title = $"Anime {id}",
            AiringStatus = airingStatus,
            TotalEpisodes = totalEpisodes,
            AiredFrom = airedFrom,
            AiredTo = airedTo,
        });

    private static void AddMember(AnimeTrackerDbContext db, int seriesId, int animeId, bool isMainLine, int order) =>
        db.SeriesMembers.Add(new SeriesMember { AnimeId = animeId, SeriesId = seriesId, IsMainLine = isMainLine, Order = order });

    private static void AddEntry(AnimeTrackerDbContext db, int animeId, WatchStatus status, int episodesWatched = 0) =>
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = animeId, Status = status, EpisodesWatched = episodesWatched });

    [Fact]
    public async Task EpisodeTotal_KnownCountsSumInFull()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 1, RootAnimeId = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100, "finished_airing", totalEpisodes: 12);
        AddAnime(db, 101, "finished_airing", totalEpisodes: 13);
        AddMember(db, 1, 100, isMainLine: true, order: 0);
        AddMember(db, 1, 101, isMainLine: true, order: 1);
        AddEntry(db, 100, WatchStatus.Completed, episodesWatched: 12);
        await db.SaveChangesAsync();

        var index = await new SeriesRankingLookup(db).LoadAsync();
        var listed = Assert.Single(index.ListedSeries([]));

        Assert.Equal(25, listed.MainLineEpisodeTotal);
        Assert.False(listed.HasUnknownEpisodeCounts);
    }

    [Fact]
    public async Task EpisodeTotal_UnknownContributesAiredSoFarAndMarksTheLowerBound()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 1, RootAnimeId = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100, "finished_airing", totalEpisodes: 12);
        AddAnime(db, 101, "currently_airing", totalEpisodes: null);
        AddMember(db, 1, 100, isMainLine: true, order: 0);
        AddMember(db, 1, 101, isMainLine: true, order: 1);
        AddEntry(db, 100, WatchStatus.Completed, episodesWatched: 12);
        AddEntry(db, 101, WatchStatus.Watching, episodesWatched: 5);
        await db.SaveChangesAsync();

        var index = await new SeriesRankingLookup(db).LoadAsync();
        var listed = Assert.Single(index.ListedSeries(new Dictionary<int, int> { [101] = 5 }));

        Assert.Equal(17, listed.MainLineEpisodeTotal); // 12 known + 5 aired-so-far
        Assert.True(listed.HasUnknownEpisodeCounts);
    }

    [Fact]
    public async Task EpisodeTotal_AllUnknownReadsZeroWithTheLowerBoundFlag()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 1, RootAnimeId = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100, "currently_airing", totalEpisodes: null);
        AddMember(db, 1, 100, isMainLine: true, order: 0);
        AddEntry(db, 100, WatchStatus.Watching);
        await db.SaveChangesAsync();

        var index = await new SeriesRankingLookup(db).LoadAsync();
        // 100 absent from the aired-episodes dictionary too: nothing known at all.
        var listed = Assert.Single(index.ListedSeries([]));

        Assert.Equal(0, listed.MainLineEpisodeTotal);
        Assert.True(listed.HasUnknownEpisodeCounts);
    }

    [Fact]
    public async Task YearSpan_SpansEveryMemberIncludingExtras()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 1, RootAnimeId = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100, "finished_airing", airedFrom: new DateOnly(2013, 4, 1), airedTo: new DateOnly(2013, 9, 1));
        AddAnime(db, 101, "finished_airing", airedFrom: new DateOnly(2022, 1, 1), airedTo: new DateOnly(2023, 3, 1)); // extra
        AddMember(db, 1, 100, isMainLine: true, order: 0);
        AddMember(db, 1, 101, isMainLine: false, order: 0);
        AddEntry(db, 100, WatchStatus.Completed);
        await db.SaveChangesAsync();

        var index = await new SeriesRankingLookup(db).LoadAsync();
        var listed = Assert.Single(index.ListedSeries([]));

        Assert.Equal(2013, listed.FirstYear);
        Assert.Equal(2023, listed.LastYear); // extra's AiredTo, later than the main-line member's
    }

    [Fact]
    public async Task YearSpan_SingleYearWhenEveryEntryAiredInOneYear()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 1, RootAnimeId = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100, "finished_airing", airedFrom: new DateOnly(2019, 1, 1), airedTo: new DateOnly(2019, 6, 1));
        AddMember(db, 1, 100, isMainLine: true, order: 0);
        AddEntry(db, 100, WatchStatus.Completed);
        await db.SaveChangesAsync();

        var index = await new SeriesRankingLookup(db).LoadAsync();
        var listed = Assert.Single(index.ListedSeries([]));

        Assert.Equal(2019, listed.FirstYear);
        Assert.Equal(2019, listed.LastYear);
    }

    [Fact]
    public async Task EntryCount_CoversExtras()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 1, RootAnimeId = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100, "finished_airing");
        AddAnime(db, 101, "finished_airing");
        AddAnime(db, 102, "finished_airing"); // extra
        AddMember(db, 1, 100, isMainLine: true, order: 0);
        AddMember(db, 1, 101, isMainLine: true, order: 1);
        AddMember(db, 1, 102, isMainLine: false, order: 0);
        AddEntry(db, 100, WatchStatus.Completed);
        await db.SaveChangesAsync();

        var index = await new SeriesRankingLookup(db).LoadAsync();
        var listed = Assert.Single(index.ListedSeries([]));

        Assert.Equal(3, listed.EntryCount);
    }

    [Fact]
    public async Task MyProgressFigures_WatchedAndAiredAreMainLineOnly()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 1, RootAnimeId = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100, "finished_airing", totalEpisodes: 12);
        AddAnime(db, 101, "currently_airing", totalEpisodes: 24);
        AddAnime(db, 102, "finished_airing", totalEpisodes: 6); // extra — must not count
        AddMember(db, 1, 100, isMainLine: true, order: 0);
        AddMember(db, 1, 101, isMainLine: true, order: 1);
        AddMember(db, 1, 102, isMainLine: false, order: 0);
        AddEntry(db, 100, WatchStatus.Completed, episodesWatched: 12);
        AddEntry(db, 101, WatchStatus.Watching, episodesWatched: 8);
        AddEntry(db, 102, WatchStatus.Completed, episodesWatched: 6);
        await db.SaveChangesAsync();

        var index = await new SeriesRankingLookup(db).LoadAsync();
        var listed = Assert.Single(index.ListedSeries(new Dictionary<int, int> { [101] = 10 }));

        Assert.Equal(20, listed.MainLineWatchedEpisodes); // 12 + 8, extra's 6 excluded
        Assert.Equal(22, listed.MainLineAiredEpisodes); // 12 (finished, full total) + min(10, 24)
    }
}
