using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Relations;
using AnimeTracker.Api.Services.Scheduling;
using AnimeTracker.Api.Services.Updates;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Services.Updates;

// IAnimeUpdateService (anime-updates spec, "Updates are shown for my own
// entries and for their franchises", "An update names the anime, what
// happened, and why it concerns me"; tasks 6.1-6.7): eligibility is derived
// at read time (design.md D4/D5) and the same method backs both the 30-day
// dashboard read and the full history.
public class AnimeUpdateServiceTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static AnimeUpdateService CreateService(AnimeTrackerDbContext db) =>
        new(db, new AnimeUpdateRelevance(db, new RelationResolver(db)), new BroadcastLocalTimeConverter());

    private static AnimeUpdateService CreateServiceWithPassthroughConverter(AnimeTrackerDbContext db) =>
        new(db, new AnimeUpdateRelevance(db, new RelationResolver(db)), new FakeBroadcastLocalTimeConverter());

    // The house fake (mirrors MainDashboardServiceReopenTests and friends): a
    // pass-through so a slot test can assert on the JST day/time it stored
    // without doing Helsinki/Tokyo DST arithmetic by hand.
    private sealed class FakeBroadcastLocalTimeConverter : IBroadcastLocalTimeConverter
    {
        public DateOnly GetStartOfWeek(DateOnly referenceDate) => referenceDate;
        public DateOnly GetLocalDate(DateTimeOffset instantUtc) => DateOnly.FromDateTime(instantUtc.UtcDateTime);
        public TimeOnly GetLocalTime(DateTimeOffset instantUtc) => TimeOnly.FromDateTime(instantUtc.UtcDateTime);
        public DateTimeOffset LocalMidnightUtc(DateOnly localDate) => new(localDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        public (DayOfWeek LocalDayOfWeek, TimeOnly LocalTime) ConvertBroadcastSlot(DayOfWeek jstDayOfWeek, TimeOnly jstTime, DateTimeOffset referenceUtc) =>
            (jstDayOfWeek, jstTime);
    }

    [Fact]
    public async Task StandaloneNonDroppedEntryQualifiesWithNoRelationsAtAll()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Solo" });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Status = WatchStatus.PlanToWatch });
        db.AnimeUpdates.Add(new AnimeUpdate { AnimeId = 1, DetectedAt = DateTimeOffset.UtcNow, Kinds = AnimeUpdateKinds.StartDateReleased });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetHistoryAsync();

        var dto = Assert.Single(result);
        Assert.Equal("Plan to watch", dto.Reason);
    }

    [Fact]
    public async Task DroppingMyOwnEntryHidesIt()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Solo" });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Status = WatchStatus.Dropped });
        db.AnimeUpdates.Add(new AnimeUpdate { AnimeId = 1, DetectedAt = DateTimeOffset.UtcNow, Kinds = AnimeUpdateKinds.StartDateReleased });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetHistoryAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task DroppingTheSoleAffiliateHidesAnAffiliatedUpdate()
    {
        using var db = CreateDb();
        // Anime 1 is the update's own anime (a sequel to anime 2), not itself
        // a list entry. Anime 2 is my sole connection to it, and it's Dropped.
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Sequel" });
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 2, Title = "My dropped show" });
        db.AnimeRelatedAnime.Add(new AnimeRelatedAnime { AnimeId = 1, RelatedAnimeId = 2, RelationType = "prequel" });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 2, Status = WatchStatus.Dropped });
        db.AnimeUpdates.Add(new AnimeUpdate { AnimeId = 1, DetectedAt = DateTimeOffset.UtcNow, Kinds = AnimeUpdateKinds.Announced });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetHistoryAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task ASharedUniverseLinkNowQualifies()
    {
        using var db = CreateDb();
        // Anime 1 is not my own entry; its only connection to my list is a
        // "character" edge. Eligibility was widened past SeriesRelations
        // .TraversalSet by user request, so this now qualifies instead of
        // being excluded.
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Crossover Cameo" });
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 2, Title = "My show" });
        db.AnimeRelatedAnime.Add(new AnimeRelatedAnime { AnimeId = 1, RelatedAnimeId = 2, RelationType = "character", Title = "My show" });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 2, Status = WatchStatus.Watching });
        db.AnimeUpdates.Add(new AnimeUpdate { AnimeId = 1, DetectedAt = DateTimeOffset.UtcNow, Kinds = AnimeUpdateKinds.StartDateReleased });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetHistoryAsync();

        var dto = Assert.Single(result);
        Assert.Equal("Shares characters with My show", dto.Reason);
    }

    [Fact]
    public async Task AnAlternativeSettingLinkQualifies()
    {
        using var db = CreateDb();
        // Mirrors the real Fate/stay night -> Fate/strange Fake (Shin Series)
        // case: an "alternative_setting" edge, excluded from series-building
        // and from the original narrower eligibility rule, but included after
        // the widening.
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Fate/strange Fake (Shin Series)" });
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 2, Title = "Fate/stay night" });
        db.AnimeRelatedAnime.Add(new AnimeRelatedAnime { AnimeId = 1, RelatedAnimeId = 2, RelationType = "alternative_setting", Title = "Fate/stay night" });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 2, Status = WatchStatus.PlanToWatch });
        db.AnimeUpdates.Add(new AnimeUpdate { AnimeId = 1, DetectedAt = DateTimeOffset.UtcNow, Kinds = AnimeUpdateKinds.Announced });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetHistoryAsync();

        var dto = Assert.Single(result);
        Assert.Equal("Alternative setting to Fate/stay night", dto.Reason);
    }

    [Fact]
    public async Task AffiliatedUpdateNamesTheAffiliationAndInvertsTheRelation()
    {
        using var db = CreateDb();
        // Anime 1's own edge says anime 2 is its "prequel" — so anime 1 IS
        // the sequel of anime 2, and the reason should read "Sequel to <2>",
        // naming anime 2 by its English title rather than its MAL one
        // (design D4) — distinct on purpose, so a test passing by accident
        // (reading the wrong field) would fail.
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "New Season" });
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 2, Title = "Tensei shitara Ken deshita", EnglishTitle = "Original Show" });
        db.AnimeRelatedAnime.Add(new AnimeRelatedAnime { AnimeId = 1, RelatedAnimeId = 2, RelationType = "prequel", Title = "Tensei shitara Ken deshita" });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 2, Status = WatchStatus.Watching });
        db.AnimeUpdates.Add(new AnimeUpdate { AnimeId = 1, DetectedAt = DateTimeOffset.UtcNow, Kinds = AnimeUpdateKinds.Announced });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetHistoryAsync();

        var dto = Assert.Single(result);
        Assert.Equal("Sequel to Original Show", dto.Reason);
    }

    [Fact]
    public async Task AnAffiliateWithNoEnglishTitleFallsBackToItsMalTitle()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "New Season" });
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 2, Title = "Original Show" }); // no EnglishTitle
        db.AnimeRelatedAnime.Add(new AnimeRelatedAnime { AnimeId = 1, RelatedAnimeId = 2, RelationType = "prequel", Title = "Original Show" });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 2, Status = WatchStatus.Watching });
        db.AnimeUpdates.Add(new AnimeUpdate { AnimeId = 1, DetectedAt = DateTimeOffset.UtcNow, Kinds = AnimeUpdateKinds.Announced });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetHistoryAsync();

        var dto = Assert.Single(result);
        Assert.Equal("Sequel to Original Show", dto.Reason);
    }

    [Fact]
    public async Task AffiliationIsPreferredOverMyOwnEntryStatus()
    {
        using var db = CreateDb();
        // Anime 1 is both my own (non-dropped) entry AND affiliated with
        // anime 2 — the affiliation should win (spec: "the affiliation SHALL
        // be named" over the entry's own status).
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "New Season" });
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 2, Title = "Original Show" });
        db.AnimeRelatedAnime.Add(new AnimeRelatedAnime { AnimeId = 1, RelatedAnimeId = 2, RelationType = "prequel", Title = "Original Show" });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Status = WatchStatus.PlanToWatch });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 2, Status = WatchStatus.Watching });
        db.AnimeUpdates.Add(new AnimeUpdate { AnimeId = 1, DetectedAt = DateTimeOffset.UtcNow, Kinds = AnimeUpdateKinds.Announced });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetHistoryAsync();

        var dto = Assert.Single(result);
        Assert.Equal("Sequel to Original Show", dto.Reason);
    }

    [Fact]
    public async Task AddingTheAnimeSurfacesItsNewsWithoutReRecording()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Unaffiliated" });
        db.AnimeUpdates.Add(new AnimeUpdate { AnimeId = 1, DetectedAt = DateTimeOffset.UtcNow, Kinds = AnimeUpdateKinds.StartDateReleased });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        Assert.Empty(await service.GetHistoryAsync());

        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Status = WatchStatus.PlanToWatch });
        await db.SaveChangesAsync();

        var afterAdding = await service.GetHistoryAsync();
        var dto = Assert.Single(afterAdding);
        Assert.Single(await db.AnimeUpdates.ToListAsync()); // still just the one recorded row
        Assert.Equal(1, dto.AnimeId);
    }

    [Fact]
    public async Task AContradictedOnlyAffiliationHidesIt()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Disputed Sequel" });
        // LastSyncedAt set: RelationResolver only adjudicates an edge whose
        // far end has itself been full-detail fetched — an unfetched far end
        // carries no information, MAL or AniList, and resolves to Unknown
        // rather than Contradicted.
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 2, Title = "My show", LastSyncedAt = DateTimeOffset.UtcNow });
        db.AnimeRelatedAnime.Add(new AnimeRelatedAnime { AnimeId = 1, RelatedAnimeId = 2, RelationType = "prequel" });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 2, Status = WatchStatus.Watching });
        // Both ends known to AniList, with no corroborating AniList edge
        // between them at all — Adjudicate resolves this to Contradicted.
        db.AnimeAiringSyncs.Add(new AnimeAiringSync { AnimeId = 1, AniListId = 100, RelationsFetchedAt = DateTimeOffset.UtcNow });
        db.AnimeAiringSyncs.Add(new AnimeAiringSync { AnimeId = 2, AniListId = 200, RelationsFetchedAt = DateTimeOffset.UtcNow });
        db.AnimeUpdates.Add(new AnimeUpdate { AnimeId = 1, DetectedAt = DateTimeOffset.UtcNow, Kinds = AnimeUpdateKinds.Announced });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetHistoryAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task OrderingIsNewestFirst()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Solo" });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Status = WatchStatus.Watching });
        var now = DateTimeOffset.UtcNow;
        db.AnimeUpdates.Add(new AnimeUpdate { Id = 1, AnimeId = 1, DetectedAt = now.AddDays(-2), Kinds = AnimeUpdateKinds.StartDateChanged });
        db.AnimeUpdates.Add(new AnimeUpdate { Id = 2, AnimeId = 1, DetectedAt = now, Kinds = AnimeUpdateKinds.BroadcastSlotChanged });
        db.AnimeUpdates.Add(new AnimeUpdate { Id = 3, AnimeId = 1, DetectedAt = now.AddDays(-1), Kinds = AnimeUpdateKinds.EpisodesMoved });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetHistoryAsync();

        Assert.Equal([2, 3, 1], result.Select(r => r.Id));
    }

    [Fact]
    public async Task ADayOldUpdateIsExcludedByASinceCutoffButPresentInHistory()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Solo" });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Status = WatchStatus.Watching });
        var now = DateTimeOffset.UtcNow;
        db.AnimeUpdates.Add(new AnimeUpdate { AnimeId = 1, DetectedAt = now.AddDays(-31), Kinds = AnimeUpdateKinds.StartDateReleased });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var recent = await service.GetRecentAsync(now.AddDays(-30));
        var history = await service.GetHistoryAsync();

        Assert.Empty(recent);
        Assert.Single(history);
    }

    [Fact]
    public async Task DisplayedEpisodeCountFollowsALaterCorrectionWhileARecordedDateMoveDoesNot()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata
        {
            Id = 1,
            Title = "Solo",
            TotalEpisodes = 12,
            AiredFrom = new DateOnly(2024, 10, 12),
        });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Status = WatchStatus.Watching });
        db.AnimeUpdates.Add(new AnimeUpdate
        {
            AnimeId = 1,
            DetectedAt = DateTimeOffset.UtcNow,
            Kinds = AnimeUpdateKinds.EpisodeCountReleased | AnimeUpdateKinds.StartDateChanged,
            PreviousStartDate = new DateOnly(2024, 10, 5),
        });
        await db.SaveChangesAsync();

        // A later correction to the episode count, applied directly to the
        // stored row the way a later refresh would.
        var anime = await db.AnimeMetadata.SingleAsync(a => a.Id == 1);
        anime.TotalEpisodes = 13;
        await db.SaveChangesAsync();

        var dto = Assert.Single(await CreateService(db).GetHistoryAsync());

        Assert.Equal(13, dto.TotalEpisodes); // live value, follows the correction
        Assert.Equal(new DateOnly(2024, 10, 5), dto.PreviousStartDate); // recorded value, unaffected
    }

    // anime-updates spec, "A later correction does not rewrite an earlier
    // update" — could not be written before record-both-ends-of-a-schedule-move
    // stored the moved-to value on the row itself.
    [Fact]
    public async Task APremiereUpdateReportsThePairItWasRecordedForAfterTheAnimeMovesAgain()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata
        {
            Id = 1,
            Title = "Solo",
            AiredFrom = new DateOnly(2026, 11, 20), // the anime's premiere moved a second time
        });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Status = WatchStatus.Watching });
        db.AnimeUpdates.Add(new AnimeUpdate
        {
            AnimeId = 1,
            DetectedAt = DateTimeOffset.UtcNow,
            Kinds = AnimeUpdateKinds.StartDateChanged,
            PreviousStartDate = new DateOnly(2026, 10, 8),
            NewStartDate = new DateOnly(2026, 10, 1), // what this row recorded for its own move
        });
        await db.SaveChangesAsync();

        var dto = Assert.Single(await CreateService(db).GetHistoryAsync());

        Assert.Equal(new DateOnly(2026, 10, 8), dto.PreviousStartDate);
        Assert.Equal(new DateOnly(2026, 10, 1), dto.NewStartDate); // recorded pair, not the anime's current date
    }

    [Fact]
    public async Task ASlotUpdateReportsThePairItWasRecordedForAfterTheAnimeMovesAgain()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata
        {
            Id = 1,
            Title = "Solo",
            BroadcastDayOfWeek = "wednesdays", // the anime's slot moved a second time
            BroadcastTime = new TimeOnly(15, 0),
        });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Status = WatchStatus.Watching });
        db.AnimeUpdates.Add(new AnimeUpdate
        {
            AnimeId = 1,
            DetectedAt = DateTimeOffset.UtcNow,
            Kinds = AnimeUpdateKinds.BroadcastSlotChanged,
            PreviousBroadcastDayOfWeek = "mondays",
            PreviousBroadcastTime = new TimeOnly(12, 0),
            NewBroadcastDayOfWeek = "tuesdays", // what this row recorded for its own move
            NewBroadcastTime = new TimeOnly(13, 30),
        });
        await db.SaveChangesAsync();

        var dto = Assert.Single(await CreateServiceWithPassthroughConverter(db).GetHistoryAsync());

        Assert.Equal(DayOfWeek.Monday, dto.PreviousBroadcastDayOfWeek);
        Assert.Equal(new TimeOnly(12, 0), dto.PreviousBroadcastTime);
        Assert.Equal(DayOfWeek.Tuesday, dto.NewBroadcastDayOfWeek); // recorded pair, not the anime's current slot
        Assert.Equal(new TimeOnly(13, 30), dto.NewBroadcastTime);
    }

    // anime-updates spec, "An update recorded before both ends were stored" —
    // rows predating the backfill (or the deploy) have no recorded moved-to
    // value and must keep showing the anime's current one.
    [Fact]
    public async Task APremiereUpdateWithNoRecordedMovedToValueFallsBackToTheAnimesCurrentValue()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Solo", AiredFrom = new DateOnly(2026, 10, 1) });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Status = WatchStatus.Watching });
        db.AnimeUpdates.Add(new AnimeUpdate
        {
            AnimeId = 1,
            DetectedAt = DateTimeOffset.UtcNow,
            Kinds = AnimeUpdateKinds.StartDateChanged,
            PreviousStartDate = new DateOnly(2026, 10, 8),
            NewStartDate = null, // recorded before this change shipped
        });
        await db.SaveChangesAsync();

        var dto = Assert.Single(await CreateService(db).GetHistoryAsync());

        Assert.Equal(new DateOnly(2026, 10, 1), dto.NewStartDate); // falls back to the anime's current value
    }

    [Fact]
    public async Task ASlotUpdateWithNoRecordedMovedToValueFallsBackToTheAnimesCurrentValue()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata
        {
            Id = 1,
            Title = "Solo",
            BroadcastDayOfWeek = "tuesdays",
            BroadcastTime = new TimeOnly(13, 30),
        });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Status = WatchStatus.Watching });
        db.AnimeUpdates.Add(new AnimeUpdate
        {
            AnimeId = 1,
            DetectedAt = DateTimeOffset.UtcNow,
            Kinds = AnimeUpdateKinds.BroadcastSlotChanged,
            PreviousBroadcastDayOfWeek = "mondays",
            PreviousBroadcastTime = new TimeOnly(12, 0),
            NewBroadcastDayOfWeek = null, // recorded before this change shipped
            NewBroadcastTime = null,
        });
        await db.SaveChangesAsync();

        var dto = Assert.Single(await CreateServiceWithPassthroughConverter(db).GetHistoryAsync());

        Assert.Equal(DayOfWeek.Tuesday, dto.NewBroadcastDayOfWeek); // falls back to the anime's current slot
        Assert.Equal(new TimeOnly(13, 30), dto.NewBroadcastTime);
    }
}
