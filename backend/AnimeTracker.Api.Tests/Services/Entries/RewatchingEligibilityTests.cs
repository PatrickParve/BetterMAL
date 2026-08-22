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
        Assert.True(RewatchingEligibility.IsEligible(Anime(airingStatus), Entry(WatchStatus.Completed)));
    }

    [Theory]
    [InlineData("currently_airing")]
    [InlineData("not_yet_aired")]
    public void IneligibleWhenTheAnimeHasNotFinishedAiring(string airingStatus)
    {
        var entry = Entry(WatchStatus.Completed, completedAt: new DateOnly(2023, 5, 1));

        Assert.False(RewatchingEligibility.IsEligible(Anime(airingStatus), entry));
    }

    [Theory]
    [InlineData(WatchStatus.Watching)]
    [InlineData(WatchStatus.OnHold)]
    [InlineData(WatchStatus.Dropped)]
    public void EligibleFromAnotherStatusWithAFinishDate(WatchStatus status)
    {
        var entry = Entry(status, completedAt: new DateOnly(2023, 5, 1));

        Assert.True(RewatchingEligibility.IsEligible(Anime("finished_airing"), entry));
    }

    [Fact]
    public void EligibleWithARewatchCountAboveZeroButNoFinishDate()
    {
        var entry = Entry(WatchStatus.Dropped, completedAt: null, rewatchCount: 1);

        Assert.True(RewatchingEligibility.IsEligible(Anime("finished_airing"), entry));
    }

    [Fact]
    public void IneligibleWithNoFinishDateNoRewatchCountAndAStatusOtherThanCompleted()
    {
        var entry = Entry(WatchStatus.Watching, completedAt: null, rewatchCount: 0);

        Assert.False(RewatchingEligibility.IsEligible(Anime("finished_airing"), entry));
    }

    [Fact]
    public void AnEntryAlreadyRewatchingPassesToo()
    {
        var entry = Entry(WatchStatus.Rewatching, completedAt: new DateOnly(2023, 5, 1));

        Assert.True(RewatchingEligibility.IsEligible(Anime("finished_airing"), entry));
    }

    [Fact]
    public void RestoringAFinishDateRestoresEligibility()
    {
        var anime = Anime("finished_airing");
        var neverFinished = Entry(WatchStatus.Watching, completedAt: null, rewatchCount: 0);
        Assert.False(RewatchingEligibility.IsEligible(anime, neverFinished));

        var restored = Entry(WatchStatus.Watching, completedAt: new DateOnly(2023, 5, 1), rewatchCount: 0);
        Assert.True(RewatchingEligibility.IsEligible(anime, restored));
    }
}
