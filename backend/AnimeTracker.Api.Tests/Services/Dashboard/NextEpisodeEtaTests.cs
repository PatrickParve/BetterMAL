using AnimeTracker.Api.Services.Dashboard;

namespace AnimeTracker.Api.Tests.Services.Dashboard;

// NextEpisodeEta.From: rounds the remaining time up to the next whole hour
// before splitting into days/hours, so the countdown never reads 0d 0h while
// an episode is still ahead (page-polish-and-first-run-defaults design.md
// D1, tasks.md 1.3).
public class NextEpisodeEtaTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void NoInstantReturnsNull()
    {
        Assert.Null(NextEpisodeEta.From(null, Now));
    }

    [Fact]
    public void OneMinuteRoundsUpToOneHour()
    {
        var eta = NextEpisodeEta.From(Now + TimeSpan.FromMinutes(1), Now);
        Assert.Equal(new NextEpisodeEtaDto(0, 1), eta);
    }

    [Fact]
    public void FortyMinutesRoundsUpToOneHour()
    {
        var eta = NextEpisodeEta.From(Now + TimeSpan.FromMinutes(40), Now);
        Assert.Equal(new NextEpisodeEtaDto(0, 1), eta);
    }

    [Fact]
    public void ExactlyTwoDaysSevenHoursIsUnchanged()
    {
        var eta = NextEpisodeEta.From(Now + TimeSpan.FromDays(2) + TimeSpan.FromHours(7), Now);
        Assert.Equal(new NextEpisodeEtaDto(2, 7), eta);
    }

    [Fact]
    public void TwoDaysSixHoursTwentyMinutesRoundsUpToTwoDaysSevenHours()
    {
        var eta = NextEpisodeEta.From(Now + TimeSpan.FromDays(2) + TimeSpan.FromHours(6) + TimeSpan.FromMinutes(20), Now);
        Assert.Equal(new NextEpisodeEtaDto(2, 7), eta);
    }

    [Fact]
    public void TwentyThreeHoursThirtyMinutesRoundsUpAndCarriesIntoADay()
    {
        var eta = NextEpisodeEta.From(Now + TimeSpan.FromHours(23) + TimeSpan.FromMinutes(30), Now);
        Assert.Equal(new NextEpisodeEtaDto(1, 0), eta);
    }

    [Fact]
    public void ExactlyTenDaysIsUnchanged()
    {
        var eta = NextEpisodeEta.From(Now + TimeSpan.FromDays(10), Now);
        Assert.Equal(new NextEpisodeEtaDto(10, 0), eta);
    }
}
