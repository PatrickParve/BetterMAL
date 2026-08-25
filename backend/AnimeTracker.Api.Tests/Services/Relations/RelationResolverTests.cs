using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Relations;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Services.Relations;

// RelationResolver (relation-confidence spec, tasks 4.1-4.8/5.7-5.8): the
// union of outgoing and inverted-incoming edges, confidence classification
// (MAL agreement plus AniList adjudication), and the ranked prequel/sequel
// pick.
public class RelationResolverTests
{
    // A fixed, non-default instant standing for "this anime has been
    // full-detail fetched" — RelationResolver only ever checks LastSyncedAt
    // against `default`, never against wall-clock time.
    private static readonly DateTimeOffset Fetched = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static AnimeMetadata Anime(
        int id, string mediaType = "tv", DateOnly? airedFrom = null, DateTimeOffset lastSyncedAt = default) => new()
    {
        Id = id,
        Title = $"Anime {id}",
        MediaType = mediaType,
        AiredFrom = airedFrom,
        LastSyncedAt = lastSyncedAt,
    };

    private static void Relate(AnimeMetadata from, AnimeMetadata to, string relationType) =>
        from.RelatedAnime.Add(new AnimeRelatedAnime
        {
            AnimeId = from.Id,
            RelatedAnimeId = to.Id,
            RelationType = relationType,
            Title = to.Title,
            MediaType = to.MediaType,
        });

    // --- 4.1: inverse map ---

    [Theory]
    [InlineData("sequel", "prequel")]
    [InlineData("prequel", "sequel")]
    [InlineData("parent_story", "side_story")]
    [InlineData("side_story", "parent_story")]
    [InlineData("summary", "full_story")]
    [InlineData("full_story", "summary")]
    [InlineData("alternative_version", "alternative_version")]
    [InlineData("alternative_setting", "alternative_setting")]
    [InlineData("character", "character")]
    [InlineData("other", "other")]
    public void Invert_MapsBothDirections(string stored, string expectedInverse)
    {
        Assert.Equal(expectedInverse, RelationInverse.Invert(stored));
    }

    [Theory]
    [InlineData("spin_off")]
    [InlineData("adaptation")]
    [InlineData("some_unrecognized_value")]
    public void Invert_NoConfidentInverse(string stored)
    {
        Assert.Null(RelationInverse.Invert(stored));
    }

    // --- 4.2/8.2: union, de-duplication, ordering ---

    [Fact]
    public async Task GetEdgesAsync_IncomingEdgeIsInvertedOntoTheViewedAnime()
    {
        using var db = CreateDb();
        var a = Anime(1, lastSyncedAt: Fetched);
        var b = Anime(2, lastSyncedAt: Fetched);
        Relate(a, b, "sequel"); // A's own page: sequel -> B
        db.AnimeMetadata.AddRange(a, b);
        await db.SaveChangesAsync();

        var edges = await new RelationResolver(db).GetEdgesAsync(b);

        var entry = Assert.Single(edges);
        Assert.Equal(1, entry.AnimeId);
        Assert.Equal("prequel", entry.RelationType);
        Assert.True(entry.IsReverseDerived);
    }

    [Fact]
    public async Task GetEdgesAsync_SymmetricEdgeDedupesToOneEntryKeepingOutgoingType()
    {
        using var db = CreateDb();
        var a = Anime(1, lastSyncedAt: Fetched);
        var b = Anime(2, lastSyncedAt: Fetched);
        Relate(a, b, "sequel");
        Relate(b, a, "prequel");
        db.AnimeMetadata.AddRange(a, b);
        await db.SaveChangesAsync();

        var edges = await new RelationResolver(db).GetEdgesAsync(b);

        var entry = Assert.Single(edges);
        Assert.Equal(1, entry.AnimeId);
        Assert.Equal("prequel", entry.RelationType); // B's own stored type, not a re-derivation of A's
        Assert.False(entry.IsReverseDerived);
        Assert.Equal(RelationConfidence.Confirmed, entry.Confidence);
    }

    [Fact]
    public async Task GetEdgesAsync_OutgoingEntriesPrecedeReverseDerivedOnes()
    {
        using var db = CreateDb();
        var a = Anime(1, lastSyncedAt: Fetched);
        var b = Anime(2, lastSyncedAt: Fetched);
        var c = Anime(3, lastSyncedAt: Fetched);
        Relate(a, b, "side_story"); // A's own outgoing edge
        Relate(c, a, "sequel"); // reverse-derived "prequel" on A's page
        db.AnimeMetadata.AddRange(a, b, c);
        await db.SaveChangesAsync();

        var edges = await new RelationResolver(db).GetEdgesAsync(a);

        Assert.Equal(2, edges.Count);
        Assert.False(edges[0].IsReverseDerived);
        Assert.Equal(2, edges[0].AnimeId);
        Assert.True(edges[1].IsReverseDerived);
        Assert.Equal(3, edges[1].AnimeId);
    }

    [Fact]
    public async Task GetEdgesAsync_NoInverseRelationKeepsRawTypeAndIsNeverConfirmed()
    {
        using var db = CreateDb();
        var a = Anime(1, lastSyncedAt: Fetched);
        var b = Anime(2, lastSyncedAt: Fetched);
        Relate(a, b, "spin_off"); // A stores itself as B's spin-off
        db.AnimeMetadata.AddRange(a, b);
        await db.SaveChangesAsync();

        var edges = await new RelationResolver(db).GetEdgesAsync(b);

        var entry = Assert.Single(edges);
        Assert.Equal("spin_off", entry.RelationType); // raw value carried across unchanged
        Assert.True(entry.IsReverseDerived);
        Assert.False(entry.HasConfidentInverse);
        Assert.Equal(RelationConfidence.Unknown, entry.Confidence);
    }

    // --- 4.4/8.3: confidence matrix ---

    [Fact]
    public async Task GetEdgesAsync_FarEndFetchedAndDisagrees_Unconfirmed()
    {
        using var db = CreateDb();
        var a = Anime(1, lastSyncedAt: Fetched);
        var b = Anime(2, lastSyncedAt: Fetched); // fetched, stores nothing back
        Relate(a, b, "sequel");
        db.AnimeMetadata.AddRange(a, b);
        await db.SaveChangesAsync();

        var edges = await new RelationResolver(db).GetEdgesAsync(a);

        Assert.Equal(RelationConfidence.Unconfirmed, Assert.Single(edges).Confidence);
    }

    [Fact]
    public async Task GetEdgesAsync_FarEndNeverFetched_UnknownNotUnconfirmed()
    {
        using var db = CreateDb();
        var a = Anime(1, lastSyncedAt: Fetched);
        var b = Anime(2); // LastSyncedAt == default: never full-detail fetched
        Relate(a, b, "sequel");
        db.AnimeMetadata.AddRange(a, b);
        await db.SaveChangesAsync();

        var edges = await new RelationResolver(db).GetEdgesAsync(a);

        Assert.Equal(RelationConfidence.Unknown, Assert.Single(edges).Confidence);
    }

    // --- 5.7/5.8/8.9: AniList adjudication ---

    private static void SetAniListKnown(AnimeTrackerDbContext db, int animeId, int aniListId) =>
        db.AnimeAiringSyncs.Add(new AnimeAiringSync { AnimeId = animeId, AniListId = aniListId, RelationsFetchedAt = Fetched });

    [Fact]
    public async Task GetEdgesAsync_AniListReportsMatchingType_Confirmed()
    {
        using var db = CreateDb();
        var a = Anime(1, lastSyncedAt: Fetched);
        var b = Anime(2, lastSyncedAt: Fetched); // MAL-Unconfirmed: fetched, no reciprocation
        Relate(a, b, "sequel");
        db.AnimeMetadata.AddRange(a, b);
        SetAniListKnown(db, 1, 101);
        SetAniListKnown(db, 2, 102);
        db.AniListRelations.Add(new AniListRelation { AnimeId = 1, RelatedAnimeId = 2, RelationType = "SEQUEL" });
        await db.SaveChangesAsync();

        var edges = await new RelationResolver(db).GetEdgesAsync(a);

        Assert.Equal(RelationConfidence.Confirmed, Assert.Single(edges).Confidence);
    }

    [Fact]
    public async Task GetEdgesAsync_AniListRelatesPairUnderDifferentType_Corroborated()
    {
        using var db = CreateDb();
        var a = Anime(1, lastSyncedAt: Fetched);
        var b = Anime(2, lastSyncedAt: Fetched);
        Relate(a, b, "sequel");
        db.AnimeMetadata.AddRange(a, b);
        SetAniListKnown(db, 1, 101);
        SetAniListKnown(db, 2, 102);
        db.AniListRelations.Add(new AniListRelation { AnimeId = 1, RelatedAnimeId = 2, RelationType = "SIDE_STORY" });
        await db.SaveChangesAsync();

        var edges = await new RelationResolver(db).GetEdgesAsync(a);

        Assert.Equal(RelationConfidence.Corroborated, Assert.Single(edges).Confidence);
    }

    [Fact]
    public async Task GetEdgesAsync_AniListKnowsBothEndsAndReportsNoEdge_Contradicted()
    {
        using var db = CreateDb();
        var a = Anime(1, lastSyncedAt: Fetched);
        var b = Anime(2, lastSyncedAt: Fetched);
        Relate(a, b, "sequel");
        db.AnimeMetadata.AddRange(a, b);
        SetAniListKnown(db, 1, 101);
        SetAniListKnown(db, 2, 102);
        // No AniListRelation row between them at all.
        await db.SaveChangesAsync();

        var edges = await new RelationResolver(db).GetEdgesAsync(a);

        Assert.Equal(RelationConfidence.Contradicted, Assert.Single(edges).Confidence);
    }

    [Fact]
    public async Task GetEdgesAsync_AbsentAdjudicationDataStaysUnconfirmed()
    {
        // Never looked up on AniList (no AnimeAiringSync rows at all) and a
        // failed background lookup are indistinguishable in stored state —
        // both must behave as they did before adjudication existed, rather
        // than being wrongly escalated to Contradicted.
        using var db = CreateDb();
        var a = Anime(1, lastSyncedAt: Fetched);
        var b = Anime(2, lastSyncedAt: Fetched);
        Relate(a, b, "sequel");
        db.AnimeMetadata.AddRange(a, b);
        await db.SaveChangesAsync();

        var edges = await new RelationResolver(db).GetEdgesAsync(a);

        Assert.Equal(RelationConfidence.Unconfirmed, Assert.Single(edges).Confidence);
    }

    // --- 4.5/8.6-8.8: ranked pick ---

    [Fact]
    public async Task ResolveAsync_RecapCandidateDiscardedInFavourOfRealPrequel()
    {
        // Mirrors the live case: Made in Abyss Movie 3 (36862) has two
        // prequel candidates — 34599 (the TV season) and 37515 (a recap
        // which also carries a prequel edge into the same chain).
        using var db = CreateDb();
        var movie3 = Anime(36862, mediaType: "movie", airedFrom: new DateOnly(2020, 6, 1), lastSyncedAt: Fetched);
        var tvSeason = Anime(34599, mediaType: "tv", airedFrom: new DateOnly(2017, 1, 1), lastSyncedAt: Fetched);
        var recap = Anime(37515, mediaType: "movie", airedFrom: new DateOnly(2019, 1, 1), lastSyncedAt: Fetched);
        Relate(movie3, tvSeason, "prequel");
        Relate(movie3, recap, "prequel");
        Relate(recap, tvSeason, "full_story"); // recap tags itself as a recap of the TV season
        db.AnimeMetadata.AddRange(movie3, tvSeason, recap);
        await db.SaveChangesAsync();

        var resolution = await new RelationResolver(db).ResolveAsync(movie3);

        Assert.NotNull(resolution.Prequel);
        Assert.Equal(34599, resolution.Prequel!.AnimeId);
    }

    [Fact]
    public async Task ResolveAsync_RecapCandidateDiscardedEvenWhenItHoldsTheLowerMalId()
    {
        // Same shape as the Made in Abyss case, but with the recap's id
        // deliberately set LOWER than the real prequel's — if ranking ever
        // fell through to the id tiebreak without discarding the recap
        // first, it would pick the recap by accident.
        using var db = CreateDb();
        var viewed = Anime(500, mediaType: "movie", lastSyncedAt: Fetched);
        var recap = Anime(100, mediaType: "movie", lastSyncedAt: Fetched);
        var realPrequel = Anime(200, mediaType: "movie", lastSyncedAt: Fetched);
        Relate(viewed, recap, "prequel");
        Relate(viewed, realPrequel, "prequel");
        Relate(recap, realPrequel, "full_story");
        db.AnimeMetadata.AddRange(viewed, recap, realPrequel);
        await db.SaveChangesAsync();

        var resolution = await new RelationResolver(db).ResolveAsync(viewed);

        Assert.Equal(200, resolution.Prequel!.AnimeId);
    }

    [Fact]
    public async Task ResolveAsync_TwoReverseDerivedPrequelCandidatesRecapExcluded()
    {
        using var db = CreateDb();
        var viewed = Anime(1, mediaType: "tv", lastSyncedAt: Fetched);
        var recapSeason = Anime(2, mediaType: "tv", lastSyncedAt: Fetched);
        var realSeason = Anime(3, mediaType: "tv", lastSyncedAt: Fetched);
        Relate(recapSeason, viewed, "sequel"); // -> reverse-derived prequel candidate
        Relate(realSeason, viewed, "sequel"); // -> reverse-derived prequel candidate
        Relate(recapSeason, realSeason, "full_story"); // recapSeason recaps realSeason
        db.AnimeMetadata.AddRange(viewed, recapSeason, realSeason);
        await db.SaveChangesAsync();

        var resolution = await new RelationResolver(db).ResolveAsync(viewed);

        Assert.Equal(3, resolution.Prequel!.AnimeId);
    }

    [Fact]
    public async Task ResolveAsync_OutgoingPickNotDisplacedByReverseDerivedAtEqualConfidence()
    {
        using var db = CreateDb();
        var viewed = Anime(1, mediaType: "tv", lastSyncedAt: Fetched);
        var outgoingTarget = Anime(2, mediaType: "tv", lastSyncedAt: Fetched); // Unconfirmed
        var reverseTarget = Anime(3, mediaType: "tv", lastSyncedAt: Fetched); // Unconfirmed
        Relate(viewed, outgoingTarget, "prequel");
        Relate(reverseTarget, viewed, "sequel"); // -> reverse-derived prequel candidate at 3
        db.AnimeMetadata.AddRange(viewed, outgoingTarget, reverseTarget);
        await db.SaveChangesAsync();

        var resolution = await new RelationResolver(db).ResolveAsync(viewed);

        Assert.Equal(2, resolution.Prequel!.AnimeId);
        Assert.False(resolution.Prequel.IsReverseDerived);
    }

    [Fact]
    public async Task ResolveAsync_ConfirmedCandidateBeatsUnconfirmedDespiteHigherId()
    {
        using var db = CreateDb();
        var viewed = Anime(1, mediaType: "tv", lastSyncedAt: Fetched);
        var unconfirmedCandidate = Anime(2, mediaType: "tv", lastSyncedAt: Fetched);
        var confirmedCandidate = Anime(3, mediaType: "tv", lastSyncedAt: Fetched);
        Relate(viewed, unconfirmedCandidate, "sequel");
        Relate(viewed, confirmedCandidate, "sequel");
        Relate(confirmedCandidate, viewed, "prequel"); // reciprocates -> Confirmed
        db.AnimeMetadata.AddRange(viewed, unconfirmedCandidate, confirmedCandidate);
        await db.SaveChangesAsync();

        var resolution = await new RelationResolver(db).ResolveAsync(viewed);

        Assert.Equal(3, resolution.Sequel!.AnimeId);
        Assert.Equal(RelationConfidence.Confirmed, resolution.Sequel.Confidence);
    }
}
