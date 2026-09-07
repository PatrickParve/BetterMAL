using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Artwork;
using AnimeTracker.Api.Services.Metadata;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Services.Artwork;

// artwork-selection spec (tasks.md 4.1/4.5) — the anime half of choosing,
// clearing, and validating a picture choice.
public class ArtworkSelectionServiceTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task SetAnimePictureAsync_UrlOutsideOptionSetIsRejected()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "T", PictureUrl = "https://mal/main.jpg", MalPictureUrl = "https://mal/main.jpg" });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1 });
        await db.SaveChangesAsync();
        var service = new ArtworkSelectionService(db);

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
        var service = new ArtworkSelectionService(db);

        await Assert.ThrowsAsync<ArtworkSelectionRejectedException>(
            () => service.SetAnimePictureAsync(1, "https://mal/alt.jpg"));
    }

    [Fact]
    public async Task SetAnimePictureAsync_UnknownAnimeThrowsNotFound()
    {
        using var db = CreateDb();
        var service = new ArtworkSelectionService(db);

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
        var service = new ArtworkSelectionService(db);

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
        var service = new ArtworkSelectionService(db);

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
        var service = new ArtworkSelectionService(db);

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
        var service = new ArtworkSelectionService(db);

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
        var service = new ArtworkSelectionService(db);

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
        var service = new ArtworkSelectionService(db);

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
        var service = new ArtworkSelectionService(db);

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
        var service = new ArtworkSelectionService(db);

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
        var service = new ArtworkSelectionService(db);

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
        var service = new ArtworkSelectionService(db);

        await service.SetSeriesPictureAsync(1, "https://mal/main.jpg");
        var setStamp = (await db.Series.AsNoTracking().SingleAsync(s => s.Id == 1)).SelectedPictureModifiedAt!.Value;

        await Task.Delay(10);
        await service.ResetSeriesPictureAsync(1);

        var series = await db.Series.AsNoTracking().SingleAsync(s => s.Id == 1);
        Assert.Null(series.SelectedPictureUrl);
        Assert.True(series.SelectedPictureModifiedAt >= setStamp);
    }
}
