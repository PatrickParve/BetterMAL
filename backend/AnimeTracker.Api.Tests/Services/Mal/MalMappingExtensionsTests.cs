using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Mal.Dto;

namespace AnimeTracker.Api.Tests.Services.Mal;

// mal-write-sync "Rewatching is pushed to MyAnimeList as watching" (tasks.md
// 5.7) — the test that would have caught ToMalStatusString's `_ => throw`
// going unhandled for the new enum value.
public class MalMappingExtensionsTests
{
    [Fact]
    public void RewatchingIsPushedAsWatching()
    {
        Assert.Equal("watching", WatchStatus.Rewatching.ToMalStatusString());
    }

    [Theory]
    [InlineData(WatchStatus.Watching, "watching")]
    [InlineData(WatchStatus.Completed, "completed")]
    [InlineData(WatchStatus.OnHold, "on_hold")]
    [InlineData(WatchStatus.Dropped, "dropped")]
    [InlineData(WatchStatus.PlanToWatch, "plan_to_watch")]
    [InlineData(WatchStatus.Rewatching, "watching")]
    public void EveryLocallyDefinedStatusMapsToAMalStatusValue(WatchStatus status, string expected)
    {
        Assert.Equal(expected, status.ToMalStatusString());
    }

    private static AnimeMetadata OverriddenAnime() => new()
    {
        Id = 1,
        Title = "Original Title",
        PictureUrl = "https://mal/chosen.jpg",
        MalPictureUrl = "https://mal/old-main.jpg",
    };

    private static MalAnimeNode NodeWithMainPicture(string url) => new()
    {
        Id = 1,
        Title = "Original Title",
        MainPicture = new MalMainPicture { Large = url },
    };

    [Fact]
    public void OverriddenRowSurvivesApplyTo()
    {
        var anime = OverriddenAnime();
        NodeWithMainPicture("https://mal/new-main.jpg").ApplyTo(anime, DateTimeOffset.UtcNow);

        Assert.Equal("https://mal/chosen.jpg", anime.PictureUrl);
        Assert.Equal("https://mal/new-main.jpg", anime.MalPictureUrl);
    }

    [Fact]
    public void OverriddenRowSurvivesApplyLeanTo()
    {
        var anime = OverriddenAnime();
        NodeWithMainPicture("https://mal/new-main.jpg").ApplyLeanTo(anime, DateTimeOffset.UtcNow);

        Assert.Equal("https://mal/chosen.jpg", anime.PictureUrl);
        Assert.Equal("https://mal/new-main.jpg", anime.MalPictureUrl);
    }

    [Fact]
    public void UnoverriddenRowFollowsMalMainPictureThroughApplyTo()
    {
        var anime = new AnimeMetadata { Id = 1, Title = "T", PictureUrl = "https://mal/old.jpg", MalPictureUrl = "https://mal/old.jpg" };
        NodeWithMainPicture("https://mal/new.jpg").ApplyTo(anime, DateTimeOffset.UtcNow);

        Assert.Equal("https://mal/new.jpg", anime.PictureUrl);
        Assert.Equal("https://mal/new.jpg", anime.MalPictureUrl);
    }

    [Fact]
    public void UnoverriddenRowFollowsMalMainPictureThroughApplyLeanTo()
    {
        var anime = new AnimeMetadata { Id = 1, Title = "T", PictureUrl = "https://mal/old.jpg", MalPictureUrl = "https://mal/old.jpg" };
        NodeWithMainPicture("https://mal/new.jpg").ApplyLeanTo(anime, DateTimeOffset.UtcNow);

        Assert.Equal("https://mal/new.jpg", anime.PictureUrl);
        Assert.Equal("https://mal/new.jpg", anime.MalPictureUrl);
    }

    [Fact]
    public void PicturesNullNodeLeavesStoredSetAndTimestampAlone()
    {
        var syncedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var anime = new AnimeMetadata
        {
            Id = 1,
            Title = "T",
            PictureUrls = ["https://mal/existing.jpg"],
            PicturesSyncedAt = syncedAt,
        };

        var node = NodeWithMainPicture("https://mal/main.jpg");
        node.Pictures = null;
        node.ApplyTo(anime, DateTimeOffset.UtcNow);

        Assert.Equal(["https://mal/existing.jpg"], anime.PictureUrls);
        Assert.Equal(syncedAt, anime.PicturesSyncedAt);
    }

    [Fact]
    public void PicturesPresentNodeStoresLargeInMalOrderAndStampsTimestamp()
    {
        var anime = new AnimeMetadata { Id = 1, Title = "T" };
        var now = DateTimeOffset.UtcNow;

        var node = NodeWithMainPicture("https://mal/main.jpg");
        node.Pictures =
        [
            new MalMainPicture { Large = "https://mal/p1-large.jpg", Medium = "https://mal/p1-medium.jpg" },
            new MalMainPicture { Medium = "https://mal/p2-medium.jpg" },
        ];
        node.ApplyTo(anime, now);

        Assert.Equal(["https://mal/p1-large.jpg", "https://mal/p2-medium.jpg"], anime.PictureUrls);
        Assert.Equal(now, anime.PicturesSyncedAt);
    }
}
