using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Entries;

namespace AnimeTracker.Api.Tests.Services.Entries;

// design.md D3: AiredEpisodeGate.EverythingHasAired, the union the completion
// and rewatching-eligibility gates both read (refine-sync-status-and-episode-totals
// tasks.md 3.1/9.3).
public class AiredEpisodeGateTests
{
    private static AnimeMetadata Anime(string? airingStatus, int? totalEpisodes = null) =>
        new() { Id = 1, Title = "Anime", AiringStatus = airingStatus, TotalEpisodes = totalEpisodes };

    [Fact]
    public void TrueForAFinishedStatusWithNoAiringDataAtAll()
    {
        Assert.True(AiredEpisodeGate.EverythingHasAired(Anime("finished_airing"), episodesAired: null));
    }

    [Fact]
    public void TrueForAStaleCurrentlyAiringStatusWhoseAiredCountHasReachedTheTotal()
    {
        Assert.True(AiredEpisodeGate.EverythingHasAired(Anime("currently_airing", totalEpisodes: 12), episodesAired: 12));
    }

    [Fact]
    public void TrueForAStaleCurrentlyAiringStatusWhoseAiredCountHasExceededTheTotal()
    {
        Assert.True(AiredEpisodeGate.EverythingHasAired(Anime("currently_airing", totalEpisodes: 12), episodesAired: 13));
    }

    [Fact]
    public void FalseWhileEpisodesAreStillToCome()
    {
        Assert.False(AiredEpisodeGate.EverythingHasAired(Anime("currently_airing", totalEpisodes: 12), episodesAired: 11));
    }

    [Fact]
    public void FalseForACurrentlyAiringAnimeWithAnUnknownTotal()
    {
        Assert.False(AiredEpisodeGate.EverythingHasAired(Anime("currently_airing", totalEpisodes: null), episodesAired: 11));
    }

    [Fact]
    public void FalseForACurrentlyAiringAnimeWithAnUnknownAiredCountEvenWithAKnownTotal()
    {
        Assert.False(AiredEpisodeGate.EverythingHasAired(Anime("currently_airing", totalEpisodes: 12), episodesAired: null));
    }

    [Fact]
    public void TrueForANotYetAiredStatusStandingInForAnUnrecognizedStatusValue()
    {
        // Anything other than "currently_airing" satisfies the status arm —
        // it is a union, not a lookup keyed on "finished_airing" specifically.
        Assert.True(AiredEpisodeGate.EverythingHasAired(Anime("not_yet_aired"), episodesAired: null));
    }

    // add-score-history-graph design.md D3: HasConfirmedAiring, the stricter
    // "known to have aired" test score recording reads.

    [Fact]
    public void ConfirmedAiringIsFalseForAnUnknownStatusWhereHasAiredIsTrue()
    {
        Assert.True(AiredEpisodeGate.HasAired(Anime(null), episodesAired: null));
        Assert.False(AiredEpisodeGate.HasConfirmedAiring(Anime(null), episodesAired: null));
    }

    [Fact]
    public void ConfirmedAiringIsTrueForANotYetAiredStatusWithAnAiredEpisodeOnRecord()
    {
        Assert.True(AiredEpisodeGate.HasConfirmedAiring(Anime("not_yet_aired"), episodesAired: 1));
    }

    [Fact]
    public void ConfirmedAiringIsFalseForANotYetAiredStatusWithNoAiredCount()
    {
        Assert.False(AiredEpisodeGate.HasConfirmedAiring(Anime("not_yet_aired"), episodesAired: null));
    }

    [Theory]
    [InlineData("currently_airing")]
    [InlineData("finished_airing")]
    public void ConfirmedAiringIsTrueForBothAiredStatusesWithNoAiredCount(string status)
    {
        Assert.True(AiredEpisodeGate.HasConfirmedAiring(Anime(status), episodesAired: null));
    }
}
