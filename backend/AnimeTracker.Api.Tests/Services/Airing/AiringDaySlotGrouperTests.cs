using AnimeTracker.Api.Services.Airing;

namespace AnimeTracker.Api.Tests.Services.Airing;

public class AiringDaySlotGrouperTests
{
    private static readonly DateTimeOffset Day = new(2026, 8, 10, 0, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset At(int hour, int minute = 0) => Day.AddHours(hour).AddMinutes(minute);

    private static AiringDaySlotGrouper.Row Row(int animeId, int? episode, DateTimeOffset at) =>
        new(animeId, $"Anime {animeId}", null, null, at, at.ToString("HH:mm"), episode);

    [Fact]
    public void SimultaneousEpisodesMerge()
    {
        var rows = new[]
        {
            Row(1, 1, At(20)),
            Row(1, 2, At(20)),
        };

        var slot = Assert.Single(AiringDaySlotGrouper.Group(rows));

        Assert.Equal(1, slot.EpisodeNumber);
        Assert.Equal(2, slot.EpisodeNumberEnd);
        Assert.Equal("20:00", slot.LocalTime);
    }

    [Fact]
    public void NonSimultaneousSameDayEpisodesMerge()
    {
        var rows = new[]
        {
            Row(1, 1, At(9)),
            Row(1, 2, At(20)),
        };

        var slot = Assert.Single(AiringDaySlotGrouper.Group(rows));

        Assert.Equal(1, slot.EpisodeNumber);
        Assert.Equal(2, slot.EpisodeNumberEnd);
        Assert.Equal("09:00", slot.LocalTime);
    }

    [Fact]
    public void InterruptionSplitsRun()
    {
        var rows = new[]
        {
            Row(1, 1, At(9)),
            Row(2, 1, At(12)), // other anime, strictly between anime 1's two episodes
            Row(1, 2, At(20)),
        };

        var slots = AiringDaySlotGrouper.Group(rows);

        Assert.Equal(3, slots.Count);

        Assert.Equal(1, slots[0].AnimeId);
        Assert.Equal(1, slots[0].EpisodeNumber);
        Assert.Null(slots[0].EpisodeNumberEnd);

        Assert.Equal(2, slots[1].AnimeId);

        Assert.Equal(1, slots[2].AnimeId);
        Assert.Equal(2, slots[2].EpisodeNumber);
        Assert.Null(slots[2].EpisodeNumberEnd);
    }

    [Fact]
    public void TieAtEarliestPushesOtherAnimeBeforeWithoutSplitting()
    {
        var rows = new[]
        {
            Row(2, 1, At(9)), // other anime, exact same instant as the group's first episode
            Row(1, 1, At(9)),
            Row(1, 2, At(20)),
        };

        var slots = AiringDaySlotGrouper.Group(rows);

        Assert.Equal(2, slots.Count);
        Assert.Equal(2, slots[0].AnimeId);
        Assert.Equal(1, slots[1].AnimeId);
        Assert.Equal(1, slots[1].EpisodeNumber);
        Assert.Equal(2, slots[1].EpisodeNumberEnd);
    }

    [Fact]
    public void TieAtLatestPushesOtherAnimeAfterWithoutSplitting()
    {
        var rows = new[]
        {
            Row(1, 1, At(9)),
            Row(1, 2, At(20)),
            Row(2, 1, At(20)), // other anime, exact same instant as the group's last episode
        };

        var slots = AiringDaySlotGrouper.Group(rows);

        Assert.Equal(2, slots.Count);
        Assert.Equal(1, slots[0].AnimeId);
        Assert.Equal(1, slots[0].EpisodeNumber);
        Assert.Equal(2, slots[0].EpisodeNumberEnd);
        Assert.Equal(2, slots[1].AnimeId);
    }

    [Fact]
    public void AllSameInstantBatchMergesAsOneRange()
    {
        var rows = new[]
        {
            Row(1, 5, At(0)),
            Row(1, 6, At(0)),
            Row(1, 7, At(0)),
        };

        var slot = Assert.Single(AiringDaySlotGrouper.Group(rows));

        Assert.Equal(5, slot.EpisodeNumber);
        Assert.Equal(7, slot.EpisodeNumberEnd);
    }

    [Fact]
    public void UnknownEpisodeNeverMergesAndDoesNotBlockSameAnime()
    {
        var rows = new[]
        {
            Row(1, 1, At(9)),
            Row(1, null, At(12)), // unknown episode number, same anime
            Row(1, 2, At(20)),
        };

        var slots = AiringDaySlotGrouper.Group(rows);

        Assert.Equal(2, slots.Count);

        var merged = slots.Single(s => s.EpisodeNumber == 1);
        Assert.Equal(2, merged.EpisodeNumberEnd);

        var unknown = slots.Single(s => s.EpisodeNumber is null);
        Assert.Null(unknown.EpisodeNumberEnd);
    }

    [Fact]
    public void DifferentAnimeNeverMerge()
    {
        var rows = new[]
        {
            Row(1, 1, At(9)),
            Row(2, 1, At(9)),
        };

        var slots = AiringDaySlotGrouper.Group(rows);

        Assert.Equal(2, slots.Count);
        Assert.All(slots, s => Assert.Null(s.EpisodeNumberEnd));
    }

    [Fact]
    public void DifferentLocalDaysNeverMerge()
    {
        // Day-bucketing happens before Group() is called — AiringScheduleService
        // invokes it once per local day, so rows from different days are never
        // passed into the same call and can never merge, even with contiguous
        // episode numbers close together across midnight.
        var lateOnDayOne = Row(1, 1, At(23, 59));
        var earlyOnDayTwo = Row(1, 2, At(0, 1));

        var dayOneSlot = Assert.Single(AiringDaySlotGrouper.Group([lateOnDayOne]));
        var dayTwoSlot = Assert.Single(AiringDaySlotGrouper.Group([earlyOnDayTwo]));

        Assert.Equal(1, dayOneSlot.EpisodeNumber);
        Assert.Null(dayOneSlot.EpisodeNumberEnd);
        Assert.Equal(2, dayTwoSlot.EpisodeNumber);
        Assert.Null(dayTwoSlot.EpisodeNumberEnd);
    }
}
