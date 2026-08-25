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
    public async Task SetAnimePictureAsync_ChoosingMalMainPictureLeavesAnimeUnchosen()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata
        {
            Id = 1,
            Title = "T",
            PictureUrl = "https://mal/chosen.jpg",
            MalPictureUrl = "https://mal/main.jpg",
        });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1 });
        await db.SaveChangesAsync();
        var service = new ArtworkSelectionService(db);

        await service.SetAnimePictureAsync(1, "https://mal/main.jpg");

        var anime = await db.AnimeMetadata.AsNoTracking().SingleAsync(a => a.Id == 1);
        Assert.False(AnimePicture.IsOverridden(anime));
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
}
