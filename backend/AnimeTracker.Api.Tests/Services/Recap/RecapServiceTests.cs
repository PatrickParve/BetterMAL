using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Recap;

namespace AnimeTracker.Api.Tests.Services.Recap;

// RecapService.ToRow's per-row MalRevealed flag (tasks.md 3.1): widened
// alongside score-visibility's always-show setting to also cover Dropped
// entries, not just Completed ones.
public class RecapServiceTests
{
    private static UserAnimeEntry Entry(int animeId, WatchStatus status, DateOnly completedAt, double malScore) =>
        new()
        {
            AnimeId = animeId,
            Anime = new AnimeMetadata { Id = animeId, Title = $"Anime {animeId}", MalScore = malScore },
            Status = status,
            CompletedAt = completedAt,
        };

    [Fact]
    public async Task DroppedEntryRowIsRevealed()
    {
        var entries = new List<UserAnimeEntry>
        {
            Entry(1, WatchStatus.Dropped, new DateOnly(2021, 6, 1), 7.5),
        };
        var service = new RecapService(new FakeUserAnimeEntryRepository(entries));

        var dto = await service.GetRecapAsync(RecapPeriod.Yearly(2021), RecapTimeFilter.Watched, CancellationToken.None);

        var row = Assert.Single(dto.Items);
        Assert.True(row.MalRevealed);
    }

    [Fact]
    public async Task WatchingEntryRowIsNotRevealed()
    {
        // The "aired" filter (unlike "watched") includes Watching entries
        // (RecapEntrySelector.AiredStatuses), so this isolates the reveal
        // flag itself from the filter's own status gate.
        var entry = Entry(1, WatchStatus.Watching, new DateOnly(2021, 6, 1), 7.5);
        entry.Anime.AiredFrom = new DateOnly(2021, 6, 1);
        var service = new RecapService(new FakeUserAnimeEntryRepository([entry]));

        var dto = await service.GetRecapAsync(RecapPeriod.Yearly(2021), RecapTimeFilter.Aired, CancellationToken.None);

        var row = Assert.Single(dto.Items);
        Assert.False(row.MalRevealed);
    }

    [Fact]
    public async Task MultiYearCurrentlyWatchingSpansEveryYearOfTheRange()
    {
        var early = Entry(1, WatchStatus.Watching, new DateOnly(2020, 3, 1), 7.5);
        early.Anime.AiredFrom = new DateOnly(2020, 3, 1);
        var late = Entry(2, WatchStatus.Watching, new DateOnly(2024, 9, 1), 8.0);
        late.Anime.AiredFrom = new DateOnly(2024, 9, 1);
        var service = new RecapService(new FakeUserAnimeEntryRepository([early, late]));

        var dto = await service.GetRecapAsync(RecapPeriod.MultiYear(2020, 2024), RecapTimeFilter.Aired, CancellationToken.None);

        Assert.Equal(2, dto.Stats.CurrentlyWatching);
    }

    private sealed class FakeUserAnimeEntryRepository(List<UserAnimeEntry> entries) : IUserAnimeEntryRepository
    {
        public Task<UserAnimeEntry?> GetByAnimeIdAsync(int animeId, CancellationToken ct = default) =>
            Task.FromResult(entries.FirstOrDefault(e => e.AnimeId == animeId));

        public Task<List<UserAnimeEntry>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(entries);

        public Task<(int PendingCount, DateTimeOffset? LastSyncedAt)> GetSyncStatusAsync(CancellationToken ct = default) =>
            Task.FromResult((0, (DateTimeOffset?)null));
    }
}
