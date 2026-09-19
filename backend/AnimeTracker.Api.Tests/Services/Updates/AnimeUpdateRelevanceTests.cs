using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Relations;
using AnimeTracker.Api.Services.Updates;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Services.Updates;

// IAnimeUpdateRelevance (anime-updates spec, "Nothing is recorded for an
// anime outside my list and its direct relations"; scope-updates-to-my-list
// tasks 6.2): the single eligibility test the write-side gate
// (IAnimeUpdateRecorder) and the read-side filter (AnimeUpdateService) both
// read, so the two can never disagree about which updates exist.
public class AnimeUpdateRelevanceTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static AnimeUpdateRelevance CreateRelevance(AnimeTrackerDbContext db) =>
        new(db, new RelationResolver(db));

    [Fact]
    public async Task ANonDroppedOwnEntryIsRelevant()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Solo" });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Status = WatchStatus.PlanToWatch });
        await db.SaveChangesAsync();

        Assert.True(await CreateRelevance(db).IsRelevantAsync(1));
    }

    [Fact]
    public async Task AnAnimeOneEdgeFromAnEntryIsRelevantThroughItsOwnOutgoingEdge()
    {
        using var db = CreateDb();
        // Anime 1's own outgoing edge names anime 2, my entry.
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Sequel" });
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 2, Title = "My show" });
        db.AnimeRelatedAnime.Add(new AnimeRelatedAnime { AnimeId = 1, RelatedAnimeId = 2, RelationType = "sequel" });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 2, Status = WatchStatus.Watching });
        await db.SaveChangesAsync();

        Assert.True(await CreateRelevance(db).IsRelevantAsync(1));
    }

    [Fact]
    public async Task AnAnimeOneEdgeFromAnEntryIsRelevantThroughMyEntrysOutgoingEdge()
    {
        using var db = CreateDb();
        // The edge is stored the other way round from the previous case: my
        // own entry (2) names anime 1 as its prequel.
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Prequel" });
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 2, Title = "My show" });
        db.AnimeRelatedAnime.Add(new AnimeRelatedAnime { AnimeId = 2, RelatedAnimeId = 1, RelationType = "prequel" });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 2, Status = WatchStatus.Watching });
        await db.SaveChangesAsync();

        Assert.True(await CreateRelevance(db).IsRelevantAsync(1));
    }

    [Theory]
    [InlineData("character")]
    [InlineData("alternative_setting")]
    public async Task ALooseRelationTypeStillQualifies(string relationType)
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Crossover" });
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 2, Title = "My show" });
        db.AnimeRelatedAnime.Add(new AnimeRelatedAnime { AnimeId = 1, RelatedAnimeId = 2, RelationType = relationType });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 2, Status = WatchStatus.Watching });
        await db.SaveChangesAsync();

        Assert.True(await CreateRelevance(db).IsRelevantAsync(1));
    }

    [Fact]
    public async Task AContradictedOnlyEdgeIsNotRelevant()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Disputed" });
        // LastSyncedAt set: RelationResolver only adjudicates an edge whose
        // far end has itself been full-detail fetched.
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 2, Title = "My show", LastSyncedAt = DateTimeOffset.UtcNow });
        db.AnimeRelatedAnime.Add(new AnimeRelatedAnime { AnimeId = 1, RelatedAnimeId = 2, RelationType = "prequel" });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 2, Status = WatchStatus.Watching });
        // Both ends known to AniList, with no corroborating AniList edge
        // between them at all — Adjudicate resolves this to Contradicted.
        db.AnimeAiringSyncs.Add(new AnimeAiringSync { AnimeId = 1, AniListId = 100, RelationsFetchedAt = DateTimeOffset.UtcNow });
        db.AnimeAiringSyncs.Add(new AnimeAiringSync { AnimeId = 2, AniListId = 200, RelationsFetchedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        Assert.False(await CreateRelevance(db).IsRelevantAsync(1));
    }

    [Fact]
    public async Task AnEdgeThatOnlyReachesADroppedEntryIsNotRelevant()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Sequel" });
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 2, Title = "My dropped show" });
        db.AnimeRelatedAnime.Add(new AnimeRelatedAnime { AnimeId = 1, RelatedAnimeId = 2, RelationType = "sequel" });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 2, Status = WatchStatus.Dropped });
        await db.SaveChangesAsync();

        Assert.False(await CreateRelevance(db).IsRelevantAsync(1));
    }

    [Fact]
    public async Task AnUnrelatedAnimeIsNotRelevant()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Stranger" });
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 2, Title = "My show" });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 2, Status = WatchStatus.Watching });
        await db.SaveChangesAsync();

        Assert.False(await CreateRelevance(db).IsRelevantAsync(1));
    }

    [Fact]
    public async Task TheTieBreakBetweenTwoEquallySpecificAffiliatesFollowsTheDisplayedTitle()
    {
        using var db = CreateDb();
        // Both candidates are "sequel" edges — the same most-specific
        // relation — so the tie-break decides which is named. By MAL title
        // alone, anime 3 ("Original Alpha") would sort first; by the title
        // each is actually displayed with, anime 2 ("Original Beta") does,
        // because anime 3's English title ("Zephyr Season Two") sorts after
        // it (design D4). The pick must follow the displayed title, matching
        // the reason AnimeUpdateService composes for the same edge.
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Multi-Affiliate Update" });
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 2, Title = "Original Beta" });
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 3, Title = "Original Alpha", EnglishTitle = "Zephyr Season Two" });
        db.AnimeRelatedAnime.Add(new AnimeRelatedAnime { AnimeId = 1, RelatedAnimeId = 2, RelationType = "sequel" });
        db.AnimeRelatedAnime.Add(new AnimeRelatedAnime { AnimeId = 1, RelatedAnimeId = 3, RelationType = "sequel" });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 2, Status = WatchStatus.Watching });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 3, Status = WatchStatus.Watching });
        await db.SaveChangesAsync();

        var anime = await db.AnimeMetadata.Include(a => a.RelatedAnime).SingleAsync(a => a.Id == 1);
        var affiliate = await CreateRelevance(db).FindAffiliateAsync(anime);

        Assert.Equal(2, affiliate?.AnimeId);
    }
}
