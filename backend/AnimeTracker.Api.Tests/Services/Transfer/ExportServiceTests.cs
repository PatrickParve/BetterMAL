using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Transfer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AnimeTracker.Api.Tests.Services.Transfer;

// device-transfer's ExportService (design.md D1-D9, tasks.md 5.5): asserts
// on the parsed JSON (JsonDocument), the file's own contract, the same
// reasoning as design.md D4 for keeping the file's shape independent of the
// C# record types. TransactionIgnoredWarning is suppressed the same way
// SeriesGraphBuilderReRootTests does, since ExportService opens an explicit
// transaction (design.md D2) and InMemory otherwise rejects it.
public class ExportServiceTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static ExportService CreateService(AnimeTrackerDbContext db) =>
        new(db, new TopAnimeSelectionRepository(db), new ActivityLogRepository(db));

    private static Guid SeedDevice(AnimeTrackerDbContext db)
    {
        var id = Guid.NewGuid();
        db.DeviceIdentities.Add(new DeviceIdentity { DeviceId = id });
        return id;
    }

    private static AnimeMetadata Anime(int id) => new() { Id = id, Title = $"Anime {id}" };

    private static async Task<JsonDocument> ExportAsync(AnimeTrackerDbContext db, string? userAgent = null)
    {
        var (bytes, _) = await CreateService(db).ExportAsync(userAgent);
        return JsonDocument.Parse(bytes);
    }

    // --- Header ---

    [Fact]
    public async Task TheTopLevelMembersAreExactlyTheSevenNamed()
    {
        using var db = CreateDb();
        SeedDevice(db);
        await db.SaveChangesAsync();

        using var doc = await ExportAsync(db);

        var names = doc.RootElement.EnumerateObject().Select(p => p.Name);
        Assert.Equal(
            ["formatVersion", "device", "exportedAt", "ranking", "animePictures", "series", "activity"],
            names);
    }

    [Fact]
    public async Task TheHeaderNamesTheFormatTheDeviceAndTheMoment()
    {
        using var db = CreateDb();
        var deviceId = SeedDevice(db);
        await db.SaveChangesAsync();
        var before = DateTime.UtcNow;

        using var doc = await ExportAsync(
            db,
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.4 Safari/605.1.15");

        var after = DateTime.UtcNow;
        var root = doc.RootElement;
        Assert.Equal(1, root.GetProperty("formatVersion").GetInt32());
        Assert.Equal(deviceId, root.GetProperty("device").GetProperty("id").GetGuid());
        Assert.Equal("macOS · Safari", root.GetProperty("device").GetProperty("name").GetString());
        var exportedAt = root.GetProperty("exportedAt").GetDateTime();
        Assert.InRange(exportedAt, before.AddSeconds(-1), after.AddSeconds(1));
    }

    [Fact]
    public async Task DeviceNameIsPresentAsNullForAnUnrecognisedAgent()
    {
        using var db = CreateDb();
        SeedDevice(db);
        await db.SaveChangesAsync();

        using var doc = await ExportAsync(db, "SomeUnknownBot/1.0");

        var device = doc.RootElement.GetProperty("device");
        Assert.True(device.TryGetProperty("name", out var name));
        Assert.Equal(JsonValueKind.Null, name.ValueKind);
    }

    // --- Ranking ---

    [Fact]
    public async Task RankingCarriesTheStoredOrderAndTime()
    {
        using var db = CreateDb();
        SeedDevice(db);
        db.AnimeMetadata.AddRange(Anime(1), Anime(2), Anime(3)); // A=1, B=2, C=3
        await db.SaveChangesAsync();
        var time = DateTimeOffset.UtcNow.AddDays(-2);
        await new TopAnimeSelectionRepository(db).ReplaceAllAsync([3, 1, 2], time); // [C, A, B]

        using var doc = await ExportAsync(db);

        var ranking = doc.RootElement.GetProperty("ranking");
        Assert.Equal([3, 1, 2], ranking.GetProperty("animeIds").EnumerateArray().Select(e => e.GetInt32()));
        Assert.Equal(time.UtcDateTime, ranking.GetProperty("modifiedAt").GetDateTime());
    }

    [Fact]
    public async Task ANeverArrangedRankingHasANullTimeAndAnEmptyList()
    {
        using var db = CreateDb();
        SeedDevice(db);
        await db.SaveChangesAsync();

        using var doc = await ExportAsync(db);

        var ranking = doc.RootElement.GetProperty("ranking");
        Assert.Empty(ranking.GetProperty("animeIds").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, ranking.GetProperty("modifiedAt").ValueKind);
    }

    [Fact]
    public async Task AnEmptiedRankingKeepsItsTime()
    {
        using var db = CreateDb();
        SeedDevice(db);
        db.AnimeMetadata.Add(Anime(1));
        await db.SaveChangesAsync();
        var time = DateTimeOffset.UtcNow.AddDays(-1);
        await new TopAnimeSelectionRepository(db).ReplaceAllAsync([], time);

        using var doc = await ExportAsync(db);

        var ranking = doc.RootElement.GetProperty("ranking");
        Assert.Empty(ranking.GetProperty("animeIds").EnumerateArray());
        Assert.Equal(time.UtcDateTime, ranking.GetProperty("modifiedAt").GetDateTime());
    }

    // --- Pictures ---

    [Fact]
    public async Task ASetPictureChoiceIsExported()
    {
        using var db = CreateDb();
        SeedDevice(db);
        var time = DateTimeOffset.UtcNow.AddDays(-3);
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Anime 1", SelectedPictureUrl = "https://example.com/1.jpg", SelectedPictureModifiedAt = time });
        await db.SaveChangesAsync();

        using var doc = await ExportAsync(db);

        var entry = Assert.Single(doc.RootElement.GetProperty("animePictures").EnumerateArray());
        Assert.Equal(1, entry.GetProperty("animeId").GetInt32());
        Assert.Equal("https://example.com/1.jpg", entry.GetProperty("selectedPictureUrl").GetString());
        Assert.Equal(time.UtcDateTime, entry.GetProperty("modifiedAt").GetDateTime());
    }

    [Fact]
    public async Task AClearedPictureChoiceIsExportedWithANullUrl()
    {
        using var db = CreateDb();
        SeedDevice(db);
        var time = DateTimeOffset.UtcNow.AddDays(-1);
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Anime 1", SelectedPictureUrl = null, SelectedPictureModifiedAt = time });
        await db.SaveChangesAsync();

        using var doc = await ExportAsync(db);

        var entry = Assert.Single(doc.RootElement.GetProperty("animePictures").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, entry.GetProperty("selectedPictureUrl").ValueKind);
        Assert.Equal(time.UtcDateTime, entry.GetProperty("modifiedAt").GetDateTime());
    }

    [Fact]
    public async Task ANeverChosenPictureHasNoEntry()
    {
        using var db = CreateDb();
        SeedDevice(db);
        db.AnimeMetadata.Add(Anime(1));
        await db.SaveChangesAsync();

        using var doc = await ExportAsync(db);

        Assert.Empty(doc.RootElement.GetProperty("animePictures").EnumerateArray());
    }

    [Fact]
    public async Task AStampedChoiceOnAnAnimeWithNoListEntryIsExported()
    {
        using var db = CreateDb();
        SeedDevice(db);
        var time = DateTimeOffset.UtcNow.AddDays(-1);
        // No UserAnimeEntry row: this anime is not in my list.
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Anime 1", SelectedPictureUrl = "https://example.com/1.jpg", SelectedPictureModifiedAt = time });
        await db.SaveChangesAsync();

        using var doc = await ExportAsync(db);

        var entry = Assert.Single(doc.RootElement.GetProperty("animePictures").EnumerateArray());
        Assert.Equal(1, entry.GetProperty("animeId").GetInt32());
    }

    [Fact]
    public async Task PictureEntriesAreOrderedByAnimeId()
    {
        using var db = CreateDb();
        SeedDevice(db);
        var time = DateTimeOffset.UtcNow;
        db.AnimeMetadata.AddRange(
            new AnimeMetadata { Id = 3, Title = "Anime 3", SelectedPictureUrl = "u3", SelectedPictureModifiedAt = time },
            new AnimeMetadata { Id = 1, Title = "Anime 1", SelectedPictureUrl = "u1", SelectedPictureModifiedAt = time },
            new AnimeMetadata { Id = 2, Title = "Anime 2", SelectedPictureUrl = "u2", SelectedPictureModifiedAt = time });
        await db.SaveChangesAsync();

        using var doc = await ExportAsync(db);

        var ids = doc.RootElement.GetProperty("animePictures").EnumerateArray().Select(e => e.GetProperty("animeId").GetInt32());
        Assert.Equal([1, 2, 3], ids);
    }

    // --- Series ---

    [Fact]
    public async Task ASeriesWithOnlyATitleChoiceHasNoPictureProperty()
    {
        using var db = CreateDb();
        SeedDevice(db);
        var time = DateTimeOffset.UtcNow.AddDays(-1);
        db.Series.Add(new AnimeTracker.Api.Models.Series { Id = 1, SelectedTitle = "My Title", SelectedTitleModifiedAt = time });
        await db.SaveChangesAsync();

        using var doc = await ExportAsync(db);

        var entry = Assert.Single(doc.RootElement.GetProperty("series").EnumerateArray());
        Assert.Equal(1, entry.GetProperty("seriesId").GetInt32());
        var title = entry.GetProperty("title");
        Assert.Equal("My Title", title.GetProperty("value").GetString());
        Assert.Equal(time.UtcDateTime, title.GetProperty("modifiedAt").GetDateTime());
        Assert.False(entry.TryGetProperty("picture", out _));
    }

    [Fact]
    public async Task ASeriesWithBothChoicesCarriesBothBlocksWithTheirOwnTimes()
    {
        using var db = CreateDb();
        SeedDevice(db);
        var titleTime = DateTimeOffset.UtcNow.AddDays(-2);
        var pictureTime = DateTimeOffset.UtcNow.AddDays(-1);
        db.Series.Add(new AnimeTracker.Api.Models.Series
        {
            Id = 1,
            SelectedTitle = "My Title",
            SelectedTitleModifiedAt = titleTime,
            SelectedPictureUrl = "https://example.com/p.jpg",
            SelectedPictureModifiedAt = pictureTime,
        });
        await db.SaveChangesAsync();

        using var doc = await ExportAsync(db);

        var entry = Assert.Single(doc.RootElement.GetProperty("series").EnumerateArray());
        Assert.Equal(titleTime.UtcDateTime, entry.GetProperty("title").GetProperty("modifiedAt").GetDateTime());
        Assert.Equal(pictureTime.UtcDateTime, entry.GetProperty("picture").GetProperty("modifiedAt").GetDateTime());
    }

    [Fact]
    public async Task ASeriesWithAClearedPictureHasANullValueAndNoTitleProperty()
    {
        using var db = CreateDb();
        SeedDevice(db);
        var time = DateTimeOffset.UtcNow.AddDays(-1);
        db.Series.Add(new AnimeTracker.Api.Models.Series { Id = 1, SelectedPictureUrl = null, SelectedPictureModifiedAt = time });
        await db.SaveChangesAsync();

        using var doc = await ExportAsync(db);

        var entry = Assert.Single(doc.RootElement.GetProperty("series").EnumerateArray());
        var picture = entry.GetProperty("picture");
        Assert.Equal(JsonValueKind.Null, picture.GetProperty("value").ValueKind);
        Assert.False(entry.TryGetProperty("title", out _));
    }

    [Fact]
    public async Task ASeriesWithNoChoicesHasNoEntry()
    {
        using var db = CreateDb();
        SeedDevice(db);
        db.Series.Add(new AnimeTracker.Api.Models.Series { Id = 1 });
        await db.SaveChangesAsync();

        using var doc = await ExportAsync(db);

        Assert.Empty(doc.RootElement.GetProperty("series").EnumerateArray());
    }

    [Fact]
    public async Task SeriesEntriesAreOrderedBySeriesId()
    {
        using var db = CreateDb();
        SeedDevice(db);
        var time = DateTimeOffset.UtcNow;
        db.Series.AddRange(
            new AnimeTracker.Api.Models.Series { Id = 3, SelectedTitle = "C", SelectedTitleModifiedAt = time },
            new AnimeTracker.Api.Models.Series { Id = 1, SelectedTitle = "A", SelectedTitleModifiedAt = time },
            new AnimeTracker.Api.Models.Series { Id = 2, SelectedTitle = "B", SelectedTitleModifiedAt = time });
        await db.SaveChangesAsync();

        using var doc = await ExportAsync(db);

        var ids = doc.RootElement.GetProperty("series").EnumerateArray().Select(e => e.GetProperty("seriesId").GetInt32());
        Assert.Equal([1, 2, 3], ids);
    }

    // --- Activity ---

    [Fact]
    public async Task ActivityOrderHoldsIncludingThreeRecordsSharingOneTimestamp()
    {
        using var db = CreateDb();
        SeedDevice(db);
        db.AnimeMetadata.Add(Anime(1));
        var earlier = DateTimeOffset.UtcNow.AddDays(-1);
        var shared = DateTimeOffset.UtcNow;
        var first = new ActivityLog { AnimeId = 1, Timestamp = earlier, ChangeType = ActivityChangeType.Added };
        var second = new ActivityLog { AnimeId = 1, Timestamp = shared, ChangeType = ActivityChangeType.EpisodeIncremented };
        var third = new ActivityLog { AnimeId = 1, Timestamp = shared, ChangeType = ActivityChangeType.ScoreChanged };
        var fourth = new ActivityLog { AnimeId = 1, Timestamp = shared, ChangeType = ActivityChangeType.Completed };
        db.ActivityLogs.AddRange(first, second, third, fourth);
        await db.SaveChangesAsync();

        using var doc = await ExportAsync(db);

        var ids = doc.RootElement.GetProperty("activity").EnumerateArray().Select(e => e.GetProperty("id").GetGuid());
        Assert.Equal([first.EventId, second.EventId, third.EventId, fourth.EventId], ids);
    }

    [Fact]
    public async Task ActivityIdEqualsEventIdAndNoPropertyHoldsTheLocalId()
    {
        using var db = CreateDb();
        SeedDevice(db);
        db.AnimeMetadata.Add(Anime(1));
        var log = new ActivityLog { AnimeId = 1, Timestamp = DateTimeOffset.UtcNow, ChangeType = ActivityChangeType.Added };
        db.ActivityLogs.Add(log);
        await db.SaveChangesAsync();

        using var doc = await ExportAsync(db);

        var entry = Assert.Single(doc.RootElement.GetProperty("activity").EnumerateArray());
        Assert.Equal(log.EventId, entry.GetProperty("id").GetGuid());
        var propertyNames = entry.EnumerateObject().Select(p => p.Name);
        Assert.Equal(["id", "timestamp", "animeId", "changeType", "changeDetail", "previousEpisodesWatched"], propertyNames);
    }

    [Fact]
    public async Task ChangeTypeIsWrittenAsTheStoredName()
    {
        using var db = CreateDb();
        SeedDevice(db);
        db.AnimeMetadata.Add(Anime(1));
        db.ActivityLogs.Add(new ActivityLog { AnimeId = 1, Timestamp = DateTimeOffset.UtcNow, ChangeType = ActivityChangeType.EpisodeIncremented });
        await db.SaveChangesAsync();

        using var doc = await ExportAsync(db);

        var entry = Assert.Single(doc.RootElement.GetProperty("activity").EnumerateArray());
        Assert.Equal("EpisodeIncremented", entry.GetProperty("changeType").GetString());
    }

    [Fact]
    public async Task ChangeDetailAndPreviousEpisodesWatchedArePresentAsNullWhereEmpty()
    {
        using var db = CreateDb();
        SeedDevice(db);
        db.AnimeMetadata.Add(Anime(1));
        db.ActivityLogs.Add(new ActivityLog { AnimeId = 1, Timestamp = DateTimeOffset.UtcNow, ChangeType = ActivityChangeType.Added, ChangeDetail = null, PreviousEpisodesWatched = null });
        await db.SaveChangesAsync();

        using var doc = await ExportAsync(db);

        var entry = Assert.Single(doc.RootElement.GetProperty("activity").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, entry.GetProperty("changeDetail").ValueKind);
        Assert.Equal(JsonValueKind.Null, entry.GetProperty("previousEpisodesWatched").ValueKind);
    }

    [Fact]
    public async Task ARemovedAnimesRecordsAreIncluded()
    {
        using var db = CreateDb();
        SeedDevice(db);
        db.AnimeMetadata.Add(Anime(1));
        db.ActivityLogs.AddRange(
            new ActivityLog { AnimeId = 1, Timestamp = DateTimeOffset.UtcNow.AddDays(-1), ChangeType = ActivityChangeType.EpisodeIncremented },
            new ActivityLog { AnimeId = 1, Timestamp = DateTimeOffset.UtcNow, ChangeType = ActivityChangeType.Removed });
        await db.SaveChangesAsync();

        using var doc = await ExportAsync(db);

        var changeTypes = doc.RootElement.GetProperty("activity").EnumerateArray().Select(e => e.GetProperty("changeType").GetString());
        Assert.Contains("Removed", changeTypes);
    }

    // --- Values ---

    [Fact]
    public async Task AMicrosecondTimeSurvives()
    {
        using var db = CreateDb();
        SeedDevice(db);
        var time = new DateTimeOffset(2026, 9, 9, 21, 26, 43, TimeSpan.Zero).AddTicks(9_738_290); // .973829 seconds
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Anime 1", SelectedPictureUrl = "u", SelectedPictureModifiedAt = time });
        await db.SaveChangesAsync();

        var (bytes, _) = await CreateService(db).ExportAsync(null);
        var json = Encoding.UTF8.GetString(bytes);

        Assert.Contains("2026-09-09T21:26:43.973829Z", json);
    }

    [Fact]
    public async Task NoTokenTitleOrMalPictureAppearsAnywhereInTheOutput()
    {
        using var db = CreateDb();
        SeedDevice(db);
        db.OAuthTokens.Add(new OAuthToken { AccessToken = "super-secret-access-token", RefreshToken = "super-secret-refresh-token", ExpiresAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
        db.AnimeMetadata.Add(new AnimeMetadata
        {
            Id = 1,
            Title = "A Title From MyAnimeList",
            MalPictureUrl = "https://mal.example.com/picture.jpg",
            SelectedPictureUrl = "https://mine.example.com/picture.jpg",
            SelectedPictureModifiedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var (bytes, _) = await CreateService(db).ExportAsync(null);
        var json = Encoding.UTF8.GetString(bytes);

        Assert.DoesNotContain("super-secret-access-token", json);
        Assert.DoesNotContain("super-secret-refresh-token", json);
        Assert.DoesNotContain("A Title From MyAnimeList", json);
        Assert.DoesNotContain("mal.example.com", json);
    }

    // --- Read-only ---

    [Fact]
    public async Task ExportingLeavesTheContextWithNoPendingChangesAndTheStoredDataUnchanged()
    {
        using var db = CreateDb();
        var deviceId = SeedDevice(db);
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Anime 1", SelectedPictureUrl = "u", SelectedPictureModifiedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        var repository = new TopAnimeSelectionRepository(db);
        await repository.ReplaceAllAsync([1], DateTimeOffset.UtcNow);
        db.ActivityLogs.Add(new ActivityLog { AnimeId = 1, Timestamp = DateTimeOffset.UtcNow, ChangeType = ActivityChangeType.Added });
        await db.SaveChangesAsync();
        var rankingTimeBefore = await repository.GetModifiedAtAsync();
        var pictureBefore = (await db.AnimeMetadata.AsNoTracking().SingleAsync()).SelectedPictureUrl;
        var logCountBefore = await db.ActivityLogs.CountAsync();

        await CreateService(db).ExportAsync(null);

        Assert.False(db.ChangeTracker.HasChanges());
        Assert.Equal(rankingTimeBefore, await repository.GetModifiedAtAsync());
        Assert.Equal(pictureBefore, (await db.AnimeMetadata.AsNoTracking().SingleAsync()).SelectedPictureUrl);
        Assert.Equal(logCountBefore, await db.ActivityLogs.CountAsync());
        Assert.Equal(deviceId, (await db.DeviceIdentities.AsNoTracking().SingleAsync()).DeviceId);
    }

    // --- Deterministic ---

    [Fact]
    public async Task TwoExportsWithTheSameUserAgentDifferOnlyInExportedAt()
    {
        using var db = CreateDb();
        SeedDevice(db);
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Anime 1", SelectedPictureUrl = "u", SelectedPictureModifiedAt = DateTimeOffset.UtcNow.AddDays(-1) });
        await db.SaveChangesAsync();
        var service = CreateService(db);
        const string userAgent = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.4 Safari/605.1.15";

        var (firstBytes, _) = await service.ExportAsync(userAgent);
        var (secondBytes, _) = await service.ExportAsync(userAgent);

        Assert.Equal(WithoutExportedAt(firstBytes), WithoutExportedAt(secondBytes));
        using var first = JsonDocument.Parse(firstBytes);
        using var second = JsonDocument.Parse(secondBytes);
        Assert.NotEqual(
            first.RootElement.GetProperty("exportedAt").GetDateTime(),
            second.RootElement.GetProperty("exportedAt").GetDateTime());
    }

    private static string WithoutExportedAt(byte[] bytes)
    {
        var node = JsonNode.Parse(bytes)!.AsObject();
        node.Remove("exportedAt");
        return node.ToJsonString();
    }
}
