using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Artwork;
using AnimeTracker.Api.Services.Infrastructure;
using AnimeTracker.Api.Services.Metadata;
using AnimeTracker.Api.Services.Tmdb;
using AnimeTracker.Api.Tests.Services.Search;
using AnimeTracker.Api.Tests.Services.IdMapping;
using AnimeTracker.Api.Tests.Services.Tmdb;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AnimeTracker.Api.Tests.Services.Artwork;

// artwork-selection spec (tasks.md 4.1/4.5) — the anime half of choosing,
// clearing, and validating a picture choice.
public class ArtworkSelectionServiceTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    // The real TMDB artwork service over the same database, with a client that
    // records what it is asked: choosing reads the cache and must never fetch,
    // so a test can assert the client saw nothing.
    private static ArtworkSelectionService CreateService(
        AnimeTrackerDbContext db, FakeAnimeSearchIndex? searchIndex = null, FakeTmdbClient? tmdbClient = null) =>
        new(db, searchIndex ?? new FakeAnimeSearchIndex(),
            new TmdbArtworkService(db, tmdbClient ?? new FakeTmdbClient(), new RefreshGate(), Options.Create(new TmdbOptions { ApiKey = "test-key" }),
                TestIdMappings.Resolver(db)));

    [Fact]
    public async Task SetAnimePictureAsync_UrlOutsideOptionSetIsRejected()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "T", PictureUrl = "https://mal/main.jpg", MalPictureUrl = "https://mal/main.jpg" });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1 });
        await db.SaveChangesAsync();
        var service = CreateService(db);

        await Assert.ThrowsAsync<ArtworkSelectionRejectedException>(
            () => service.SetAnimePictureAsync(1, "https://mal/not-an-option.jpg"));

        var anime = await db.AnimeMetadata.AsNoTracking().SingleAsync(a => a.Id == 1);
        Assert.Equal("https://mal/main.jpg", anime.PictureUrl);
    }

    [Fact]
    public async Task SetAnimePictureAsync_AnimeWithNoEntryIsRejected()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata
        {
            Id = 1,
            Title = "T",
            PictureUrl = "https://mal/main.jpg",
            MalPictureUrl = "https://mal/main.jpg",
            PictureUrls = ["https://mal/alt.jpg"],
        });
        await db.SaveChangesAsync();
        var service = CreateService(db);

        await Assert.ThrowsAsync<ArtworkSelectionRejectedException>(
            () => service.SetAnimePictureAsync(1, "https://mal/alt.jpg"));
    }

    [Fact]
    public async Task SetAnimePictureAsync_UnknownAnimeThrowsNotFound()
    {
        using var db = CreateDb();
        var service = CreateService(db);

        await Assert.ThrowsAsync<AnimeMetadataNotFoundException>(
            () => service.SetAnimePictureAsync(999, "https://mal/anything.jpg"));
    }

    [Fact]
    public async Task SetAnimePictureAsync_ChoosingMalMainPicturePinsIt()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata
        {
            Id = 1,
            Title = "T",
            SelectedPictureUrl = "https://mal/chosen.jpg",
            PictureUrl = "https://mal/chosen.jpg",
            MalPictureUrl = "https://mal/main.jpg",
        });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1 });
        await db.SaveChangesAsync();
        var service = CreateService(db);

        await service.SetAnimePictureAsync(1, "https://mal/main.jpg");

        var anime = await db.AnimeMetadata.AsNoTracking().SingleAsync(a => a.Id == 1);
        Assert.True(AnimePicture.IsOverridden(anime)); // picking MAL's own picture now pins it (design.md D6), rather than clearing
        Assert.Equal("https://mal/main.jpg", anime.SelectedPictureUrl);
        Assert.Equal("https://mal/main.jpg", anime.PictureUrl);
    }

    [Fact]
    public async Task SetAnimePictureAsync_StampsSelectedPictureModifiedAt()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata
        {
            Id = 1,
            Title = "T",
            PictureUrl = "https://mal/main.jpg",
            MalPictureUrl = "https://mal/main.jpg",
            PictureUrls = ["https://mal/main.jpg", "https://mal/alt.jpg"],
        });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1 });
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var before = DateTimeOffset.UtcNow;
        await service.SetAnimePictureAsync(1, "https://mal/alt.jpg");
        var after = DateTimeOffset.UtcNow;

        var anime = await db.AnimeMetadata.AsNoTracking().SingleAsync(a => a.Id == 1);
        Assert.NotNull(anime.SelectedPictureModifiedAt);
        Assert.InRange(anime.SelectedPictureModifiedAt!.Value, before, after);
    }

    [Fact]
    public async Task SetAnimePictureAsync_ChoosingTheSamePictureTwiceRestamps()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata
        {
            Id = 1,
            Title = "T",
            PictureUrl = "https://mal/main.jpg",
            MalPictureUrl = "https://mal/main.jpg",
            PictureUrls = ["https://mal/main.jpg", "https://mal/alt.jpg"],
        });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1 });
        await db.SaveChangesAsync();
        var service = CreateService(db);

        await service.SetAnimePictureAsync(1, "https://mal/alt.jpg");
        var first = (await db.AnimeMetadata.AsNoTracking().SingleAsync(a => a.Id == 1)).SelectedPictureModifiedAt!.Value;

        await Task.Delay(10);
        await service.SetAnimePictureAsync(1, "https://mal/alt.jpg");
        var second = (await db.AnimeMetadata.AsNoTracking().SingleAsync(a => a.Id == 1)).SelectedPictureModifiedAt!.Value;

        Assert.True(second > first); // a write, even a no-op one, re-stamps (design.md D3)
    }

    [Fact]
    public async Task SetAnimePictureAsync_ChoosingAnotherOptionLeavesAnimeChosen()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata
        {
            Id = 1,
            Title = "T",
            PictureUrl = "https://mal/main.jpg",
            MalPictureUrl = "https://mal/main.jpg",
            PictureUrls = ["https://mal/main.jpg", "https://mal/alt.jpg"],
        });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1 });
        await db.SaveChangesAsync();
        var service = CreateService(db);

        await service.SetAnimePictureAsync(1, "https://mal/alt.jpg");

        var anime = await db.AnimeMetadata.AsNoTracking().SingleAsync(a => a.Id == 1);
        Assert.True(AnimePicture.IsOverridden(anime));
        Assert.Equal("https://mal/alt.jpg", anime.PictureUrl);
    }

    [Fact]
    public async Task ResetAnimePictureAsync_RestoresMalPicture()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata
        {
            Id = 1,
            Title = "T",
            SelectedPictureUrl = "https://mal/chosen.jpg",
            PictureUrl = "https://mal/chosen.jpg",
            MalPictureUrl = "https://mal/main.jpg",
        });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1 });
        await db.SaveChangesAsync();
        var service = CreateService(db);

        await service.ResetAnimePictureAsync(1);

        var anime = await db.AnimeMetadata.AsNoTracking().SingleAsync(a => a.Id == 1);
        Assert.Equal("https://mal/main.jpg", anime.PictureUrl);
        Assert.False(AnimePicture.IsOverridden(anime));
    }

    [Fact]
    public async Task ResetAnimePictureAsync_ClearsTheChoiceAndStampsATimeNoEarlierThanTheSetBeforeIt()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata
        {
            Id = 1,
            Title = "T",
            PictureUrl = "https://mal/main.jpg",
            MalPictureUrl = "https://mal/main.jpg",
            PictureUrls = ["https://mal/main.jpg", "https://mal/alt.jpg"],
        });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1 });
        await db.SaveChangesAsync();
        var service = CreateService(db);

        await service.SetAnimePictureAsync(1, "https://mal/alt.jpg");
        var setStamp = (await db.AnimeMetadata.AsNoTracking().SingleAsync(a => a.Id == 1)).SelectedPictureModifiedAt!.Value;

        await Task.Delay(10);
        await service.ResetAnimePictureAsync(1);

        var anime = await db.AnimeMetadata.AsNoTracking().SingleAsync(a => a.Id == 1);
        Assert.Null(anime.SelectedPictureUrl);
        Assert.Equal("https://mal/main.jpg", anime.PictureUrl); // re-derived to MAL's
        Assert.True(anime.SelectedPictureModifiedAt >= setStamp); // a clear must be able to outrank an earlier set (design.md D3)
    }

    // --- 7.2: the series methods each stamp their own timestamp on set and on clear ---

    [Fact]
    public async Task SetSeriesTitleAsync_StampsSelectedTitleModifiedAt()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Series Title" });
        db.Series.Add(new AnimeTracker.Api.Models.Series { Id = 1, BuiltAt = DateTimeOffset.UtcNow });
        db.SeriesMembers.Add(new SeriesMember { SeriesId = 1, AnimeId = 1, IsMainLine = true, Order = 0 });
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var before = DateTimeOffset.UtcNow;
        await service.SetSeriesTitleAsync(1, "Series Title");
        var after = DateTimeOffset.UtcNow;

        var series = await db.Series.AsNoTracking().SingleAsync(s => s.Id == 1);
        Assert.InRange(series.SelectedTitleModifiedAt!.Value, before, after);
    }

    [Fact]
    public async Task ResetSeriesTitleAsync_ClearsTheChoiceAndStampsATimeNoEarlierThanTheSetBeforeIt()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Series Title" });
        db.Series.Add(new AnimeTracker.Api.Models.Series { Id = 1, BuiltAt = DateTimeOffset.UtcNow });
        db.SeriesMembers.Add(new SeriesMember { SeriesId = 1, AnimeId = 1, IsMainLine = true, Order = 0 });
        await db.SaveChangesAsync();
        var service = CreateService(db);

        await service.SetSeriesTitleAsync(1, "Series Title");
        var setStamp = (await db.Series.AsNoTracking().SingleAsync(s => s.Id == 1)).SelectedTitleModifiedAt!.Value;

        await Task.Delay(10);
        await service.ResetSeriesTitleAsync(1);

        var series = await db.Series.AsNoTracking().SingleAsync(s => s.Id == 1);
        Assert.Null(series.SelectedTitle);
        Assert.True(series.SelectedTitleModifiedAt >= setStamp);
    }

    [Fact]
    public async Task SetSeriesPictureAsync_StampsSelectedPictureModifiedAt()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata
        {
            Id = 1,
            Title = "T",
            PictureUrl = "https://mal/main.jpg",
            MalPictureUrl = "https://mal/main.jpg",
        });
        db.Series.Add(new AnimeTracker.Api.Models.Series { Id = 1, BuiltAt = DateTimeOffset.UtcNow });
        db.SeriesMembers.Add(new SeriesMember { SeriesId = 1, AnimeId = 1, IsMainLine = true, Order = 0 });
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var before = DateTimeOffset.UtcNow;
        await service.SetSeriesPictureAsync(1, "https://mal/main.jpg");
        var after = DateTimeOffset.UtcNow;

        var series = await db.Series.AsNoTracking().SingleAsync(s => s.Id == 1);
        Assert.InRange(series.SelectedPictureModifiedAt!.Value, before, after);
    }

    [Fact]
    public async Task ResetSeriesPictureAsync_ClearsTheChoiceAndStampsATimeNoEarlierThanTheSetBeforeIt()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata
        {
            Id = 1,
            Title = "T",
            PictureUrl = "https://mal/main.jpg",
            MalPictureUrl = "https://mal/main.jpg",
        });
        db.Series.Add(new AnimeTracker.Api.Models.Series { Id = 1, BuiltAt = DateTimeOffset.UtcNow });
        db.SeriesMembers.Add(new SeriesMember { SeriesId = 1, AnimeId = 1, IsMainLine = true, Order = 0 });
        await db.SaveChangesAsync();
        var service = CreateService(db);

        await service.SetSeriesPictureAsync(1, "https://mal/main.jpg");
        var setStamp = (await db.Series.AsNoTracking().SingleAsync(s => s.Id == 1)).SelectedPictureModifiedAt!.Value;

        await Task.Delay(10);
        await service.ResetSeriesPictureAsync(1);

        var series = await db.Series.AsNoTracking().SingleAsync(s => s.Id == 1);
        Assert.Null(series.SelectedPictureUrl);
        Assert.True(series.SelectedPictureModifiedAt >= setStamp);
    }

    // --- Adopting choices through the picker's checks (device-transfer design.md D6, tasks.md 2.5) ---

    [Fact]
    public async Task AdoptAnimePictureAsync_StoresTheGivenTimeNotNow()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata
        {
            Id = 1, Title = "T", PictureUrl = "https://mal/main.jpg", MalPictureUrl = "https://mal/main.jpg",
            PictureUrls = ["https://mal/main.jpg", "https://mal/alt.jpg"],
        });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1 });
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var fileTime = DateTimeOffset.UtcNow.AddDays(-30);

        await service.AdoptAnimePictureAsync(1, "https://mal/alt.jpg", fileTime);

        var anime = await db.AnimeMetadata.AsNoTracking().SingleAsync(a => a.Id == 1);
        Assert.Equal("https://mal/alt.jpg", anime.SelectedPictureUrl);
        Assert.Equal(fileTime, anime.SelectedPictureModifiedAt);
    }

    [Fact]
    public async Task AdoptAnimePictureAsync_AClearNeedsNoCheckEvenForAnAnimeNotInMyList()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata
        {
            Id = 1, Title = "T", SelectedPictureUrl = "https://mal/chosen.jpg",
            PictureUrl = "https://mal/chosen.jpg", MalPictureUrl = "https://mal/main.jpg",
        });
        await db.SaveChangesAsync(); // no UserAnimeEntry
        var service = CreateService(db);
        var fileTime = DateTimeOffset.UtcNow.AddDays(-1);

        await service.AdoptAnimePictureAsync(1, null, fileTime);

        var anime = await db.AnimeMetadata.AsNoTracking().SingleAsync(a => a.Id == 1);
        Assert.Null(anime.SelectedPictureUrl);
        Assert.Equal(fileTime, anime.SelectedPictureModifiedAt);
        Assert.Equal("https://mal/main.jpg", anime.PictureUrl);
    }

    [Fact]
    public async Task AdoptAnimePictureAsync_ASetIsRefusedForAnAnimeNotInMyListWithNothingModified()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "T", PictureUrl = "https://mal/main.jpg", MalPictureUrl = "https://mal/main.jpg", PictureUrls = ["https://mal/main.jpg", "https://mal/alt.jpg"] });
        await db.SaveChangesAsync();
        var service = CreateService(db);

        await Assert.ThrowsAsync<ArtworkSelectionRejectedException>(
            () => service.AdoptAnimePictureAsync(1, "https://mal/alt.jpg", DateTimeOffset.UtcNow));

        var anime = await db.AnimeMetadata.AsNoTracking().SingleAsync(a => a.Id == 1);
        Assert.Null(anime.SelectedPictureUrl);
    }

    [Fact]
    public async Task AdoptAnimePictureAsync_ASetIsRefusedForAPictureOutsideTheOptionSet()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "T", PictureUrl = "https://mal/main.jpg", MalPictureUrl = "https://mal/main.jpg" });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1 });
        await db.SaveChangesAsync();
        var service = CreateService(db);

        await Assert.ThrowsAsync<ArtworkSelectionRejectedException>(
            () => service.AdoptAnimePictureAsync(1, "https://mal/not-an-option.jpg", DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task CheckAnimePictureAsync_WritesNothing()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "T", PictureUrl = "https://mal/main.jpg", MalPictureUrl = "https://mal/main.jpg", PictureUrls = ["https://mal/main.jpg", "https://mal/alt.jpg"] });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1 });
        await db.SaveChangesAsync();
        var service = CreateService(db);

        await service.CheckAnimePictureAsync(1, "https://mal/alt.jpg");

        var anime = await db.AnimeMetadata.AsNoTracking().SingleAsync(a => a.Id == 1);
        Assert.Null(anime.SelectedPictureUrl);
        Assert.Null(anime.SelectedPictureModifiedAt);
    }

    [Fact]
    public async Task CheckAnimePictureAsync_ThrowsForAPictureOutsideTheOptionSetAndWritesNothing()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "T", PictureUrl = "https://mal/main.jpg", MalPictureUrl = "https://mal/main.jpg" });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1 });
        await db.SaveChangesAsync();
        var service = CreateService(db);

        await Assert.ThrowsAsync<ArtworkSelectionRejectedException>(
            () => service.CheckAnimePictureAsync(1, "https://mal/not-an-option.jpg"));
    }

    [Fact]
    public async Task AdoptSeriesTitleAsync_StoresTheGivenTimeNotNowAndNormalizesTheTitle()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Beyblade: Metal Fusion" });
        db.Series.Add(new AnimeTracker.Api.Models.Series { Id = 1, BuiltAt = DateTimeOffset.UtcNow });
        db.SeriesMembers.Add(new SeriesMember { SeriesId = 1, AnimeId = 1, IsMainLine = true, Order = 0 });
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var fileTime = DateTimeOffset.UtcNow.AddDays(-10);

        await service.AdoptSeriesTitleAsync(1, "  Beyblade:  ", fileTime);

        var series = await db.Series.AsNoTracking().SingleAsync(s => s.Id == 1);
        Assert.Equal("Beyblade", series.SelectedTitle); // normalized: whitespace collapsed, boundary punctuation trimmed
        Assert.Equal(fileTime, series.SelectedTitleModifiedAt);
    }

    [Fact]
    public async Task AdoptSeriesTitleAsync_ASetIsRefusedWhenTheTitleIsNotATrimOfAnOfferedTitle()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Series Title" });
        db.Series.Add(new AnimeTracker.Api.Models.Series { Id = 1, BuiltAt = DateTimeOffset.UtcNow });
        db.SeriesMembers.Add(new SeriesMember { SeriesId = 1, AnimeId = 1, IsMainLine = true, Order = 0 });
        await db.SaveChangesAsync();
        var service = CreateService(db);

        await Assert.ThrowsAsync<ArtworkSelectionRejectedException>(
            () => service.AdoptSeriesTitleAsync(1, "Invented Title", DateTimeOffset.UtcNow));

        var series = await db.Series.AsNoTracking().SingleAsync(s => s.Id == 1);
        Assert.Null(series.SelectedTitle);
    }

    [Fact]
    public async Task AdoptSeriesTitleAsync_AClearNeedsNoCheck()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Series Title" });
        db.Series.Add(new AnimeTracker.Api.Models.Series { Id = 1, BuiltAt = DateTimeOffset.UtcNow, SelectedTitle = "Series Title" });
        db.SeriesMembers.Add(new SeriesMember { SeriesId = 1, AnimeId = 1, IsMainLine = true, Order = 0 });
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var fileTime = DateTimeOffset.UtcNow.AddDays(-1);

        await service.AdoptSeriesTitleAsync(1, null, fileTime);

        var series = await db.Series.AsNoTracking().SingleAsync(s => s.Id == 1);
        Assert.Null(series.SelectedTitle);
        Assert.Equal(fileTime, series.SelectedTitleModifiedAt);
    }

    [Fact]
    public async Task AdoptSeriesPictureAsync_StoresTheGivenTimeNotNow()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "T", PictureUrl = "https://mal/main.jpg", MalPictureUrl = "https://mal/main.jpg" });
        db.Series.Add(new AnimeTracker.Api.Models.Series { Id = 1, BuiltAt = DateTimeOffset.UtcNow });
        db.SeriesMembers.Add(new SeriesMember { SeriesId = 1, AnimeId = 1, IsMainLine = true, Order = 0 });
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var fileTime = DateTimeOffset.UtcNow.AddDays(-5);

        await service.AdoptSeriesPictureAsync(1, "https://mal/main.jpg", fileTime);

        var series = await db.Series.AsNoTracking().SingleAsync(s => s.Id == 1);
        Assert.Equal("https://mal/main.jpg", series.SelectedPictureUrl);
        Assert.Equal(fileTime, series.SelectedPictureModifiedAt);
    }

    [Fact]
    public async Task CheckSeriesPictureAsync_WritesNothing()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "T", PictureUrl = "https://mal/main.jpg", MalPictureUrl = "https://mal/main.jpg" });
        db.Series.Add(new AnimeTracker.Api.Models.Series { Id = 1, BuiltAt = DateTimeOffset.UtcNow });
        db.SeriesMembers.Add(new SeriesMember { SeriesId = 1, AnimeId = 1, IsMainLine = true, Order = 0 });
        await db.SaveChangesAsync();
        var service = CreateService(db);

        await service.CheckSeriesPictureAsync(1, "https://mal/main.jpg");

        var series = await db.Series.AsNoTracking().SingleAsync(s => s.Id == 1);
        Assert.Null(series.SelectedPictureUrl);
        Assert.Null(series.SelectedPictureModifiedAt);
    }

    [Fact]
    public async Task CheckSeriesPictureAsync_ThrowsForAPictureOutsideThePoolAndWritesNothing()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "T", PictureUrl = "https://mal/main.jpg", MalPictureUrl = "https://mal/main.jpg" });
        db.Series.Add(new AnimeTracker.Api.Models.Series { Id = 1, BuiltAt = DateTimeOffset.UtcNow });
        db.SeriesMembers.Add(new SeriesMember { SeriesId = 1, AnimeId = 1, IsMainLine = true, Order = 0 });
        await db.SaveChangesAsync();
        var service = CreateService(db);

        await Assert.ThrowsAsync<ArtworkSelectionRejectedException>(
            () => service.CheckSeriesPictureAsync(1, "https://mal/not-an-option.jpg"));
    }

    // --- cache-type-ahead-search-index tasks.md 5.2: invalidation on the three anime methods, none on series ---

    [Fact]
    public async Task SetAnimePictureAsync_InvalidatesTheSearchIndex()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata
        {
            Id = 1, Title = "T", PictureUrl = "https://mal/main.jpg", MalPictureUrl = "https://mal/main.jpg",
            PictureUrls = ["https://mal/main.jpg", "https://mal/alt.jpg"],
        });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1 });
        await db.SaveChangesAsync();
        var searchIndex = new FakeAnimeSearchIndex();
        var service = CreateService(db, searchIndex);

        await service.SetAnimePictureAsync(1, "https://mal/alt.jpg");

        Assert.Equal(1, searchIndex.InvalidateCallCount);
    }

    [Fact]
    public async Task ResetAnimePictureAsync_InvalidatesTheSearchIndex()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata
        {
            Id = 1, Title = "T", SelectedPictureUrl = "https://mal/chosen.jpg",
            PictureUrl = "https://mal/chosen.jpg", MalPictureUrl = "https://mal/main.jpg",
        });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1 });
        await db.SaveChangesAsync();
        var searchIndex = new FakeAnimeSearchIndex();
        var service = CreateService(db, searchIndex);

        await service.ResetAnimePictureAsync(1);

        Assert.Equal(1, searchIndex.InvalidateCallCount);
    }

    [Fact]
    public async Task AdoptAnimePictureAsync_InvalidatesTheSearchIndex()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata
        {
            Id = 1, Title = "T", PictureUrl = "https://mal/main.jpg", MalPictureUrl = "https://mal/main.jpg",
            PictureUrls = ["https://mal/main.jpg", "https://mal/alt.jpg"],
        });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1 });
        await db.SaveChangesAsync();
        var searchIndex = new FakeAnimeSearchIndex();
        var service = CreateService(db, searchIndex);

        await service.AdoptAnimePictureAsync(1, "https://mal/alt.jpg", DateTimeOffset.UtcNow.AddDays(-1));

        Assert.Equal(1, searchIndex.InvalidateCallCount);
    }

    [Fact]
    public async Task SetSeriesTitleAsync_DoesNotInvalidateTheSearchIndex()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Series Title" });
        db.Series.Add(new AnimeTracker.Api.Models.Series { Id = 1, BuiltAt = DateTimeOffset.UtcNow });
        db.SeriesMembers.Add(new SeriesMember { SeriesId = 1, AnimeId = 1, IsMainLine = true, Order = 0 });
        await db.SaveChangesAsync();
        var searchIndex = new FakeAnimeSearchIndex();
        var service = CreateService(db, searchIndex);

        await service.SetSeriesTitleAsync(1, "Series Title");

        Assert.Equal(0, searchIndex.InvalidateCallCount);
    }

    [Fact]
    public async Task SetSeriesPictureAsync_DoesNotInvalidateTheSearchIndex()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "T", PictureUrl = "https://mal/main.jpg", MalPictureUrl = "https://mal/main.jpg" });
        db.Series.Add(new AnimeTracker.Api.Models.Series { Id = 1, BuiltAt = DateTimeOffset.UtcNow });
        db.SeriesMembers.Add(new SeriesMember { SeriesId = 1, AnimeId = 1, IsMainLine = true, Order = 0 });
        await db.SaveChangesAsync();
        var searchIndex = new FakeAnimeSearchIndex();
        var service = CreateService(db, searchIndex);

        await service.SetSeriesPictureAsync(1, "https://mal/main.jpg");

        Assert.Equal(0, searchIndex.InvalidateCallCount);
    }

    // --- TMDB images among the options (design.md D13, tasks.md 6.2) ---

    private static string Tmdb(string filePath) => TmdbImageUrl.Original(filePath);

    private static TmdbSeasonImage SeasonImage(int tvId, int season, string path) =>
        new() { TvId = tvId, SeasonNumber = season, FilePath = path, Kind = TmdbImageKind.Poster, Language = "ja", Width = 2000, Height = 3000 };

    // Attack on Titan Season 3: TMDB show 1429, season 3. Its series set and its
    // season set are cached; a different show (2000) has a set of its own.
    private static void SeedSeasonThree(AnimeTrackerDbContext db, bool inMyList = true, string? selectedPictureUrl = null)
    {
        db.AnimeMetadata.Add(new AnimeMetadata
        {
            Id = 35760,
            Title = "Attack on Titan Season 3",
            SelectedPictureUrl = selectedPictureUrl,
            PictureUrl = selectedPictureUrl ?? "https://mal/aot3.jpg",
            MalPictureUrl = "https://mal/aot3.jpg",
        });
        if (inMyList)
            db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 35760 });
        db.AnimeIdMappings.Add(new AnimeIdMapping { AnimeId = 35760, TmdbTvId = 1429, TmdbSeasonNumber = 3 });
        db.TmdbTvImageSets.Add(new TmdbTvImageSet
        {
            TvId = 1429,
            FetchedAt = DateTimeOffset.UtcNow,
            Images = [new TmdbTvImage { TvId = 1429, FilePath = "/show-backdrop.jpg", Kind = TmdbImageKind.Backdrop, Language = null, Width = 3840, Height = 2160 }],
        });
        db.TmdbSeasonImageSets.Add(new TmdbSeasonImageSet
        {
            TvId = 1429, SeasonNumber = 3, FetchedAt = DateTimeOffset.UtcNow, Images = [SeasonImage(1429, 3, "/season3-poster.jpg")],
        });
        db.TmdbSeasonImageSets.Add(new TmdbSeasonImageSet
        {
            TvId = 2000, SeasonNumber = 1, FetchedAt = DateTimeOffset.UtcNow, Images = [SeasonImage(2000, 1, "/other-show-poster.jpg")],
        });
    }

    [Fact]
    public async Task SetAnimePictureAsync_AnAnimesOwnCachedSeasonPosterIsAcceptedAndStoredAsItsFullUrl()
    {
        using var db = CreateDb();
        SeedSeasonThree(db);
        await db.SaveChangesAsync();
        var client = new FakeTmdbClient();
        var service = CreateService(db, tmdbClient: client);

        var displayed = await service.SetAnimePictureAsync(35760, Tmdb("/season3-poster.jpg"));

        var anime = await db.AnimeMetadata.AsNoTracking().SingleAsync(a => a.Id == 35760);
        Assert.Equal("https://image.tmdb.org/t/p/original/season3-poster.jpg", anime.SelectedPictureUrl); // the full URL, as a MAL pick stores its own
        Assert.Equal("https://image.tmdb.org/t/p/original/season3-poster.jpg", anime.PictureUrl); // and what every surface renders
        Assert.Equal(anime.PictureUrl, displayed);
        Assert.NotNull(anime.SelectedPictureModifiedAt);
        Assert.Empty(client.Calls); // validated against the cache; nothing was fetched
    }

    [Fact]
    public async Task SetAnimePictureAsync_ASeriesLevelTmdbBackdropOfTheAnimesShowIsAccepted()
    {
        using var db = CreateDb();
        SeedSeasonThree(db);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        await service.SetAnimePictureAsync(35760, Tmdb("/show-backdrop.jpg"));

        var anime = await db.AnimeMetadata.AsNoTracking().SingleAsync(a => a.Id == 35760);
        Assert.Equal(Tmdb("/show-backdrop.jpg"), anime.SelectedPictureUrl);
    }

    [Fact]
    public async Task SetAnimePictureAsync_ATmdbImageFromAnotherShowsSetIsRejected()
    {
        using var db = CreateDb();
        SeedSeasonThree(db);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        await Assert.ThrowsAsync<ArtworkSelectionRejectedException>(
            () => service.SetAnimePictureAsync(35760, Tmdb("/other-show-poster.jpg"))); // cached, but not from a set this anime draws from

        var anime = await db.AnimeMetadata.AsNoTracking().SingleAsync(a => a.Id == 35760);
        Assert.Null(anime.SelectedPictureUrl);
        Assert.Equal("https://mal/aot3.jpg", anime.PictureUrl);
    }

    [Fact]
    public async Task SetAnimePictureAsync_ATmdbImageThatIsNotCachedYetIsRejectedWithoutFetching()
    {
        using var db = CreateDb();
        SeedSeasonThree(db);
        await db.SaveChangesAsync();
        var client = new FakeTmdbClient();
        var service = CreateService(db, tmdbClient: client);

        await Assert.ThrowsAsync<ArtworkSelectionRejectedException>(
            () => service.SetAnimePictureAsync(35760, Tmdb("/never-cached.jpg")));

        Assert.Empty(client.Calls);
    }

    [Fact]
    public async Task SetAnimePictureAsync_AnAnimeNotInMyListIsRejectedWithATmdbUrlToo()
    {
        using var db = CreateDb();
        SeedSeasonThree(db, inMyList: false);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        await Assert.ThrowsAsync<ArtworkSelectionRejectedException>(
            () => service.SetAnimePictureAsync(35760, Tmdb("/season3-poster.jpg"))); // in its own cached set, and still refused

        var anime = await db.AnimeMetadata.AsNoTracking().SingleAsync(a => a.Id == 35760);
        Assert.Null(anime.SelectedPictureUrl);
    }

    [Fact]
    public async Task SetAnimePictureAsync_RePickingTheCurrentTmdbChoiceAfterItsSetDroppedItIsAccepted()
    {
        // The season set no longer lists /dropped.jpg, but a stored choice is
        // never re-validated away (design.md D9): it can still be re-chosen.
        var dropped = Tmdb("/dropped.jpg");
        using var db = CreateDb();
        SeedSeasonThree(db, selectedPictureUrl: dropped);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        await service.SetAnimePictureAsync(35760, dropped);

        var anime = await db.AnimeMetadata.AsNoTracking().SingleAsync(a => a.Id == 35760);
        Assert.Equal(dropped, anime.SelectedPictureUrl);
    }

    [Fact]
    public async Task SetAnimePictureAsync_ATmdbChoiceStandsAndCanBeRePickedAfterItsSetsWerePurged()
    {
        // The purge (design.md D20) deletes every set older than 150 days,
        // whatever was chosen from it. The choice is the address stored on the
        // anime, so it stands and can be chosen again; nothing else that is no
        // longer cached can.
        var chosen = Tmdb("/season3-poster.jpg");
        using var db = CreateDb();
        SeedSeasonThree(db, selectedPictureUrl: chosen);
        foreach (var set in db.TmdbTvImageSets.Local)
            set.FetchedAt = DateTimeOffset.UtcNow.AddDays(-151);
        foreach (var set in db.TmdbSeasonImageSets.Local)
            set.FetchedAt = DateTimeOffset.UtcNow.AddDays(-151);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var removed = await new TmdbCachePurgeService(db, NullLogger<TmdbCachePurgeService>.Instance).PurgeExpiredAsync();

        Assert.Equal(3, removed); // the show's series set and both season sets
        Assert.Empty(await db.TmdbSeasonImages.AsNoTracking().ToListAsync());
        var picked = await db.AnimeMetadata.AsNoTracking().SingleAsync(a => a.Id == 35760);
        Assert.Equal(chosen, picked.SelectedPictureUrl);
        Assert.Equal(chosen, picked.PictureUrl); // what every page renders

        Assert.Equal(chosen, await service.SetAnimePictureAsync(35760, chosen));
        await Assert.ThrowsAsync<ArtworkSelectionRejectedException>(
            () => service.SetAnimePictureAsync(35760, Tmdb("/season3-poster-not-the-current-one.jpg")));
    }

    [Fact]
    public async Task SetAnimePictureAsync_ACurrentTmdbChoiceDoesNotMakeOtherUncachedTmdbUrlsAcceptable()
    {
        using var db = CreateDb();
        SeedSeasonThree(db, selectedPictureUrl: Tmdb("/dropped.jpg"));
        await db.SaveChangesAsync();
        var service = CreateService(db);

        await Assert.ThrowsAsync<ArtworkSelectionRejectedException>(
            () => service.SetAnimePictureAsync(35760, Tmdb("/something-else.jpg")));
    }

    [Fact]
    public async Task CheckAnimePictureAsync_AcceptsACachedTmdbImageAndWritesNothing()
    {
        using var db = CreateDb();
        SeedSeasonThree(db);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        await service.CheckAnimePictureAsync(35760, Tmdb("/season3-poster.jpg")); // device-transfer's pre-check

        var anime = await db.AnimeMetadata.AsNoTracking().SingleAsync(a => a.Id == 35760);
        Assert.Null(anime.SelectedPictureUrl);
        Assert.Null(anime.SelectedPictureModifiedAt);
        await Assert.ThrowsAsync<ArtworkSelectionRejectedException>(
            () => service.CheckAnimePictureAsync(35760, Tmdb("/other-show-poster.jpg")));
    }

    [Fact]
    public async Task AdoptAnimePictureAsync_AcceptsACachedTmdbImageAndStoresTheGivenTime()
    {
        using var db = CreateDb();
        SeedSeasonThree(db);
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var fileTime = DateTimeOffset.UtcNow.AddDays(-20);

        await service.AdoptAnimePictureAsync(35760, Tmdb("/season3-poster.jpg"), fileTime);

        var anime = await db.AnimeMetadata.AsNoTracking().SingleAsync(a => a.Id == 35760);
        Assert.Equal(Tmdb("/season3-poster.jpg"), anime.SelectedPictureUrl);
        Assert.Equal(fileTime, anime.SelectedPictureModifiedAt);
    }

    // A series of three: the root (main line, TMDB show 100), a film that is an
    // extra (TMDB movie 900) and a spin-off that is an extra with a TMDB show of
    // its own (555). None of them is in my list: a series' picture can be
    // chosen for any series.
    private static void SeedSeriesWithTmdb(AnimeTrackerDbContext db, string? selectedPictureUrl = null)
    {
        foreach (var id in new[] { 1, 2, 3 })
            db.AnimeMetadata.Add(new AnimeMetadata { Id = id, Title = $"Anime {id}", PictureUrl = $"https://mal/{id}.jpg", MalPictureUrl = $"https://mal/{id}.jpg" });
        db.Series.Add(new AnimeTracker.Api.Models.Series { Id = 1, BuiltAt = DateTimeOffset.UtcNow, SelectedPictureUrl = selectedPictureUrl });
        db.SeriesMembers.Add(new SeriesMember { SeriesId = 1, AnimeId = 1, IsMainLine = true, Order = 0 });
        db.SeriesMembers.Add(new SeriesMember { SeriesId = 1, AnimeId = 2, IsMainLine = false, Order = 0, RelationGroup = "SideStory" });
        db.SeriesMembers.Add(new SeriesMember { SeriesId = 1, AnimeId = 3, IsMainLine = false, Order = 0, RelationGroup = "SpinOff" });
        db.AnimeIdMappings.Add(new AnimeIdMapping { AnimeId = 1, TmdbTvId = 100, TmdbSeasonNumber = 1 });
        db.AnimeIdMappings.Add(new AnimeIdMapping { AnimeId = 2, TmdbMovieIds = [900] });
        db.AnimeIdMappings.Add(new AnimeIdMapping { AnimeId = 3, TmdbTvId = 555, TmdbSeasonNumber = 1 });
        db.TmdbTvImageSets.Add(new TmdbTvImageSet
        {
            TvId = 100, FetchedAt = DateTimeOffset.UtcNow,
            Images = [new TmdbTvImage { TvId = 100, FilePath = "/show100-poster.jpg", Kind = TmdbImageKind.Poster, Language = "ja", Width = 2000, Height = 3000 }],
        });
        db.TmdbMovieImageSets.Add(new TmdbMovieImageSet
        {
            MovieId = 900, FetchedAt = DateTimeOffset.UtcNow,
            Images = [new TmdbMovieImage { MovieId = 900, FilePath = "/film900-poster.jpg", Kind = TmdbImageKind.Poster, Language = "ja", Width = 2000, Height = 3000 }],
        });
        db.TmdbTvImageSets.Add(new TmdbTvImageSet
        {
            TvId = 555, FetchedAt = DateTimeOffset.UtcNow,
            Images = [new TmdbTvImage { TvId = 555, FilePath = "/spinoff555-poster.jpg", Kind = TmdbImageKind.Poster, Language = "ja", Width = 2000, Height = 3000 }],
        });
    }

    [Fact]
    public async Task SetSeriesPictureAsync_ACachedMovieImageFromAnExtraIsAccepted()
    {
        using var db = CreateDb();
        SeedSeriesWithTmdb(db);
        await db.SaveChangesAsync();
        var client = new FakeTmdbClient();
        var service = CreateService(db, tmdbClient: client);

        var stored = await service.SetSeriesPictureAsync(1, Tmdb("/film900-poster.jpg"));

        var series = await db.Series.AsNoTracking().SingleAsync(s => s.Id == 1);
        Assert.Equal("https://image.tmdb.org/t/p/original/film900-poster.jpg", series.SelectedPictureUrl);
        Assert.Equal(series.SelectedPictureUrl, stored);
        Assert.NotNull(series.SelectedPictureModifiedAt);
        Assert.Empty(client.Calls);
    }

    [Fact]
    public async Task SetSeriesPictureAsync_ACachedImageOfTheMainLinesShowIsAccepted()
    {
        using var db = CreateDb();
        SeedSeriesWithTmdb(db);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        await service.SetSeriesPictureAsync(1, Tmdb("/show100-poster.jpg"));

        Assert.Equal(Tmdb("/show100-poster.jpg"), (await db.Series.AsNoTracking().SingleAsync(s => s.Id == 1)).SelectedPictureUrl);
    }

    [Fact]
    public async Task SetSeriesPictureAsync_AnImageOfASpinOffExtrasOwnShowIsRejected()
    {
        using var db = CreateDb();
        SeedSeriesWithTmdb(db);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        await Assert.ThrowsAsync<ArtworkSelectionRejectedException>(
            () => service.SetSeriesPictureAsync(1, Tmdb("/spinoff555-poster.jpg"))); // cached, but only the main line's shows count

        Assert.Null((await db.Series.AsNoTracking().SingleAsync(s => s.Id == 1)).SelectedPictureUrl);
    }

    [Fact]
    public async Task SetSeriesPictureAsync_ATmdbImageNoSetOfTheSeriesHoldsIsRejected()
    {
        using var db = CreateDb();
        SeedSeriesWithTmdb(db);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        await Assert.ThrowsAsync<ArtworkSelectionRejectedException>(
            () => service.SetSeriesPictureAsync(1, Tmdb("/from-nowhere.jpg")));
    }

    [Fact]
    public async Task SetSeriesPictureAsync_RePickingTheCurrentTmdbChoiceAfterItsSetDroppedItIsAccepted()
    {
        var dropped = Tmdb("/dropped.jpg");
        using var db = CreateDb();
        SeedSeriesWithTmdb(db, selectedPictureUrl: dropped);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        await service.SetSeriesPictureAsync(1, dropped);

        Assert.Equal(dropped, (await db.Series.AsNoTracking().SingleAsync(s => s.Id == 1)).SelectedPictureUrl);
    }

    [Fact]
    public async Task CheckSeriesPictureAsyncAndAdoptSeriesPictureAsync_AcceptACachedTmdbImage()
    {
        using var db = CreateDb();
        SeedSeriesWithTmdb(db);
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var fileTime = DateTimeOffset.UtcNow.AddDays(-3);

        await service.CheckSeriesPictureAsync(1, Tmdb("/film900-poster.jpg"));
        Assert.Null((await db.Series.AsNoTracking().SingleAsync(s => s.Id == 1)).SelectedPictureUrl); // a check writes nothing

        await service.AdoptSeriesPictureAsync(1, Tmdb("/film900-poster.jpg"), fileTime);

        var series = await db.Series.AsNoTracking().SingleAsync(s => s.Id == 1);
        Assert.Equal(Tmdb("/film900-poster.jpg"), series.SelectedPictureUrl);
        Assert.Equal(fileTime, series.SelectedPictureModifiedAt);
    }
}
