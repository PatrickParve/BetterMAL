using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Setup;

namespace AnimeTracker.Api.Tests.Services.Setup;

// add-first-run-setup task 8.7 (design D14; spec "Home opens when the library is complete"): each
// of the four conditions holds Home when it is unmet, and only the first three are MyAnimeList's.
// The kit's clock is 2026-09-29, in summer 2026, so a currently airing anime is in the airing
// priority set and a long-finished one is not.
public class SetupHomeCheckTests
{
    private static (SetupHomeCheck Check, RetryLadder AniListLadder) Create(SetupRunKit kit)
    {
        var ladder = new RetryLadder();
        return (new SetupHomeCheck(kit.Context, ladder, kit.AniListHealth), ladder);
    }

    /// <summary>A library where all four conditions hold: the list has been read, and anime 1 is
    /// fetched, covered and marked.</summary>
    private static async Task<SetupRunKit> CompleteLibraryAsync()
    {
        var kit = new SetupRunKit();
        await kit.SeedAnimeAsync(1, fetched: true, marked: true, airing: "currently_airing");
        await kit.SeedSeriesAsync(10, 1);
        kit.State.MarkListRead();
        return kit;
    }

    [Fact]
    public async Task ACompleteLibraryHoldsAllFourConditions()
    {
        using var kit = await CompleteLibraryAsync();

        Assert.True(await Create(kit).Check.HoldsAsync(default));
    }

    [Fact]
    public async Task TheListNotYetReadInThisRunHoldsHomeWhateverTheDatabaseSays()
    {
        using var kit = new SetupRunKit();
        await kit.SeedAnimeAsync(1, fetched: true, marked: true);
        await kit.SeedSeriesAsync(10, 1);
        var (check, _) = Create(kit); // a restart: everything stored, but the list is read again first

        Assert.False(await check.HoldsAsync(default));

        kit.State.MarkListRead();
        Assert.True(await check.HoldsAsync(default));
    }

    [Fact]
    public async Task AnEmptyListFinishesRightAfterItIsRead()
    {
        using var kit = new SetupRunKit();
        var (check, _) = Create(kit);
        Assert.False(await check.HoldsAsync(default));

        kit.State.MarkListRead();

        Assert.True(await check.HoldsAsync(default));
    }

    [Fact]
    public async Task AnAnimeWithDetailsStillToFetchHoldsHome()
    {
        using var kit = await CompleteLibraryAsync();
        await kit.SeedAnimeAsync(2, fetched: false);
        await kit.SeedSeriesAsync(20, 2);
        var (check, _) = Create(kit);

        Assert.False(await check.HoldsAsync(default));

        await using var db = kit.NewDb();
        (await db.AnimeMetadata.FindAsync(2))!.LastSyncedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        Assert.True(await check.HoldsAsync(default));
    }

    [Fact]
    public async Task AnAnimeMalHasNoDetailsForIsSkippedForGoodAndDoesNotHoldHome()
    {
        using var kit = await CompleteLibraryAsync();
        await kit.SeedAnimeAsync(2, notOnMal: true); // a 404: no details, and no series to build

        Assert.True(await Create(kit).Check.HoldsAsync(default));
    }

    [Fact]
    public async Task AnAnimeWithNoSeriesYetHoldsHomeUntilItIsCoveredOrFoundToBeAlone()
    {
        using var kit = await CompleteLibraryAsync();
        await kit.SeedAnimeAsync(2, fetched: true, marked: true);
        var (check, _) = Create(kit);

        Assert.False(await check.HoldsAsync(default)); // not covered by any series

        kit.State.AddNoSeries(2); // its build found it belongs to none
        Assert.True(await check.HoldsAsync(default));
    }

    [Fact]
    public async Task APartialOrOutOfDateSeriesHoldsHome()
    {
        using var kit = new SetupRunKit();
        await kit.SeedAnimeAsync(1, fetched: true, marked: true);
        await kit.SeedAnimeAsync(2, fetched: true, marked: true);
        await using (var db = kit.NewDb())
        {
            db.Series.Add(new Models.Series { Id = 10, BuiltAt = DateTimeOffset.UtcNow, IsPartial = true });
            db.SeriesMembers.Add(new SeriesMember { SeriesId = 10, AnimeId = 1, IsPrimary = true });
            await db.SaveChangesAsync();
        }
        await kit.SeedSeriesAsync(20, 2);
        kit.State.MarkListRead();
        var (check, _) = Create(kit);

        Assert.False(await check.HoldsAsync(default)); // anime 1's series is partial

        await using var db2 = kit.NewDb();
        db2.Series.Single(s => s.Id == 10).IsPartial = false;
        await db2.SaveChangesAsync();
        Assert.True(await check.HoldsAsync(default));
    }

    [Fact]
    public async Task APriorityAnimeWithNoAiringMarkHoldsHome()
    {
        using var kit = await CompleteLibraryAsync();
        await kit.SeedAnimeAsync(2, fetched: true, marked: false, airing: "currently_airing");
        await kit.SeedSeriesAsync(20, 2);
        var (check, _) = Create(kit);

        Assert.False(await check.HoldsAsync(default));

        await using var db = kit.NewDb();
        db.AnimeAiringSyncs.Add(new AnimeAiringSync { AnimeId = 2, LastFetchedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        Assert.True(await check.HoldsAsync(default));
    }

    [Fact]
    public async Task AnAnimeOutsideThePrioritySetNeverHoldsHome()
    {
        using var kit = await CompleteLibraryAsync();
        await kit.SeedAnimeAsync(2, fetched: true, marked: false, airing: "finished_airing", airedFrom: new DateOnly(2015, 1, 1));
        await kit.SeedSeriesAsync(20, 2);

        Assert.True(await Create(kit).Check.HoldsAsync(default)); // its airing dates come in the background
    }

    [Fact]
    public async Task AnAnimeWaitingOnAnAniListRetryDoesNotHoldHome()
    {
        using var kit = await CompleteLibraryAsync();
        await kit.SeedAnimeAsync(2, fetched: true, marked: false, airing: "currently_airing");
        await kit.SeedSeriesAsync(20, 2);
        var (check, ladder) = Create(kit);
        Assert.False(await check.HoldsAsync(default));

        ladder.RecordFailure(2, kit.Now); // AniList failed for it, and it is waiting for its retry

        Assert.True(await check.HoldsAsync(default));
    }

    [Fact]
    public async Task AniListBeingDownDoesNotHoldHome()
    {
        using var kit = await CompleteLibraryAsync();
        await kit.SeedAnimeAsync(2, fetched: true, marked: false, airing: "currently_airing");
        await kit.SeedSeriesAsync(20, 2);
        var (check, _) = Create(kit);
        for (var i = 0; i < ServiceHealth.DownAfterFailures; i++)
            kit.AniListHealth.RecordTemporaryFailure();

        Assert.True(await check.HoldsAsync(default));
    }

    [Fact]
    public async Task MalBeingDownHoldsHomeForTheAnimeItStillOwes()
    {
        using var kit = await CompleteLibraryAsync();
        await kit.SeedAnimeAsync(2, fetched: false, marked: true);
        await kit.SeedSeriesAsync(20, 2);
        for (var i = 0; i < ServiceHealth.DownAfterFailures; i++)
            kit.MalHealth.RecordTemporaryFailure();
        var (check, _) = Create(kit);

        // Home waits for MyAnimeList: there is no way past its outage, unlike AniList's.
        Assert.False(await check.HoldsAsync(default));
    }
}
