using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Mal;

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
}
