using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Entries;

namespace AnimeTracker.Api.Tests.Services.Entries;

// design.md D0 / list-editing "Rewatching requires a finished anime the user
// has finished once" (tasks.md 5.4-5.5).
public class RewatchingEligibilityTests
{
    private static AnimeMetadata Anime(string? airingStatus) =>
        new() { Id = 1, Title = "Anime", AiringStatus = airingStatus };

    private static UserAnimeEntry Entry(WatchStatus status, DateOnly? completedAt = null, int rewatchCount = 0) =>
        new() { AnimeId = 1, Status = status, CompletedAt = completedAt, RewatchCount = rewatchCount };

    [Theory]
    [InlineData("finished_airing")]
    [InlineData(null)]
    public void EligibleFromCompletedOnAFinishedOrUnrecordedAnime(string? airingStatus)
    {
        Assert.True(RewatchingEligibility.IsEligible(Anime(airingStatus), Entry(WatchStatus.Completed), episodesAired: null));
    }

    [Fact]
    public void IneligibleWhenTheAnimeIsCurrentlyAiringWithNoEvidenceItHasFinished()
    {
        var entry = Entry(WatchStatus.Completed, completedAt: new DateOnly(2023, 5, 1));

        Assert.False(RewatchingEligibility.IsEligible(Anime("currently_airing"), entry, episodesAired: null));
    }

    // design.md D3: EverythingHasAired's status arm excludes only
    // `currently_airing`, not `not_yet_aired` specifically — the airing-status
    // check is no longer a lookup keyed on "finished_airing", but this is
    // inert in practice: nothing in the app can produce a not-yet-aired
    // anime with completion evidence (HasFinishedOnce) in the first place.
    [Fact]
    public void ANotYetAiredAnimeIsNoLongerSpecialCasedByTheStatusArmAlone()
    {
        var entry = Entry(WatchStatus.Completed, completedAt: new DateOnly(2023, 5, 1));

        Assert.True(RewatchingEligibility.IsEligible(Anime("not_yet_aired"), entry, episodesAired: null));
    }

    [Theory]
    [InlineData(WatchStatus.Watching)]
    [InlineData(WatchStatus.OnHold)]
    [InlineData(WatchStatus.Dropped)]
    public void EligibleFromAnotherStatusWithAFinishDate(WatchStatus status)
    {
        var entry = Entry(status, completedAt: new DateOnly(2023, 5, 1));

        Assert.True(RewatchingEligibility.IsEligible(Anime("finished_airing"), entry, episodesAired: null));
    }

    [Fact]
    public void EligibleWithARewatchCountAboveZeroButNoFinishDate()
    {
        var entry = Entry(WatchStatus.Dropped, completedAt: null, rewatchCount: 1);

        Assert.True(RewatchingEligibility.IsEligible(Anime("finished_airing"), entry, episodesAired: null));
    }

    [Fact]
    public void IneligibleWithNoFinishDateNoRewatchCountAndAStatusOtherThanCompleted()
    {
        var entry = Entry(WatchStatus.Watching, completedAt: null, rewatchCount: 0);

        Assert.False(RewatchingEligibility.IsEligible(Anime("finished_airing"), entry, episodesAired: null));
    }

    [Fact]
    public void AnEntryAlreadyRewatchingPassesToo()
    {
        var entry = Entry(WatchStatus.Rewatching, completedAt: new DateOnly(2023, 5, 1));

        Assert.True(RewatchingEligibility.IsEligible(Anime("finished_airing"), entry, episodesAired: null));
    }

    [Fact]
    public void RestoringAFinishDateRestoresEligibility()
    {
        var anime = Anime("finished_airing");
        var neverFinished = Entry(WatchStatus.Watching, completedAt: null, rewatchCount: 0);
        Assert.False(RewatchingEligibility.IsEligible(anime, neverFinished, episodesAired: null));

        var restored = Entry(WatchStatus.Watching, completedAt: new DateOnly(2023, 5, 1), rewatchCount: 0);
        Assert.True(RewatchingEligibility.IsEligible(anime, restored, episodesAired: null));
    }

    // design.md D3: EverythingHasAired's aired-count arm, exercised through
    // RewatchingEligibility (refine-sync-status-and-episode-totals tasks.md
    // 3.2/9.5) — a stale `currently_airing` status no longer blocks a rewatch
    // of a show whose aired count has actually reached its total.
    [Fact]
    public void EligibleOnAStaleCurrentlyAiringAnimeWhoseAiredCountHasReachedTheTotal()
    {
        var anime = new AnimeMetadata { Id = 1, Title = "Anime", AiringStatus = "currently_airing", TotalEpisodes = 12 };
        var entry = Entry(WatchStatus.Completed, completedAt: new DateOnly(2023, 5, 1));

        Assert.True(RewatchingEligibility.IsEligible(anime, entry, episodesAired: 12));
    }

    [Fact]
    public void IneligibleOnACurrentlyAiringAnimeWhoseAiredCountHasNotYetReachedTheTotal()
    {
        var anime = new AnimeMetadata { Id = 1, Title = "Anime", AiringStatus = "currently_airing", TotalEpisodes = 12 };
        var entry = Entry(WatchStatus.Completed, completedAt: new DateOnly(2023, 5, 1));

        Assert.False(RewatchingEligibility.IsEligible(anime, entry, episodesAired: 11));
    }

    [Fact]
    public void IneligibilityReasonNamesTheAiringConditionWhenTheAnimeHasNotFinishedAiring()
    {
        var anime = Anime("currently_airing");
        var entry = Entry(WatchStatus.Completed, completedAt: new DateOnly(2023, 5, 1));

        Assert.Equal("the anime has not aired in full", RewatchingEligibility.IneligibilityReason(anime, entry, episodesAired: null));
    }

    [Fact]
    public void IneligibilityReasonNamesTheNeverFinishedConditionOnAFinishedAnimeNeverCompleted()
    {
        var anime = Anime("finished_airing");
        var entry = Entry(WatchStatus.Watching, completedAt: null, rewatchCount: 0);

        Assert.Equal("the anime has never been finished", RewatchingEligibility.IneligibilityReason(anime, entry, episodesAired: null));
    }
}
