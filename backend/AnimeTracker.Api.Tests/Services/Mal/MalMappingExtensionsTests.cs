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
    [InlineData("watching", WatchStatus.Watching)]
    [InlineData("completed", WatchStatus.Completed)]
    [InlineData("on_hold", WatchStatus.OnHold)]
    [InlineData("dropped", WatchStatus.Dropped)]
    [InlineData("plan_to_watch", WatchStatus.PlanToWatch)]
    public void TryToWatchStatusMapsEveryKnownMalStatusString(string malStatus, WatchStatus expected)
    {
        Assert.True(malStatus.TryToWatchStatus(out var status));
        Assert.Equal(expected, status);
    }

    [Fact]
    public void TryToWatchStatusReturnsFalseForAnUnknownString()
    {
        Assert.False("rewatching_v2".TryToWatchStatus(out _));
    }

    [Fact]
    public void ToWatchStatusStillThrowsOnAnUnknownString()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => "rewatching_v2".ToWatchStatus());
    }

    [Fact]
    public void HasRecognizedStatusIsTrueForANullMalListStatus()
    {
        Assert.True(((MalListStatus?)null).HasRecognizedStatus());
    }

    [Fact]
    public void HasRecognizedStatusIsTrueForANullStatusString()
    {
        Assert.True(new MalListStatus { Status = null }.HasRecognizedStatus());
    }

    [Theory]
    [InlineData("watching")]
    [InlineData("completed")]
    [InlineData("on_hold")]
    [InlineData("dropped")]
    [InlineData("plan_to_watch")]
    public void HasRecognizedStatusIsTrueForEachKnownString(string malStatus)
    {
        Assert.True(new MalListStatus { Status = malStatus }.HasRecognizedStatus());
    }

    [Fact]
    public void HasRecognizedStatusIsFalseForAnUnknownString()
    {
        Assert.False(new MalListStatus { Status = "rewatching_v2" }.HasRecognizedStatus());
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
        SelectedPictureUrl = "https://mal/chosen.jpg",
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
        Assert.Equal("https://mal/chosen.jpg", anime.SelectedPictureUrl); // the choice is untouched by a MAL sync
        Assert.Null(anime.SelectedPictureModifiedAt); // no MAL sync writes this timestamp (design.md D4)
    }

    [Fact]
    public void OverriddenRowSurvivesApplyLeanTo()
    {
        var anime = OverriddenAnime();
        NodeWithMainPicture("https://mal/new-main.jpg").ApplyLeanTo(anime, DateTimeOffset.UtcNow);

        Assert.Equal("https://mal/chosen.jpg", anime.PictureUrl);
        Assert.Equal("https://mal/new-main.jpg", anime.MalPictureUrl);
        Assert.Equal("https://mal/chosen.jpg", anime.SelectedPictureUrl);
        Assert.Null(anime.SelectedPictureModifiedAt);
    }

    [Fact]
    public void UnoverriddenRowFollowsMalMainPictureThroughApplyTo()
    {
        var anime = new AnimeMetadata { Id = 1, Title = "T", PictureUrl = "https://mal/old.jpg", MalPictureUrl = "https://mal/old.jpg" };
        NodeWithMainPicture("https://mal/new.jpg").ApplyTo(anime, DateTimeOffset.UtcNow);

        Assert.Equal("https://mal/new.jpg", anime.PictureUrl);
        Assert.Equal("https://mal/new.jpg", anime.MalPictureUrl);
        Assert.Null(anime.SelectedPictureUrl);
    }

    [Fact]
    public void UnoverriddenRowFollowsMalMainPictureThroughApplyLeanTo()
    {
        var anime = new AnimeMetadata { Id = 1, Title = "T", PictureUrl = "https://mal/old.jpg", MalPictureUrl = "https://mal/old.jpg" };
        NodeWithMainPicture("https://mal/new.jpg").ApplyLeanTo(anime, DateTimeOffset.UtcNow);

        Assert.Equal("https://mal/new.jpg", anime.PictureUrl);
        Assert.Equal("https://mal/new.jpg", anime.MalPictureUrl);
        Assert.Null(anime.SelectedPictureUrl);
    }

    // 7.6: the invariant PictureUrl == SelectedPictureUrl ?? MalPictureUrl
    // holds after a full and a lean upsert, with and without a stored choice.
    [Theory]
    [InlineData(null)]
    [InlineData("https://mal/chosen.jpg")]
    public void ApplyToAlwaysLeavesPictureUrlEqualToSelectedPictureUrlOrMalPictureUrl(string? selectedPictureUrl)
    {
        var anime = new AnimeMetadata { Id = 1, Title = "T", SelectedPictureUrl = selectedPictureUrl };
        NodeWithMainPicture("https://mal/main.jpg").ApplyTo(anime, DateTimeOffset.UtcNow);

        Assert.Equal(anime.SelectedPictureUrl ?? anime.MalPictureUrl, anime.PictureUrl);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("https://mal/chosen.jpg")]
    public void ApplyLeanToAlwaysLeavesPictureUrlEqualToSelectedPictureUrlOrMalPictureUrl(string? selectedPictureUrl)
    {
        var anime = new AnimeMetadata { Id = 1, Title = "T", SelectedPictureUrl = selectedPictureUrl };
        NodeWithMainPicture("https://mal/main.jpg").ApplyLeanTo(anime, DateTimeOffset.UtcNow);

        Assert.Equal(anime.SelectedPictureUrl ?? anime.MalPictureUrl, anime.PictureUrl);
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

    // design.md D8 (refine-sync-status-and-episode-totals tasks.md 1.3/9.9):
    // ApplyTo/ApplyLeanTo write MalTotalEpisodes rather than TotalEpisodes
    // directly, then re-derive the effective total through ResolveTotalEpisodes.
    [Fact]
    public void ApplyToWritesMalTotalEpisodesAndDerivesTheEffectiveTotal()
    {
        var anime = new AnimeMetadata { Id = 1, Title = "T" };
        var node = NodeWithMainPicture("https://mal/main.jpg");
        node.NumEpisodes = 24;

        node.ApplyTo(anime, DateTimeOffset.UtcNow);

        Assert.Equal(24, anime.MalTotalEpisodes);
        Assert.Equal(24, anime.TotalEpisodes);
    }

    [Fact]
    public void ApplyToNormalisesAZeroEpisodeCountToNull()
    {
        var anime = new AnimeMetadata { Id = 1, Title = "T" };
        var node = NodeWithMainPicture("https://mal/main.jpg");
        node.NumEpisodes = 0;

        node.ApplyTo(anime, DateTimeOffset.UtcNow);

        Assert.Null(anime.MalTotalEpisodes);
        Assert.Null(anime.TotalEpisodes);
    }

    [Fact]
    public void ApplyToWithALaterMalTotalSupersedesAPreviouslyFilledAniListTotal()
    {
        // design.md D8: MAL wins whenever it has a figure — a MAL total that
        // arrives after an AniList fallback was already stored supersedes it.
        var anime = new AnimeMetadata { Id = 1, Title = "T", AniListTotalEpisodes = 12, TotalEpisodes = 12 };
        var node = NodeWithMainPicture("https://mal/main.jpg");
        node.NumEpisodes = 24;

        node.ApplyTo(anime, DateTimeOffset.UtcNow);

        Assert.Equal(24, anime.MalTotalEpisodes);
        Assert.Equal(12, anime.AniListTotalEpisodes); // untouched
        Assert.Equal(24, anime.TotalEpisodes);
    }

    [Fact]
    public void ApplyToReportingNoEpisodeCountDoesNotBlankAPreviouslyFilledAniListTotal()
    {
        // design.md D8: a MAL refresh that still reports nothing must not
        // blank a total AniList already filled in.
        var anime = new AnimeMetadata { Id = 1, Title = "T", AniListTotalEpisodes = 12, TotalEpisodes = 12 };
        var node = NodeWithMainPicture("https://mal/main.jpg");
        node.NumEpisodes = null;

        node.ApplyTo(anime, DateTimeOffset.UtcNow);

        Assert.Null(anime.MalTotalEpisodes);
        Assert.Equal(12, anime.AniListTotalEpisodes);
        Assert.Equal(12, anime.TotalEpisodes); // still resolves through AniList's figure
    }

    [Fact]
    public void ApplyLeanToWritesMalTotalEpisodesAndDerivesTheEffectiveTotal()
    {
        var anime = new AnimeMetadata { Id = 1, Title = "T" };
        var node = NodeWithMainPicture("https://mal/main.jpg");
        node.NumEpisodes = 13;

        node.ApplyLeanTo(anime, DateTimeOffset.UtcNow);

        Assert.Equal(13, anime.MalTotalEpisodes);
        Assert.Equal(13, anime.TotalEpisodes);
    }

    [Fact]
    public void ApplyLeanToStillLeavesRichFieldsAlone()
    {
        var anime = new AnimeMetadata
        {
            Id = 1,
            Title = "T",
            Synopsis = "Existing synopsis",
            Genres = ["Action"],
            AiringStatus = "finished_airing",
        };
        var node = NodeWithMainPicture("https://mal/main.jpg");
        node.NumEpisodes = 13;

        node.ApplyLeanTo(anime, DateTimeOffset.UtcNow);

        Assert.Equal(13, anime.TotalEpisodes);
        Assert.Equal("Existing synopsis", anime.Synopsis);
        Assert.Equal(["Action"], anime.Genres);
        Assert.Equal("finished_airing", anime.AiringStatus); // rich/detail-only field, untouched by a lean upsert
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
