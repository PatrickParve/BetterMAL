using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Ranking;

namespace AnimeTracker.Api.Tests.Services.Ranking;

// design.md D2: the four-band resolution rule, tested in isolation from the
// full ordering (tasks.md 1.6).
public class RankBandResolverTests
{
    private static UserAnimeEntry Entry(
        WatchStatus status = WatchStatus.Completed,
        int? myScore = 8,
        string? mediaType = "tv",
        string? airingStatus = "finished_airing") =>
        new()
        {
            AnimeId = 1,
            Anime = new AnimeMetadata { Id = 1, Title = "Title", MediaType = mediaType, AiringStatus = airingStatus },
            Status = status,
            MyScore = myScore,
        };

    [Fact]
    public void UnscoredIsUnranked()
    {
        Assert.Equal(RankBand.Unranked, RankBandResolver.Resolve(Entry(myScore: null)));
    }

    [Fact]
    public void PlanToWatchIsUnrankedDespiteAScore()
    {
        Assert.Equal(RankBand.Unranked, RankBandResolver.Resolve(Entry(status: WatchStatus.PlanToWatch, myScore: 7)));
    }

    [Fact]
    public void UnairedIsUnranked()
    {
        Assert.Equal(RankBand.Unranked, RankBandResolver.Resolve(Entry(airingStatus: "not_yet_aired")));
    }

    [Fact]
    public void DroppedIsInTheDroppedBand()
    {
        Assert.Equal(RankBand.Dropped, RankBandResolver.Resolve(Entry(status: WatchStatus.Dropped, myScore: 7)));
    }

    [Theory]
    [InlineData("music")]
    [InlineData("cm")]
    [InlineData("pv")]
    [InlineData("MUSIC")]
    public void ShortFormMediaIsInTheShortFormBand(string mediaType)
    {
        Assert.Equal(RankBand.ShortForm, RankBandResolver.Resolve(Entry(mediaType: mediaType)));
    }

    [Fact]
    public void ADroppedMusicVideoIsInTheDroppedBandNotShortForm()
    {
        Assert.Equal(RankBand.Dropped, RankBandResolver.Resolve(Entry(status: WatchStatus.Dropped, mediaType: "music")));
    }

    [Fact]
    public void AnOrdinaryScoredAiredEntryIsHandOrdered()
    {
        Assert.Equal(RankBand.HandOrdered, RankBandResolver.Resolve(Entry()));
    }
}
