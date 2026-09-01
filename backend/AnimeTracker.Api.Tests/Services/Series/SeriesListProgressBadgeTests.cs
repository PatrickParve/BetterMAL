using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Ranking;
using AnimeTracker.Api.Services.Series;
using Microsoft.EntityFrameworkCore;
using SeriesModel = AnimeTracker.Api.Models.Series;

namespace AnimeTracker.Api.Tests.Services.Series;

// The Series page card's progress badge (polish-series-badges-and-filters
// design.md D1, spec "A card's progress badge follows the series page's
// precedence") — mirrors SeriesPage.tsx's completionBadge exactly, over the
// same case set.
public class SeriesListProgressBadgeTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static void AddAnime(AnimeTrackerDbContext db, int id, string airingStatus, int? totalEpisodes = null) =>
        db.AnimeMetadata.Add(new AnimeMetadata { Id = id, Title = $"Anime {id}", AiringStatus = airingStatus, TotalEpisodes = totalEpisodes });

    private static void AddMember(AnimeTrackerDbContext db, int seriesId, int animeId, int order) =>
        db.SeriesMembers.Add(new SeriesMember { AnimeId = animeId, SeriesId = seriesId, IsMainLine = true, Order = order });

    private static void AddEntry(AnimeTrackerDbContext db, int animeId, WatchStatus status, int episodesWatched = 0) =>
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = animeId, Status = status, EpisodesWatched = episodesWatched });

    [Fact]
    public async Task EveryMemberFinishedAndCompleted_ReadsCompleted()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 1, RootAnimeId = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100, "finished_airing", totalEpisodes: 12);
        AddMember(db, 1, 100, order: 0);
        AddEntry(db, 100, WatchStatus.Completed, episodesWatched: 12);
        await db.SaveChangesAsync();

        var index = await new SeriesRankingLookup(db).LoadAsync();
        var listed = Assert.Single(index.ListedSeries([], AnimeRankingSnapshot.Empty));

        Assert.Equal(SeriesProgressBadge.Completed, listed.ProgressBadge);
        Assert.Null(listed.BehindEpisodes);
    }

    [Fact]
    public async Task CaughtUpOnAnAiringSeason_ReadsCaughtUp()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 1, RootAnimeId = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100, "finished_airing", totalEpisodes: 12);
        AddAnime(db, 101, "currently_airing");
        AddMember(db, 1, 100, order: 0);
        AddMember(db, 1, 101, order: 1);
        AddEntry(db, 100, WatchStatus.Completed, episodesWatched: 12);
        AddEntry(db, 101, WatchStatus.Watching, episodesWatched: 5);
        await db.SaveChangesAsync();

        var index = await new SeriesRankingLookup(db).LoadAsync();
        var listed = Assert.Single(index.ListedSeries(new Dictionary<int, int> { [101] = 5 }, AnimeRankingSnapshot.Empty));

        Assert.Equal(SeriesProgressBadge.CaughtUp, listed.ProgressBadge);
        Assert.Null(listed.BehindEpisodes);
    }

    [Fact]
    public async Task BehindOnAnAiringSeason_ReadsBehindWithTheUnwatchedCount()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 1, RootAnimeId = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100, "finished_airing", totalEpisodes: 12);
        AddAnime(db, 101, "currently_airing");
        AddMember(db, 1, 100, order: 0);
        AddMember(db, 1, 101, order: 1);
        AddEntry(db, 100, WatchStatus.Completed, episodesWatched: 12);
        AddEntry(db, 101, WatchStatus.Watching, episodesWatched: 5);
        await db.SaveChangesAsync();

        var index = await new SeriesRankingLookup(db).LoadAsync();
        var listed = Assert.Single(index.ListedSeries(new Dictionary<int, int> { [101] = 8 }, AnimeRankingSnapshot.Empty));

        Assert.Equal(SeriesProgressBadge.Behind, listed.ProgressBadge);
        Assert.Equal(3, listed.BehindEpisodes);
    }

    [Fact]
    public async Task AiringSeasonNotInMyListAtAll_CountsAsZeroWatched()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 1, RootAnimeId = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100, "finished_airing", totalEpisodes: 12);
        AddAnime(db, 101, "currently_airing"); // not in my list at all
        AddMember(db, 1, 100, order: 0);
        AddMember(db, 1, 101, order: 1);
        AddEntry(db, 100, WatchStatus.Completed, episodesWatched: 12);
        await db.SaveChangesAsync();

        var index = await new SeriesRankingLookup(db).LoadAsync();
        var listed = Assert.Single(index.ListedSeries(new Dictionary<int, int> { [101] = 8 }, AnimeRankingSnapshot.Empty));

        Assert.Equal(SeriesProgressBadge.Behind, listed.ProgressBadge);
        Assert.Equal(8, listed.BehindEpisodes);
    }

    [Fact]
    public async Task CaughtUpWhileNextEntryUnaired_ReadsCaughtUp()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 1, RootAnimeId = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100, "finished_airing", totalEpisodes: 12);
        AddAnime(db, 101, "not_yet_aired");
        AddMember(db, 1, 100, order: 0);
        AddMember(db, 1, 101, order: 1);
        AddEntry(db, 100, WatchStatus.Completed, episodesWatched: 12);
        await db.SaveChangesAsync();

        var index = await new SeriesRankingLookup(db).LoadAsync();
        var listed = Assert.Single(index.ListedSeries([], AnimeRankingSnapshot.Empty));

        Assert.Equal(SeriesProgressBadge.CaughtUp, listed.ProgressBadge);
    }

    // design.md D1 rule 4: no longer limited to a currently-airing entry —
    // a finished entry watched partway through, never completed or
    // dropped, now reads as a behind count instead of silence.
    [Fact]
    public async Task PartiallyWatchedFinishedEntry_ReadsBehindNotNoBadge()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 1, RootAnimeId = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100, "finished_airing", totalEpisodes: 12);
        AddMember(db, 1, 100, order: 0);
        AddEntry(db, 100, WatchStatus.Watching, episodesWatched: 5); // never marked Completed
        await db.SaveChangesAsync();

        var index = await new SeriesRankingLookup(db).LoadAsync();
        var listed = Assert.Single(index.ListedSeries([], AnimeRankingSnapshot.Empty));

        Assert.Equal(SeriesProgressBadge.Behind, listed.ProgressBadge);
        Assert.Equal(7, listed.BehindEpisodes);
    }

    // design.md D2: the most recently aired Dropped entry, with nothing
    // aired after it ever watched.
    [Fact]
    public async Task DroppedWithNothingWatchedAfter_ReadsDropped()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 1, RootAnimeId = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100, "finished_airing", totalEpisodes: 12);
        AddAnime(db, 101, "finished_airing", totalEpisodes: 12);
        AddMember(db, 1, 100, order: 0);
        AddMember(db, 1, 101, order: 1);
        AddEntry(db, 100, WatchStatus.Dropped, episodesWatched: 3);
        AddEntry(db, 101, WatchStatus.PlanToWatch); // aired after the drop, never watched
        await db.SaveChangesAsync();

        var index = await new SeriesRankingLookup(db).LoadAsync();
        var listed = Assert.Single(index.ListedSeries([], AnimeRankingSnapshot.Empty));

        Assert.Equal(SeriesProgressBadge.Dropped, listed.ProgressBadge);
        Assert.Null(listed.BehindEpisodes);
    }

    // A dropped entry as the only (and therefore last) aired main-line
    // entry: "nothing aired after it" is vacuously true.
    [Fact]
    public async Task DroppedAsTheOnlyAiredEntry_ReadsDropped()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 1, RootAnimeId = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100, "finished_airing", totalEpisodes: 12);
        AddMember(db, 1, 100, order: 0);
        AddEntry(db, 100, WatchStatus.Dropped, episodesWatched: 0);
        await db.SaveChangesAsync();

        var index = await new SeriesRankingLookup(db).LoadAsync();
        var listed = Assert.Single(index.ListedSeries([], AnimeRankingSnapshot.Empty));

        Assert.Equal(SeriesProgressBadge.Dropped, listed.ProgressBadge);
    }

    // design.md D2: a drop later resumed and watched past no longer counts
    // as Dropped — it falls through to the ordinary behind/caught-up figures.
    [Fact]
    public async Task DroppedEntryLaterResumed_DoesNotReadDropped()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 1, RootAnimeId = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100, "finished_airing", totalEpisodes: 12);
        AddAnime(db, 101, "finished_airing", totalEpisodes: 12);
        AddMember(db, 1, 100, order: 0);
        AddMember(db, 1, 101, order: 1);
        AddEntry(db, 100, WatchStatus.Dropped, episodesWatched: 3);
        AddEntry(db, 101, WatchStatus.Watching, episodesWatched: 6); // resumed on the next entry
        await db.SaveChangesAsync();

        var index = await new SeriesRankingLookup(db).LoadAsync();
        var listed = Assert.Single(index.ListedSeries([], AnimeRankingSnapshot.Empty));

        Assert.NotEqual(SeriesProgressBadge.Dropped, listed.ProgressBadge);
        Assert.Equal(SeriesProgressBadge.Behind, listed.ProgressBadge);
        Assert.Equal(15, listed.BehindEpisodes); // (12 + 12) aired - (3 + 6) watched
    }

    // design.md D3: nothing watched at all, and no drop to explain it —
    // reads as Unwatched, not silence.
    [Fact]
    public async Task NothingWatchedAtAllWithNoDrop_ReadsUnwatched()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 1, RootAnimeId = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100, "finished_airing", totalEpisodes: 12);
        AddMember(db, 1, 100, order: 0);
        AddEntry(db, 100, WatchStatus.PlanToWatch); // finished airing, not completed, nothing watched
        await db.SaveChangesAsync();

        var index = await new SeriesRankingLookup(db).LoadAsync();
        var listed = Assert.Single(index.ListedSeries([], AnimeRankingSnapshot.Empty));

        Assert.Equal(SeriesProgressBadge.Unwatched, listed.ProgressBadge);
    }

    // design.md D3: Dropped wins over Unwatched when both technically apply
    // (a drop with literally zero episodes watched).
    [Fact]
    public async Task DroppedWithZeroWatched_ReadsDroppedNotUnwatched()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 1, RootAnimeId = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100, "finished_airing", totalEpisodes: 12);
        AddMember(db, 1, 100, order: 0);
        AddEntry(db, 100, WatchStatus.Dropped, episodesWatched: 0);
        await db.SaveChangesAsync();

        var index = await new SeriesRankingLookup(db).LoadAsync();
        var listed = Assert.Single(index.ListedSeries([], AnimeRankingSnapshot.Empty));

        Assert.Equal(SeriesProgressBadge.Dropped, listed.ProgressBadge);
    }

    [Fact]
    public async Task NothingAiredAtAll_ReadsNoBadge()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 1, RootAnimeId = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100, "not_yet_aired");
        AddMember(db, 1, 100, order: 0);
        AddEntry(db, 100, WatchStatus.PlanToWatch);
        await db.SaveChangesAsync();

        var index = await new SeriesRankingLookup(db).LoadAsync();
        var listed = Assert.Single(index.ListedSeries([], AnimeRankingSnapshot.Empty));

        Assert.Equal(SeriesProgressBadge.None, listed.ProgressBadge);
    }

    // design.md D4: the unknown-broadcast-count guard only blocks the
    // Caught-up/Behind computation, reached only once watchedTotal > 0 and
    // no qualifying drop applies.
    [Fact]
    public async Task CurrentlyAiringEntryWithNoStoredAiredCount_ReadsNoBadge()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 1, RootAnimeId = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100, "currently_airing");
        AddMember(db, 1, 100, order: 0);
        AddEntry(db, 100, WatchStatus.Watching, episodesWatched: 3);
        await db.SaveChangesAsync();

        var index = await new SeriesRankingLookup(db).LoadAsync();
        // 100 is absent from the aired-episodes dictionary: unknown.
        var listed = Assert.Single(index.ListedSeries([], AnimeRankingSnapshot.Empty));

        Assert.Equal(SeriesProgressBadge.None, listed.ProgressBadge);
    }

    // design.md D4: an unknown broadcast count never blocks Dropped or
    // Unwatched, since neither needs it.
    [Fact]
    public async Task UnknownBroadcastCount_StillReadsDroppedWhenApplicable()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 1, RootAnimeId = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100, "finished_airing", totalEpisodes: 12);
        AddAnime(db, 101, "currently_airing"); // unknown broadcast count
        AddMember(db, 1, 100, order: 0);
        AddMember(db, 1, 101, order: 1);
        AddEntry(db, 100, WatchStatus.Dropped, episodesWatched: 0);
        await db.SaveChangesAsync();

        var index = await new SeriesRankingLookup(db).LoadAsync();
        // 101 absent from the aired-episodes dictionary, but the drop on
        // 100 with nothing watched after it (101 has no entry at all)
        // decides this before the unknown count would ever matter.
        var listed = Assert.Single(index.ListedSeries([], AnimeRankingSnapshot.Empty));

        Assert.Equal(SeriesProgressBadge.Dropped, listed.ProgressBadge);
    }

    // --- A Rewatching entry counts as fully watched (polish-rewatch design.md D2, tasks.md 3.7) ---

    [Fact]
    public async Task RewatchInProgress_DoesNotReadAsBehind()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 1, RootAnimeId = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100, "finished_airing", totalEpisodes: 12);
        AddAnime(db, 101, "finished_airing", totalEpisodes: 12);
        AddAnime(db, 102, "finished_airing", totalEpisodes: 12);
        // A 4th, not-yet-aired member keeps the series' overall status off
        // "Finished", so this exercises the Caught-up/Behind computation
        // rather than shortcutting through rule (1)'s Completed clause.
        AddAnime(db, 103, "not_yet_aired");
        AddMember(db, 1, 100, order: 0);
        AddMember(db, 1, 101, order: 1);
        AddMember(db, 1, 102, order: 2);
        AddMember(db, 1, 103, order: 3);
        AddEntry(db, 100, WatchStatus.Rewatching, episodesWatched: 2); // resets to 2, but full run has aired
        AddEntry(db, 101, WatchStatus.Completed, episodesWatched: 12);
        AddEntry(db, 102, WatchStatus.Completed, episodesWatched: 12);
        await db.SaveChangesAsync();

        var index = await new SeriesRankingLookup(db).LoadAsync();
        var listed = Assert.Single(index.ListedSeries([], AnimeRankingSnapshot.Empty));

        Assert.NotEqual(SeriesProgressBadge.Behind, listed.ProgressBadge);
        Assert.Equal(SeriesProgressBadge.CaughtUp, listed.ProgressBadge);
    }

    [Fact]
    public async Task RewatchInProgress_DoesNotReadAsUnwatched()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 1, RootAnimeId = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100, "finished_airing", totalEpisodes: 12);
        // A 2nd, not-yet-aired member keeps the series' overall status off
        // "Finished", so this exercises the Unwatched check itself rather
        // than shortcutting through rule (1)'s Completed clause.
        AddAnime(db, 101, "not_yet_aired");
        AddMember(db, 1, 100, order: 0);
        AddMember(db, 1, 101, order: 1);
        AddEntry(db, 100, WatchStatus.Rewatching, episodesWatched: 0); // just started the rewatch
        await db.SaveChangesAsync();

        var index = await new SeriesRankingLookup(db).LoadAsync();
        var listed = Assert.Single(index.ListedSeries([], AnimeRankingSnapshot.Empty));

        Assert.NotEqual(SeriesProgressBadge.Unwatched, listed.ProgressBadge);
        Assert.Equal(SeriesProgressBadge.CaughtUp, listed.ProgressBadge);
    }

    [Fact]
    public async Task RewatchAfterADrop_CountsAsWatchingPastIt()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 1, RootAnimeId = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100, "finished_airing", totalEpisodes: 12);
        AddAnime(db, 101, "finished_airing", totalEpisodes: 12);
        AddMember(db, 1, 100, order: 0);
        AddMember(db, 1, 101, order: 1);
        AddEntry(db, 100, WatchStatus.Dropped, episodesWatched: 3);
        AddEntry(db, 101, WatchStatus.Rewatching, episodesWatched: 0); // reset, but has aired in full
        await db.SaveChangesAsync();

        var index = await new SeriesRankingLookup(db).LoadAsync();
        var listed = Assert.Single(index.ListedSeries([], AnimeRankingSnapshot.Empty));

        Assert.NotEqual(SeriesProgressBadge.Dropped, listed.ProgressBadge);
        Assert.Equal(SeriesProgressBadge.Behind, listed.ProgressBadge);
        Assert.Equal(9, listed.BehindEpisodes); // (12 + 12) aired - (3 + 12) effectively watched
    }

    [Fact]
    public async Task FinishedFranchiseBeingRewatched_KeepsCompleted()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 1, RootAnimeId = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100, "finished_airing", totalEpisodes: 12);
        AddMember(db, 1, 100, order: 0);
        AddEntry(db, 100, WatchStatus.Rewatching, episodesWatched: 2);
        await db.SaveChangesAsync();

        var index = await new SeriesRankingLookup(db).LoadAsync();
        var listed = Assert.Single(index.ListedSeries([], AnimeRankingSnapshot.Empty));

        Assert.Equal(SeriesProgressBadge.Completed, listed.ProgressBadge);
    }
}
