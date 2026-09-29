using AnimeTracker.Api.Services.Airing.AniList;

namespace AnimeTracker.Api.Tests.Services.Airing.AniList;

// episode-airing-data spec, "AniList request pacing"; design D11. The pacer's
// clock and its waiting are stand-ins here: a wait is recorded and moves the
// clock forward by exactly that much, so each interval is exact and nothing
// sleeps.
public class AniListRequestPacerTests
{
    private sealed class Harness
    {
        public DateTimeOffset Now { get; set; } = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);
        public List<TimeSpan> Waits { get; } = [];
        public AniListRequestPacer Pacer { get; }

        public Harness()
        {
            Pacer = new AniListRequestPacer(() => Now, (wait, _) =>
            {
                Waits.Add(wait);
                Now += wait;
                return Task.CompletedTask;
            });
        }
    }

    [Fact]
    public async Task TheFirstRequestIsNotDelayed()
    {
        var h = new Harness();

        await h.Pacer.WaitAsync(CancellationToken.None);

        Assert.Empty(h.Waits);
    }

    [Fact]
    public async Task UntilAResponseHasBeenReadTheLowestPublishedLimitIsAssumed()
    {
        var h = new Harness();

        await h.Pacer.WaitAsync(CancellationToken.None);
        await h.Pacer.WaitAsync(CancellationToken.None);

        Assert.Equal(30, AniListRequestPacer.DefaultLimitPerMinute);
        Assert.Equal(TimeSpan.FromSeconds(2), h.Pacer.Interval);
        Assert.Equal([TimeSpan.FromSeconds(2)], h.Waits);
    }

    [Fact]
    public async Task SpacingFollowsTheLimitTheLatestResponseStates()
    {
        var h = new Harness();
        await h.Pacer.WaitAsync(CancellationToken.None);
        h.Pacer.Observe(30, 29, null);
        await h.Pacer.WaitAsync(CancellationToken.None);

        h.Pacer.Observe(90, 89, null);
        await h.Pacer.WaitAsync(CancellationToken.None);

        Assert.Equal(TimeSpan.FromSeconds(2), h.Waits[0]);
        Assert.Equal(TimeSpan.FromSeconds(60.0 / 90), h.Waits[1]);
    }

    [Fact]
    public async Task TimeAlreadySpentBetweenRequestsCountsTowardTheSpacing()
    {
        var h = new Harness();
        await h.Pacer.WaitAsync(CancellationToken.None);

        h.Now += TimeSpan.FromSeconds(1.5);
        await h.Pacer.WaitAsync(CancellationToken.None);

        Assert.Equal([TimeSpan.FromSeconds(0.5)], h.Waits);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void ALimitThatIsNotPositiveIsIgnored(int limit)
    {
        var h = new Harness();

        h.Pacer.Observe(limit, null, null);

        Assert.Equal(TimeSpan.FromSeconds(2), h.Pacer.Interval);
    }

    [Fact]
    public async Task WithNothingRemainingTheNextRequestWaitsForTheReset()
    {
        var h = new Harness();
        await h.Pacer.WaitAsync(CancellationToken.None);

        h.Pacer.Observe(30, 0, h.Now + TimeSpan.FromSeconds(20));
        await h.Pacer.WaitAsync(CancellationToken.None);

        Assert.Equal([TimeSpan.FromSeconds(20)], h.Waits);
    }

    [Fact]
    public async Task WithNothingRemainingAndNoResetTheNextRequestWaitsAFullMinute()
    {
        var h = new Harness();
        await h.Pacer.WaitAsync(CancellationToken.None);

        h.Pacer.Observe(30, 0, null);
        await h.Pacer.WaitAsync(CancellationToken.None);

        Assert.Equal([TimeSpan.FromMinutes(1)], h.Waits);
    }

    [Fact]
    public async Task AResetMoreThanAWindowAwayIsWaitedOutForOnlyAWindow()
    {
        var h = new Harness();
        await h.Pacer.WaitAsync(CancellationToken.None);

        h.Pacer.Observe(30, 0, h.Now + TimeSpan.FromHours(1));
        await h.Pacer.WaitAsync(CancellationToken.None);

        Assert.Equal([TimeSpan.FromMinutes(1)], h.Waits);
    }

    [Fact]
    public async Task ARemainingCountAboveZeroHoldsNothingBack()
    {
        var h = new Harness();
        await h.Pacer.WaitAsync(CancellationToken.None);

        h.Pacer.Observe(30, 1, h.Now + TimeSpan.FromSeconds(20));
        await h.Pacer.WaitAsync(CancellationToken.None);

        Assert.Equal([TimeSpan.FromSeconds(2)], h.Waits);
    }

    [Fact]
    public async Task AWaitForTheResetIsOverOnceItHasBeenServed()
    {
        var h = new Harness();
        await h.Pacer.WaitAsync(CancellationToken.None);
        h.Pacer.Observe(30, 0, null);
        await h.Pacer.WaitAsync(CancellationToken.None);

        await h.Pacer.WaitAsync(CancellationToken.None);

        Assert.Equal([TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(2)], h.Waits);
    }

    [Fact]
    public async Task BlockForHoldsTheNextRequestAndReportsWhenItResumes()
    {
        var h = new Harness();
        await h.Pacer.WaitAsync(CancellationToken.None);
        var expected = h.Now + TimeSpan.FromSeconds(30);

        var resumesAt = h.Pacer.BlockFor(TimeSpan.FromSeconds(30));
        await h.Pacer.WaitAsync(CancellationToken.None);

        Assert.Equal(expected, resumesAt);
        Assert.Equal([TimeSpan.FromSeconds(30)], h.Waits);
    }

    [Fact]
    public async Task ABlockIsNeverShortenedByALaterOneThatEndsSooner()
    {
        var h = new Harness();
        await h.Pacer.WaitAsync(CancellationToken.None);

        h.Pacer.BlockFor(TimeSpan.FromSeconds(30));
        h.Pacer.BlockFor(TimeSpan.FromSeconds(5));
        h.Pacer.Observe(30, 0, h.Now + TimeSpan.FromSeconds(10));
        await h.Pacer.WaitAsync(CancellationToken.None);

        Assert.Equal([TimeSpan.FromSeconds(30)], h.Waits);
    }

    [Fact]
    public async Task ACancelledCallerStopsWaitingForItsTurn()
    {
        var pacer = new AniListRequestPacer();
        using var cts = new CancellationTokenSource();
        await pacer.WaitAsync(CancellationToken.None);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pacer.WaitAsync(cts.Token));
    }
}
