using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Metadata;
using AnimeTracker.Api.Services.Relations;
using AnimeTracker.Api.Services.Series;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimeTracker.Api.Tests.Services.Series;

// key-series-by-root-anime-id design.md D1-D6, tasks 6.2-6.6: Series.Id is
// now the root anime id itself rather than an independent counter, so a
// build that discovers an earlier main-line prequel must move the series'
// identity, not just its RootAnimeId column. Every test here uses CreateDb's
// InMemoryEventId.TransactionIgnoredWarning suppression because the re-root
// path (PersistReRootedAsync) is the one place SeriesGraphBuilder opens an
// explicit transaction (design.md D4) — InMemory otherwise rejects it.
public class SeriesGraphBuilderReRootTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static AnimeMetadata Anime(int id, string mediaType, DateOnly? airedFrom = null) => new()
    {
        Id = id,
        Title = $"Anime {id}",
        MediaType = mediaType,
        AiredFrom = airedFrom,
    };

    private static void Relate(AnimeMetadata from, AnimeMetadata to, string relationType) =>
        from.RelatedAnime.Add(new AnimeRelatedAnime
        {
            AnimeId = from.Id,
            RelatedAnimeId = to.Id,
            RelationType = relationType,
            Title = to.Title,
        });

    private static SeriesGraphBuilder CreateBuilder(AnimeTrackerDbContext db) =>
        new(db, new NoOpRefreshService(), new RelationResolver(db), NullLogger<SeriesGraphBuilder>.Instance);

    // --- 6.2: a first build keys the series on the root, not an arbitrary id ---

    [Fact]
    public async Task FirstBuildKeysTheSeriesOnTheEarliestMainLineMembersAnimeId()
    {
        using var db = CreateDb();
        // The movie is the earlier-in-story prequel despite airing later and
        // holding the higher MAL id — mirrors the live Jujutsu Kaisen case
        // (SeriesGraphBuilderTests.ChainEdgeOverridesAirDateOrdering) — so a
        // series keyed on "the root" rather than "the lowest id" or "the
        // seed anime" must land on the movie's id.
        var prequelMovie = Anime(48561, "movie", new DateOnly(2021, 12, 24));
        var tvSeason = Anime(40748, "tv", new DateOnly(2020, 10, 3));
        Relate(prequelMovie, tvSeason, "sequel");
        Relate(tvSeason, prequelMovie, "prequel");
        db.AnimeMetadata.AddRange(prequelMovie, tvSeason);
        await db.SaveChangesAsync();

        var series = await CreateBuilder(db).BuildAsync(tvSeason.Id, fetchBudget: 0, probeBudget: 0, expandLeanMembers: false);
        var members = await db.SeriesMembers.Where(m => m.SeriesId == series!.Id).ToListAsync();

        Assert.Equal(prequelMovie.Id, series!.Id);
        Assert.Equal(2, members.Count);
        Assert.All(members, m => Assert.Equal(prequelMovie.Id, m.SeriesId));
    }

    // --- 6.3: a stored series' root moving forward re-keys it (design.md D3 case 3, D4) ---

    [Fact]
    public async Task StoredSeriesGainingAnEarlierMainLineEntryReRootsAndKeepsItsChoices()
    {
        using var db = CreateDb();
        var b = Anime(5, "tv", new DateOnly(2015, 1, 1));
        var c = Anime(6, "tv", new DateOnly(2016, 1, 1));
        Relate(b, c, "sequel");
        db.AnimeMetadata.AddRange(b, c);
        await db.SaveChangesAsync();

        var firstBuild = await CreateBuilder(db).BuildAsync(b.Id, fetchBudget: 0, probeBudget: 0, expandLeanMembers: false);
        Assert.Equal(b.Id, firstBuild!.Id); // b is the root before the prequel is known

        var stored = await db.Series.FirstAsync(s => s.Id == firstBuild.Id);
        stored.SelectedTitle = "Chosen Title";
        stored.SelectedPictureUrl = "chosen.jpg";
        await db.SaveChangesAsync();

        // MAL reveals an older prequel — the same kind of discovery a
        // relation refresh can surface on any rebuild.
        var a = Anime(3, "tv", new DateOnly(2010, 1, 1));
        db.AnimeMetadata.Add(a);
        Relate(a, b, "sequel");
        await db.SaveChangesAsync();

        var rebuilt = await CreateBuilder(db).BuildAsync(b.Id, fetchBudget: 0, probeBudget: 0, expandLeanMembers: false);

        Assert.Equal(a.Id, rebuilt!.Id); // the series moved to the new root's id

        var allSeries = await db.Series.AsNoTracking().ToListAsync();
        var survivor = Assert.Single(allSeries); // nothing left under b's old id
        Assert.Equal(a.Id, survivor.Id);
        Assert.Equal("Chosen Title", survivor.SelectedTitle);
        Assert.Equal("chosen.jpg", survivor.SelectedPictureUrl);

        var members = await db.SeriesMembers.Where(m => m.SeriesId == a.Id).ToListAsync();
        Assert.Equal(
            new[] { a.Id, b.Id, c.Id }.OrderBy(x => x),
            members.Select(m => m.AnimeId).OrderBy(x => x));
    }

    // --- 6.4: design.md D5 — the id a re-root needs is always free by the time it's taken,
    // even when a stale (absorbed) series, not the matched survivor, is the one holding it ---

    [Fact]
    public async Task ReRootWhoseFreedIdBelongsToAnAbsorbedSeriesDoesNotThrow()
    {
        using var db = CreateDb();
        var root = Anime(10, "tv", new DateOnly(2010, 1, 1));
        var mid = Anime(20, "tv", new DateOnly(2015, 1, 1));
        var tail1 = Anime(30, "tv", new DateOnly(2016, 1, 1));
        var tail2 = Anime(40, "tv", new DateOnly(2017, 1, 1));
        Relate(mid, tail1, "sequel");
        Relate(tail1, tail2, "sequel");
        db.AnimeMetadata.AddRange(root, mid, tail1, tail2);

        // The larger, surviving series (currently rooted at mid, three
        // members) and a smaller, unrelated-so-far series that happens to
        // already own the anime id (root's) that this rebuild's true root
        // will need to claim.
        db.Series.Add(new AnimeTracker.Api.Models.Series { Id = mid.Id, BuiltAt = DateTimeOffset.UtcNow, SelectedTitle = "Survivor's Own" });
        db.SeriesMembers.AddRange(
            new SeriesMember { SeriesId = mid.Id, AnimeId = mid.Id, IsMainLine = true, Order = 0 },
            new SeriesMember { SeriesId = mid.Id, AnimeId = tail1.Id, IsMainLine = true, Order = 1 },
            new SeriesMember { SeriesId = mid.Id, AnimeId = tail2.Id, IsMainLine = true, Order = 2 });

        db.Series.Add(new AnimeTracker.Api.Models.Series { Id = root.Id, BuiltAt = DateTimeOffset.UtcNow, SelectedTitle = "Stale Choice" });
        db.SeriesMembers.Add(new SeriesMember { SeriesId = root.Id, AnimeId = root.Id, IsMainLine = true, Order = 0 });
        await db.SaveChangesAsync();

        // MAL reveals the relation that merges the two: root is mid's prequel.
        Relate(root, mid, "sequel");
        await db.SaveChangesAsync();

        var result = await CreateBuilder(db).BuildAsync(mid.Id, fetchBudget: 0, probeBudget: 0, expandLeanMembers: false);

        Assert.Equal(root.Id, result!.Id); // the new root's id, freed by deleting the stale series that held it

        var allSeries = await db.Series.AsNoTracking().ToListAsync();
        var survivor = Assert.Single(allSeries);
        Assert.Equal(root.Id, survivor.Id);
        Assert.Equal("Survivor's Own", survivor.SelectedTitle); // the larger series' own choice, not the absorbed one's

        var members = await db.SeriesMembers.Where(m => m.SeriesId == root.Id).ToListAsync();
        Assert.Equal(
            new[] { root.Id, mid.Id, tail1.Id, tail2.Id }.OrderBy(x => x),
            members.Select(m => m.AnimeId).OrderBy(x => x));
    }

    // --- 6.5: a rebuild with no membership change doesn't shuffle identity ---

    [Fact]
    public async Task RebuildWithNoMembershipChangeKeepsIdAndChoicesButRefreshesBuiltAt()
    {
        using var db = CreateDb();
        var a = Anime(1, "tv", new DateOnly(2018, 1, 1));
        var b = Anime(2, "tv", new DateOnly(2019, 1, 1));
        Relate(a, b, "sequel");
        db.AnimeMetadata.AddRange(a, b);
        await db.SaveChangesAsync();

        var first = await CreateBuilder(db).BuildAsync(a.Id, fetchBudget: 0, probeBudget: 0, expandLeanMembers: false);
        var stored = await db.Series.FirstAsync(s => s.Id == first!.Id);
        stored.SelectedTitle = "Kept Title";
        stored.SelectedPictureUrl = "kept.jpg";
        var firstBuiltAt = stored.BuiltAt;
        await db.SaveChangesAsync();

        var second = await CreateBuilder(db).BuildAsync(a.Id, fetchBudget: 0, probeBudget: 0, expandLeanMembers: false);

        Assert.Equal(a.Id, second!.Id); // same derived id — no re-root, no shuffle

        var rebuilt = await db.Series.AsNoTracking().SingleAsync();
        Assert.Equal(a.Id, rebuilt.Id);
        Assert.Equal("Kept Title", rebuilt.SelectedTitle);
        Assert.Equal("kept.jpg", rebuilt.SelectedPictureUrl);
        Assert.True(rebuilt.BuiltAt >= firstBuiltAt); // still refreshed in place, same row throughout
    }

    // --- 6.6: design.md D6/D8, task 3.8 — the folded-primary tie-break now
    // reads competingFold.SeriesId directly (it *is* the root id) instead of
    // looking up a separate RootAnimeId column; the outcome is unchanged:
    // equal core counts still resolve to the lower root id ---

    [Fact]
    public async Task EqualSizedFoldsCompetingForAFoldedVersionResolveToTheLowerRootId()
    {
        using var db = CreateDb();
        var sharedFold = Anime(999, "tv"); // no story relations of its own

        var rootLow = Anime(100, "tv");
        var seqLow = Anime(101, "tv");
        Relate(rootLow, seqLow, "sequel");
        Relate(sharedFold, rootLow, "alternative_setting");

        var rootHigh = Anime(200, "tv");
        var seqHigh = Anime(201, "tv");
        Relate(rootHigh, seqHigh, "sequel");
        Relate(sharedFold, rootHigh, "alternative_version");

        db.AnimeMetadata.AddRange(sharedFold, rootLow, seqLow, rootHigh, seqHigh);
        await db.SaveChangesAsync();

        // Built high-root-first — the order that would win the tie if it
        // were "whoever built most recently" rather than "the lower root id".
        var highSeries = await CreateBuilder(db).BuildAsync(rootHigh.Id, fetchBudget: 0, probeBudget: 0, expandLeanMembers: false);
        var lowSeries = await CreateBuilder(db).BuildAsync(rootLow.Id, fetchBudget: 0, probeBudget: 0, expandLeanMembers: false);

        Assert.Equal(rootHigh.Id, highSeries!.Id);
        Assert.Equal(rootLow.Id, lowSeries!.Id);

        var foldRows = await db.SeriesMembers.AsNoTracking().Where(m => m.AnimeId == sharedFold.Id).ToListAsync();
        Assert.Equal(2, foldRows.Count);
        var primary = Assert.Single(foldRows, m => m.IsPrimary);
        Assert.Equal(rootLow.Id, primary.SeriesId); // equal core counts (2 vs 2): lower root id wins
    }

    private sealed class NoOpRefreshService : IMetadataRefreshService
    {
        public Task<int> RefreshStaleBatchAsync(int batchSize, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task RefreshOneAsync(int animeId, CancellationToken ct = default) =>
            throw new AnimeMetadataNotFoundException(animeId);
    }
}
