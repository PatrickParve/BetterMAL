using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Metadata;
using AnimeTracker.Api.Services.Relations;
using AnimeTracker.Api.Services.Series;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimeTracker.Api.Tests.Services.Series;

// SeriesGraphBuilder.TraverseAsync (rebuild-series-by-story-component
// design.md decisions D1-D3, tasks 4.1-4.3): phase 1 is a bounded BFS over
// story relations alone, so a version relation (alternative_version,
// alternative_setting) neither joins nor grows the story component. Phase 2
// then follows each member's version relations one hop, in both directions,
// to find and classify the component's version neighbours — never expanding
// through one. A phase-1 result of exactly one anime additionally triggers
// seed resolution: rebuild from whichever version neighbour has the largest
// story component of its own, folding the original seed back in as a
// neighbour, so a lone alternative version resolves to the franchise it
// belongs to instead of reporting no series.
public class SeriesGraphBuilderTraverseAsyncTests
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

    [Fact]
    public async Task EachDeclaredEdgeIsRecordedOnceWithItsRelationType()
    {
        using var db = CreateDb();
        var season1 = Anime(1, "tv");
        var season2 = Anime(2, "tv");
        Relate(season1, season2, "sequel");

        db.AnimeMetadata.AddRange(season1, season2);
        await db.SaveChangesAsync();

        var (_, edges, _, _, _) = await CreateBuilder(db).TraverseAsync(season1.Id, fetchBudget: 0, probeBudget: 0, expandLeanMembers: false, ct: default);

        var edge = Assert.Single(edges);
        Assert.Equal(new RelationEdge(season1.Id, season2.Id, "sequel"), edge);
    }

    [Fact]
    public async Task ContradictedEdgeIsExcludedFromEdgesEvenWhenTheFarEndIsAMemberThroughAnotherPath()
    {
        // 39360 is reached legitimately through 3044's own sequel edge, but
        // the direct 17965->39360 edge is contradicted and must not appear
        // among the recorded edges even though 39360 is still a member.
        using var db = CreateDb();
        var fetched = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var root = Anime(17965, "movie");
        var contradicted = Anime(39360, "movie");
        var bridge = Anime(3044, "tv");
        root.LastSyncedAt = fetched;
        contradicted.LastSyncedAt = fetched;
        bridge.LastSyncedAt = fetched;

        Relate(root, contradicted, "sequel"); // one-sided; contradicted by AniList below
        Relate(root, bridge, "sequel");
        Relate(bridge, contradicted, "sequel"); // legitimate second path to the same anime

        db.AnimeMetadata.AddRange(root, contradicted, bridge);
        db.AnimeAiringSyncs.AddRange(
            new AnimeAiringSync { AnimeId = 17965, AniListId = 1, RelationsFetchedAt = fetched },
            new AnimeAiringSync { AnimeId = 39360, AniListId = 2, RelationsFetchedAt = fetched });
        await db.SaveChangesAsync();

        var (members, edges, _, _, _) = await CreateBuilder(db).TraverseAsync(17965, fetchBudget: 0, probeBudget: 0, expandLeanMembers: false, ct: default);

        Assert.Contains(members, m => m.Id == 39360); // still a member, via the bridge
        Assert.DoesNotContain(edges, e => e.OwnerId == 17965 && e.RelatedAnimeId == 39360);
        Assert.Contains(edges, e => e.OwnerId == 3044 && e.RelatedAnimeId == 39360);
    }

    // fix-alternative-version-grouping task 4.5, design.md D1: a version
    // relation is recorded for grouping but never followed, so it draws the
    // containment line at exactly the story component — no wider, no
    // narrower. movieVersion is a genuine member (tied in by its own
    // side_story edge) and its alternative_version edge to season2, another
    // member, is now recorded; outsider's only relation to the component is
    // alternative_version, so it admits nothing — the member set stops at
    // the three story-connected anime.
    [Fact]
    public async Task AVersionEdgeBetweenTwoMembersIsRecordedWhileOneToANonMemberAdmitsNothing()
    {
        using var db = CreateDb();
        var season1 = Anime(1, "tv");
        var season2 = Anime(2, "tv");
        var movieVersion = Anime(3, "movie"); // tied into the component by its own side_story edge to season1
        var outsider = Anime(4, "movie"); // only relation to the component is a version relation — never admitted

        Relate(season1, season2, "sequel");
        Relate(season1, movieVersion, "side_story");
        Relate(movieVersion, season2, "alternative_version"); // version relation between two members
        Relate(season1, outsider, "alternative_version"); // version relation to a non-member

        db.AnimeMetadata.AddRange(season1, season2, movieVersion, outsider);
        await db.SaveChangesAsync();

        var (members, edges, _, _, _) = await CreateBuilder(db)
            .TraverseAsync(season1.Id, fetchBudget: 0, probeBudget: 0, expandLeanMembers: false, ct: default);

        Assert.Equal(
            new[] { season1.Id, season2.Id, movieVersion.Id }.OrderBy(x => x),
            members.Select(m => m.Id).OrderBy(x => x));
        Assert.DoesNotContain(members, m => m.Id == outsider.Id);

        Assert.Contains(edges, e => e.OwnerId == movieVersion.Id && e.RelatedAnimeId == season2.Id && e.RelationType == "alternative_version");
    }

    // fix-alternative-version-grouping task 4.5: a version relation is
    // filtered by the same AniList-contradiction check as a story relation
    // (design.md D1) — mirrors
    // ContradictedEdgeIsExcludedFromEdgesEvenWhenTheFarEndIsAMemberThroughAnotherPath
    // above, with the direct edge's type swapped from sequel to
    // alternative_version.
    [Fact]
    public async Task AnAniListContradictedVersionRelationIsNotRecorded()
    {
        using var db = CreateDb();
        var fetched = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var root = Anime(17965, "movie");
        var contradicted = Anime(39360, "movie");
        var bridge = Anime(3044, "tv");
        root.LastSyncedAt = fetched;
        contradicted.LastSyncedAt = fetched;
        bridge.LastSyncedAt = fetched;

        Relate(root, contradicted, "alternative_version"); // one-sided; contradicted by AniList below
        Relate(root, bridge, "sequel");
        Relate(bridge, contradicted, "sequel"); // legitimate second path to the same anime

        db.AnimeMetadata.AddRange(root, contradicted, bridge);
        db.AnimeAiringSyncs.AddRange(
            new AnimeAiringSync { AnimeId = 17965, AniListId = 1, RelationsFetchedAt = fetched },
            new AnimeAiringSync { AnimeId = 39360, AniListId = 2, RelationsFetchedAt = fetched });
        await db.SaveChangesAsync();

        var (members, edges, _, _, _) = await CreateBuilder(db).TraverseAsync(root.Id, fetchBudget: 0, probeBudget: 0, expandLeanMembers: false, ct: default);

        Assert.Contains(members, m => m.Id == contradicted.Id); // still a member, via the bridge
        Assert.DoesNotContain(edges, e => e.OwnerId == root.Id && e.RelatedAnimeId == contradicted.Id);
        Assert.Contains(edges, e => e.OwnerId == bridge.Id && e.RelatedAnimeId == contradicted.Id);
    }

    // Fate/Zero's sequel edges put Fate/stay night and Unlimited Blade Works
    // on the same story component regardless of the alternative_version edge
    // between them; Prisma☆Illya hangs off by alternative_setting alone,
    // with its own sequel — a version neighbour that opens its own series
    // rather than joining this one, and whose own sequel is never reached at
    // all.
    [Fact]
    public async Task ASeedInsideFateStayNightTraversesOnlyItsStoryComponent()
    {
        using var db = CreateDb();
        var fateZero = Anime(10, "tv");
        var fateStayNight = Anime(1, "tv");
        var unlimitedBladeWorks = Anime(2, "tv");
        var prismaIllya = Anime(20, "tv");
        var prismaIllya2Wei = Anime(21, "tv");

        Relate(fateZero, fateStayNight, "sequel");
        Relate(fateZero, unlimitedBladeWorks, "sequel");
        Relate(fateStayNight, unlimitedBladeWorks, "alternative_version");
        Relate(fateStayNight, prismaIllya, "alternative_setting");
        Relate(prismaIllya, prismaIllya2Wei, "sequel");

        db.AnimeMetadata.AddRange(fateZero, fateStayNight, unlimitedBladeWorks, prismaIllya, prismaIllya2Wei);
        await db.SaveChangesAsync();

        var (members, edges, _, _, versionNeighbours) = await CreateBuilder(db)
            .TraverseAsync(fateStayNight.Id, fetchBudget: 0, probeBudget: 0, expandLeanMembers: false, ct: default);

        Assert.Equal(
            new[] { fateStayNight.Id, unlimitedBladeWorks.Id, fateZero.Id }.OrderBy(x => x),
            members.Select(m => m.Id).OrderBy(x => x));

        var neighbour = Assert.Single(versionNeighbours);
        Assert.Equal(prismaIllya.Id, neighbour.AnimeId);
        Assert.Equal(MembershipKind.NeighbourTelling, neighbour.MembershipKind);

        // Never expanded through: Prisma☆Illya's own sequel is reached by
        // nothing here at all.
        Assert.DoesNotContain(members, m => m.Id == prismaIllya2Wei.Id);
        Assert.DoesNotContain(edges, e => e.OwnerId == prismaIllya2Wei.Id || e.RelatedAnimeId == prismaIllya2Wei.Id);
    }

    // Clannad Movie's only relations are alternative_version to Clannad and
    // to Clannad: After Story — phase 1 alone finds just the Movie. Seed
    // resolution rebuilds from whichever of the two has the largest story
    // component; both resolve to the same {Clannad, After Story} component
    // (tied at size 2), so the lower MAL id wins deterministically, and the
    // Movie folds back in as a version neighbour of it.
    [Fact]
    public async Task ASeedFromClannadMovieResolvesToTheClannadComponent()
    {
        using var db = CreateDb();
        var clannad = Anime(1, "tv");
        var afterStory = Anime(2, "tv");
        var movie = Anime(3, "movie");

        Relate(clannad, afterStory, "sequel");
        Relate(movie, clannad, "alternative_version");
        Relate(movie, afterStory, "alternative_version");

        db.AnimeMetadata.AddRange(clannad, afterStory, movie);
        await db.SaveChangesAsync();

        var (members, _, _, _, versionNeighbours) = await CreateBuilder(db)
            .TraverseAsync(movie.Id, fetchBudget: 0, probeBudget: 0, expandLeanMembers: false, ct: default);

        Assert.Equal(
            new[] { clannad.Id, afterStory.Id }.OrderBy(x => x),
            members.Select(m => m.Id).OrderBy(x => x));

        var neighbour = Assert.Single(versionNeighbours);
        Assert.Equal(movie.Id, neighbour.AnimeId);
        Assert.Equal(MembershipKind.FoldedVersion, neighbour.MembershipKind);
    }

    // Two standalone works with no story relation to anything, linked only to
    // each other by alternative_version: phase 1 alone finds just the seed,
    // and its only candidate's own component also sizes to 1 — a genuine tie,
    // unlike Clannad Movie's neighbours which win outright on size. The
    // seed's own inclusion in that comparison, plus the episode-count
    // tie-break, is what settles on the fuller work regardless of which side
    // was opened first, rather than always flipping onto whichever one
    // happens to be "the other" (fix-false-memory-lone-version-pair).
    [Theory]
    [InlineData(1)] // seeded from the thin pilot
    [InlineData(2)] // seeded from the fuller remake
    public async Task TwoStandaloneWorksLinkedOnlyByVersionSettleOnTheFullerOneEitherWayRound(int seedId)
    {
        using var db = CreateDb();
        var pilot = Anime(1, "ona");
        pilot.TotalEpisodes = 1;
        var remake = Anime(2, "ona");
        remake.TotalEpisodes = 7;
        Relate(pilot, remake, "alternative_version");
        Relate(remake, pilot, "alternative_version");

        db.AnimeMetadata.AddRange(pilot, remake);
        await db.SaveChangesAsync();

        var (members, _, _, _, versionNeighbours) = await CreateBuilder(db)
            .TraverseAsync(seedId, fetchBudget: 0, probeBudget: 0, expandLeanMembers: false, ct: default);

        var soleMember = Assert.Single(members);
        Assert.Equal(remake.Id, soleMember.Id);
        var neighbour = Assert.Single(versionNeighbours);
        Assert.Equal(pilot.Id, neighbour.AnimeId);
        Assert.Equal(MembershipKind.FoldedVersion, neighbour.MembershipKind);
    }

    // With no version neighbour to resolve through either, a lone anime is
    // reported exactly as today: not part of a series (SeriesGraphBuilder.
    // BuildAsync's members.Count <= 1 rule).
    [Fact]
    public async Task ALoneAnimeWithNoVersionNeighbourIsReturnedAlone()
    {
        using var db = CreateDb();
        var lone = Anime(1, "tv");
        db.AnimeMetadata.Add(lone);
        await db.SaveChangesAsync();

        var (members, _, _, _, versionNeighbours) = await CreateBuilder(db)
            .TraverseAsync(lone.Id, fetchBudget: 0, probeBudget: 0, expandLeanMembers: false, ct: default);

        var soleMember = Assert.Single(members);
        Assert.Equal(lone.Id, soleMember.Id);
        Assert.Empty(versionNeighbours);
    }

    private sealed class NoOpRefreshService : IMetadataRefreshService
    {
        public Task<int> RefreshStaleBatchAsync(int batchSize, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task RefreshOneAsync(int animeId, CancellationToken ct = default) =>
            throw new AnimeMetadataNotFoundException(animeId);
    }
}
