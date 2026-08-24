using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Series;

namespace AnimeTracker.Api.Tests.Services.Series;

// Covers ComputeStatus's precedence (design.md D1/tasks.md 1.1, 11.1):
// a main-line member currently airing outranks everything else and reads
// "Airing"; the four pre-existing arms below it are otherwise unchanged.
public class SeriesServiceComputeStatusTests
{
    private static AnimeMetadata Anime(int id, string airingStatus) =>
        new() { Id = id, Title = $"Anime {id}", AiringStatus = airingStatus };

    [Fact]
    public void MainLineEntryCurrentlyAiringReadsAiring()
    {
        var airing = Anime(1, "currently_airing");
        var mainLine = new List<AnimeMetadata> { airing };
        var members = new List<AnimeMetadata> { airing };

        Assert.Equal("Airing", SeriesService.ComputeStatus(mainLine, members));
    }

    [Fact]
    public void OnlyAnExtraAiringReadsOngoing()
    {
        var finishedMainLine = Anime(1, "finished_airing");
        var airingExtra = Anime(2, "currently_airing");
        var mainLine = new List<AnimeMetadata> { finishedMainLine };
        var members = new List<AnimeMetadata> { finishedMainLine, airingExtra };

        Assert.Equal("Ongoing", SeriesService.ComputeStatus(mainLine, members));
    }

    [Fact]
    public void MainLineAiringAlongsideAnnouncedSequelReadsAiring()
    {
        var airingSeason = Anime(1, "currently_airing");
        var announcedSequel = Anime(2, "not_yet_aired");
        var mainLine = new List<AnimeMetadata> { airingSeason, announcedSequel };
        var members = new List<AnimeMetadata> { airingSeason, announcedSequel };

        Assert.Equal("Airing", SeriesService.ComputeStatus(mainLine, members));
    }

    [Fact]
    public void MainLineAiringWithNothingFinishedReadsAiringNotUpcoming()
    {
        var firstSeasonAiring = Anime(1, "currently_airing");
        var mainLine = new List<AnimeMetadata> { firstSeasonAiring };
        var members = new List<AnimeMetadata> { firstSeasonAiring };

        Assert.Equal("Airing", SeriesService.ComputeStatus(mainLine, members));
    }

    [Fact]
    public void AnnouncedSequelWithNothingElseAiringReadsOngoing()
    {
        var finished = Anime(1, "finished_airing");
        var announcedSequel = Anime(2, "not_yet_aired");
        var mainLine = new List<AnimeMetadata> { finished, announcedSequel };
        var members = new List<AnimeMetadata> { finished, announcedSequel };

        Assert.Equal("Ongoing", SeriesService.ComputeStatus(mainLine, members));
    }

    [Fact]
    public void EverythingFinishedAndNothingUpcomingReadsFinished()
    {
        var finished = Anime(1, "finished_airing");
        var mainLine = new List<AnimeMetadata> { finished };
        var members = new List<AnimeMetadata> { finished };

        Assert.Equal("Finished", SeriesService.ComputeStatus(mainLine, members));
    }

    [Fact]
    public void NothingAiredYetReadsUpcoming()
    {
        var notYetAired = Anime(1, "not_yet_aired");
        var mainLine = new List<AnimeMetadata> { notYetAired };
        var members = new List<AnimeMetadata> { notYetAired };

        Assert.Equal("Upcoming", SeriesService.ComputeStatus(mainLine, members));
    }
}
