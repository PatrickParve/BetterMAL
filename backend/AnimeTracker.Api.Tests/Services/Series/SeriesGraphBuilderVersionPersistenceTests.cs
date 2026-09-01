using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Metadata;
using AnimeTracker.Api.Services.Relations;
using AnimeTracker.Api.Services.Series;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SeriesEntity = AnimeTracker.Api.Models.Series;

namespace AnimeTracker.Api.Tests.Services.Series;

// SeriesGraphBuilder.PersistAsync's single-series identity matching
// (rebuild-series-by-story-component design.md D8/D9, tasks 6.1-6.3): a
// build persists only the seed's own story component plus its version
// neighbours, matching the stored series it overlaps most over Core members
// alone (MatchToStoredSeries) and resolving IsPrimary by membership kind
// rather than by anchor distance (ResolveFoldedPrimaryAsync).
public class SeriesGraphBuilderVersionPersistenceTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
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

    private static SeriesMember Member(int seriesId, int animeId, bool isMainLine, int order,
        MembershipKind kind = MembershipKind.Core, bool isPrimary = true) => new()
    {
        SeriesId = seriesId,
        AnimeId = animeId,
        IsMainLine = isMainLine,
        Order = order,
        IsPrimary = isPrimary,
        MembershipKind = kind.ToString(),
    };

    // Clannad (sequel-linked to After Story) with a lone alternative_version
    // Movie that carries no story relations of its own — a FoldedVersion
    // neighbour (design.md D2). Pre-existing storage mirrors what
    // split-series-by-version's anchor rule actually produced: Clannad and
    // After Story are each their own anchor (each carries its own
    // alternative_setting special, omitted here as it plays no further part)
    // and Movie is a third anchor — so all three stored series hold each
    // other as boundary members, which the MembershipKind migration default
    // (design.md Migration Plan step 1) backfilled as Core across the board.
    [Fact]
    public async Task RebuildingThreeFragmentedClannadSeriesCollapsesIntoOneKeepingTheLargestOverlapsIdentity()
    {
        using var db = CreateDb();
        var clannad = Anime(1, "tv", new DateOnly(2007, 10, 4));
        var afterStory = Anime(2, "tv", new DateOnly(2008, 10, 2));
        var movie = Anime(3, "movie");
        Relate(clannad, afterStory, "sequel");
        Relate(movie, clannad, "alternative_version");
        db.AnimeMetadata.AddRange(clannad, afterStory, movie);

        db.Series.Add(new SeriesEntity { Id = 10, RootAnimeId = clannad.Id, BuiltAt = DateTimeOffset.UtcNow });
        db.SeriesMembers.AddRange(
            Member(10, clannad.Id, isMainLine: true, order: 0),
            Member(10, afterStory.Id, isMainLine: true, order: 1));

        db.Series.Add(new SeriesEntity { Id = 20, RootAnimeId = afterStory.Id, BuiltAt = DateTimeOffset.UtcNow, SelectedTitle = "After Story's Own Title" });
        db.SeriesMembers.AddRange(
            Member(20, afterStory.Id, isMainLine: true, order: 0),
            Member(20, clannad.Id, isMainLine: false, order: 0));

        db.Series.Add(new SeriesEntity { Id = 30, RootAnimeId = movie.Id, BuiltAt = DateTimeOffset.UtcNow, SelectedTitle = "Movie's Own Title" });
        db.SeriesMembers.AddRange(
            Member(30, movie.Id, isMainLine: true, order: 0),
            Member(30, clannad.Id, isMainLine: false, order: 0));
        await db.SaveChangesAsync();

        var result = await CreateBuilder(db).BuildAsync(clannad.Id, fetchBudget: 0, probeBudget: 0, expandLeanMembers: false);

        var allSeries = await db.Series.AsNoTracking().ToListAsync();
        Assert.Equal(10, result!.Id); // ties at overlap 2 (clannad+afterStory); lowest stored id wins
        Assert.Single(allSeries);
        var survivor = allSeries[0];
        Assert.Equal(clannad.Id, survivor.RootAnimeId);
        // Series 20 (overlap 2) outranks series 30 (overlap 1) for adoption.
        Assert.Equal("After Story's Own Title", survivor.SelectedTitle);

        var members = await db.SeriesMembers.Where(m => m.SeriesId == survivor.Id).ToListAsync();
        var movieMember = members.Single(m => m.AnimeId == movie.Id);
        Assert.Equal(nameof(MembershipKind.FoldedVersion), movieMember.MembershipKind);
        Assert.False(movieMember.IsMainLine);
        Assert.Equal(nameof(RelationGroup.AlternativeVersion), movieMember.RelationGroup);
        Assert.True(movieMember.IsPrimary); // no Core membership anywhere once the fragments are gone
    }

    // Fullmetal Alchemist 2003 and Brotherhood share no story relation — only
    // alternative_version — so they're two disjoint story components
    // (design.md D1). Brotherhood carries a side_story of its own, so it's a
    // NeighbourTelling (design.md D2): counted as an extra of 2003's series,
    // but its own stored series is a different story component entirely and
    // must never be touched by a build seeded from 2003 (design.md D9).
    [Fact]
    public async Task RebuildingFma2003LeavesBrotherhoodsStoredSeriesIntact()
    {
        using var db = CreateDb();
        var fma2003 = Anime(1, "tv", new DateOnly(2003, 10, 4));
        var fmaFilm = Anime(3, "movie");
        var brotherhood = Anime(2, "tv", new DateOnly(2009, 4, 5));
        var brotherhoodFilm = Anime(4, "movie");
        Relate(fma2003, fmaFilm, "sequel");
        Relate(fma2003, brotherhood, "alternative_version");
        Relate(brotherhood, brotherhoodFilm, "side_story");
        db.AnimeMetadata.AddRange(fma2003, fmaFilm, brotherhood, brotherhoodFilm);

        db.Series.Add(new SeriesEntity { Id = 100, RootAnimeId = brotherhood.Id, BuiltAt = DateTimeOffset.UtcNow, SelectedTitle = "Brotherhood" });
        db.SeriesMembers.AddRange(
            Member(100, brotherhood.Id, isMainLine: true, order: 0),
            Member(100, brotherhoodFilm.Id, isMainLine: true, order: 1));
        await db.SaveChangesAsync();

        var result = await CreateBuilder(db).BuildAsync(fma2003.Id, fetchBudget: 0, probeBudget: 0, expandLeanMembers: false);

        var brotherhoodSeries = await db.Series.AsNoTracking().SingleAsync(s => s.Id == 100);
        Assert.Equal("Brotherhood", brotherhoodSeries.SelectedTitle);
        var brotherhoodMembers = await db.SeriesMembers.Where(m => m.SeriesId == 100).ToListAsync();
        Assert.Equal(2, brotherhoodMembers.Count); // untouched — brotherhoodFilm never left it

        Assert.NotEqual(100, result!.Id); // a brand-new series for 2003's own component
        var newSeriesMembers = await db.SeriesMembers.Where(m => m.SeriesId == result.Id).ToListAsync();
        Assert.Equal(3, newSeriesMembers.Count); // 2003, its film, and brotherhood as a neighbour — never brotherhoodFilm
        Assert.DoesNotContain(newSeriesMembers, m => m.AnimeId == brotherhoodFilm.Id);

        var brotherhoodMember = newSeriesMembers.Single(m => m.AnimeId == brotherhood.Id);
        Assert.Equal(nameof(MembershipKind.NeighbourTelling), brotherhoodMember.MembershipKind);
        Assert.False(brotherhoodMember.IsMainLine);
        Assert.False(brotherhoodMember.IsPrimary); // that anime's own series (100) is its home
        Assert.Equal(nameof(RelationGroup.AlternativeVersion), brotherhoodMember.RelationGroup);
    }

    // A FoldedVersion neighbour can legitimately belong to two series at once
    // (design.md D8, e.g. Fate/Prototype). Exactly one membership is primary
    // — the larger story component — and building the larger one second
    // demotes the smaller one's existing primary claim in place rather than
    // leaving both true until it happens to rebuild.
    [Fact]
    public async Task AFoldedVersionSharedByTwoSeriesIsPrimaryInExactlyOneOfThem()
    {
        using var db = CreateDb();
        var sharedFold = Anime(1, "tv"); // no story relations of its own

        var smallRoot = Anime(2, "tv");
        var smallSeq = Anime(3, "tv");
        Relate(smallRoot, smallSeq, "sequel");
        Relate(sharedFold, smallRoot, "alternative_setting");

        var bigRoot = Anime(4, "tv");
        var bigSeq2 = Anime(5, "tv");
        var bigSeq3 = Anime(6, "tv");
        Relate(bigRoot, bigSeq2, "sequel");
        Relate(bigSeq2, bigSeq3, "sequel");
        Relate(sharedFold, bigRoot, "alternative_version");

        db.AnimeMetadata.AddRange(sharedFold, smallRoot, smallSeq, bigRoot, bigSeq2, bigSeq3);
        await db.SaveChangesAsync();

        var smallSeries = await CreateBuilder(db).BuildAsync(smallRoot.Id, fetchBudget: 0, probeBudget: 0, expandLeanMembers: false);
        var foldInSmall = await db.SeriesMembers.AsNoTracking()
            .SingleAsync(m => m.SeriesId == smallSeries!.Id && m.AnimeId == sharedFold.Id);
        Assert.True(foldInSmall.IsPrimary); // nothing competes for it yet

        var bigSeries = await CreateBuilder(db).BuildAsync(bigRoot.Id, fetchBudget: 0, probeBudget: 0, expandLeanMembers: false);

        var allFoldRows = await db.SeriesMembers.AsNoTracking().Where(m => m.AnimeId == sharedFold.Id).ToListAsync();
        Assert.Equal(2, allFoldRows.Count); // a member of both series
        Assert.Single(allFoldRows, m => m.IsPrimary);
        var primaryRow = allFoldRows.Single(m => m.IsPrimary);
        Assert.Equal(bigSeries!.Id, primaryRow.SeriesId); // the larger component (3 members) wins it from the smaller (2)
    }

    private sealed class NoOpRefreshService : IMetadataRefreshService
    {
        public Task<int> RefreshStaleBatchAsync(int batchSize, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task RefreshOneAsync(int animeId, CancellationToken ct = default) =>
            throw new AnimeMetadataNotFoundException(animeId);
    }
}
