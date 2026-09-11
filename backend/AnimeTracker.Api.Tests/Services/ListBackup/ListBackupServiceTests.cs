using System.Text.Json;
using System.Text.Json.Nodes;
using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.ListBackup;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Services.ListBackup;

// list-backup's ListBackupService (design.md D3-D4, tasks.md 3.1): asserts
// on the parsed JSON (JsonDocument), the file's own contract, the same
// reasoning ExportServiceTests uses for keeping the file's shape independent
// of the C# record types.
public class ListBackupServiceTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ListBackupService CreateService(AnimeTrackerDbContext db) => new(db);

    private static Guid SeedDevice(AnimeTrackerDbContext db)
    {
        var id = Guid.NewGuid();
        db.DeviceIdentities.Add(new DeviceIdentity { DeviceId = id });
        return id;
    }

    private static AnimeMetadata Anime(int id) => new() { Id = id, Title = $"Anime {id}" };

    private static async Task<JsonDocument> BackupAsync(AnimeTrackerDbContext db, string? userAgent = null)
    {
        var (bytes, _) = await CreateService(db).ExportAsync(userAgent);
        return JsonDocument.Parse(bytes);
    }

    // --- The file ---

    [Fact]
    public async Task TheTopLevelMembersAreExactlyTheFiveNamedInOrder()
    {
        using var db = CreateDb();
        SeedDevice(db);
        await db.SaveChangesAsync();

        using var doc = await BackupAsync(db);

        var names = doc.RootElement.EnumerateObject().Select(p => p.Name);
        Assert.Equal(["kind", "formatVersion", "device", "exportedAt", "entries"], names);
    }

    [Fact]
    public async Task TheHeaderNamesTheKindTheFormatTheDeviceAndTheMoment()
    {
        using var db = CreateDb();
        var deviceId = SeedDevice(db);
        await db.SaveChangesAsync();
        var before = DateTime.UtcNow;

        using var doc = await BackupAsync(
            db,
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.4 Safari/605.1.15");

        var after = DateTime.UtcNow;
        var root = doc.RootElement;
        Assert.Equal("listBackup", root.GetProperty("kind").GetString());
        Assert.Equal(1, root.GetProperty("formatVersion").GetInt32());
        Assert.Equal(deviceId, root.GetProperty("device").GetProperty("id").GetGuid());
        Assert.Equal("macOS · Safari", root.GetProperty("device").GetProperty("name").GetString());
        var exportedAt = root.GetProperty("exportedAt").GetDateTime();
        Assert.InRange(exportedAt, before.AddSeconds(-1), after.AddSeconds(1));
    }

    [Fact]
    public async Task NoSyncBookkeepingMemberAppearsAnywhereInTheOutput()
    {
        using var db = CreateDb();
        SeedDevice(db);
        db.AnimeMetadata.Add(Anime(1));
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, PendingSync = true, LastSyncedAt = DateTimeOffset.UtcNow, HeldForReviewAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var (bytes, _) = await CreateService(db).ExportAsync(null);
        var json = System.Text.Encoding.UTF8.GetString(bytes);

        Assert.DoesNotContain("pendingSync", json);
        Assert.DoesNotContain("lastSyncedAt", json);
        Assert.DoesNotContain("heldForReviewAt", json);
    }

    // --- Selection ---

    [Fact]
    public async Task OneEntryPerAnimeInMyListOrderedByAnimeId()
    {
        using var db = CreateDb();
        SeedDevice(db);
        db.AnimeMetadata.AddRange(Anime(3), Anime(1), Anime(2));
        db.UserAnimeEntries.AddRange(
            new UserAnimeEntry { AnimeId = 3 },
            new UserAnimeEntry { AnimeId = 1 },
            new UserAnimeEntry { AnimeId = 2 });
        await db.SaveChangesAsync();

        using var doc = await BackupAsync(db);

        var ids = doc.RootElement.GetProperty("entries").EnumerateArray().Select(e => e.GetProperty("animeId").GetInt32());
        Assert.Equal([1, 2, 3], ids);
    }

    [Fact]
    public async Task AnAnimeWithMetadataButNoEntryIsAbsent()
    {
        using var db = CreateDb();
        SeedDevice(db);
        db.AnimeMetadata.Add(Anime(1)); // browsed, never added to my list
        await db.SaveChangesAsync();

        using var doc = await BackupAsync(db);

        Assert.Empty(doc.RootElement.GetProperty("entries").EnumerateArray());
    }

    [Fact]
    public async Task AnUnsentEditAndAHeldEntryCarryTheirLocalValues()
    {
        using var db = CreateDb();
        SeedDevice(db);
        db.AnimeMetadata.AddRange(Anime(1), Anime(2));
        db.UserAnimeEntries.AddRange(
            new UserAnimeEntry { AnimeId = 1, EpisodesWatched = 4, PendingSync = true },
            new UserAnimeEntry { AnimeId = 2, Status = WatchStatus.Completed, HeldForReviewAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        using var doc = await BackupAsync(db);

        var entries = doc.RootElement.GetProperty("entries").EnumerateArray().ToList();
        Assert.Equal(4, entries[0].GetProperty("episodesWatched").GetInt32());
        Assert.Equal("Completed", entries[1].GetProperty("status").GetString());
    }

    [Fact]
    public async Task AnEmptyListGivesAnEmptyEntries()
    {
        using var db = CreateDb();
        SeedDevice(db);
        await db.SaveChangesAsync();

        using var doc = await BackupAsync(db);

        Assert.Equal(JsonValueKind.Array, doc.RootElement.GetProperty("entries").ValueKind);
        Assert.Empty(doc.RootElement.GetProperty("entries").EnumerateArray());
    }

    // --- Values ---

    [Fact]
    public async Task AllTenEntryMembersArePresentOnEveryEntry()
    {
        using var db = CreateDb();
        SeedDevice(db);
        db.AnimeMetadata.Add(Anime(1));
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1 });
        await db.SaveChangesAsync();

        using var doc = await BackupAsync(db);

        var entry = Assert.Single(doc.RootElement.GetProperty("entries").EnumerateArray());
        var names = entry.EnumerateObject().Select(p => p.Name);
        Assert.Equal(
            ["animeId", "title", "englishTitle", "totalEpisodes", "status", "episodesWatched", "score", "startedAt", "completedAt", "rewatchCount"],
            names);
    }

    [Fact]
    public async Task NullsAreWrittenForScoreDatesEnglishTitleAndTotalEpisodesWhereUnset()
    {
        using var db = CreateDb();
        SeedDevice(db);
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Anime 1", EnglishTitle = null, TotalEpisodes = null });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, MyScore = null, StartedAt = null, CompletedAt = null });
        await db.SaveChangesAsync();

        using var doc = await BackupAsync(db);

        var entry = Assert.Single(doc.RootElement.GetProperty("entries").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, entry.GetProperty("score").ValueKind);
        Assert.Equal(JsonValueKind.Null, entry.GetProperty("startedAt").ValueKind);
        Assert.Equal(JsonValueKind.Null, entry.GetProperty("completedAt").ValueKind);
        Assert.Equal(JsonValueKind.Null, entry.GetProperty("englishTitle").ValueKind);
        Assert.Equal(JsonValueKind.Null, entry.GetProperty("totalEpisodes").ValueKind);
    }

    [Fact]
    public async Task RewatchingAndPlanToWatchAreWrittenAsThoseStrings()
    {
        using var db = CreateDb();
        SeedDevice(db);
        db.AnimeMetadata.AddRange(Anime(1), Anime(2));
        db.UserAnimeEntries.AddRange(
            new UserAnimeEntry { AnimeId = 1, Status = WatchStatus.Rewatching },
            new UserAnimeEntry { AnimeId = 2, Status = WatchStatus.PlanToWatch });
        await db.SaveChangesAsync();

        using var doc = await BackupAsync(db);

        var statuses = doc.RootElement.GetProperty("entries").EnumerateArray().Select(e => e.GetProperty("status").GetString());
        Assert.Equal(["Rewatching", "PlanToWatch"], statuses);
    }

    [Fact]
    public async Task DatesAreWrittenAsYyyyMmDd()
    {
        using var db = CreateDb();
        SeedDevice(db);
        db.AnimeMetadata.Add(Anime(1));
        db.UserAnimeEntries.Add(new UserAnimeEntry
        {
            AnimeId = 1,
            StartedAt = new DateOnly(2026, 9, 11),
            CompletedAt = new DateOnly(2026, 9, 12),
        });
        await db.SaveChangesAsync();

        using var doc = await BackupAsync(db);

        var entry = Assert.Single(doc.RootElement.GetProperty("entries").EnumerateArray());
        Assert.Equal("2026-09-11", entry.GetProperty("startedAt").GetString());
        Assert.Equal("2026-09-12", entry.GetProperty("completedAt").GetString());
    }

    [Fact]
    public async Task ExportedAtEndsInZ()
    {
        using var db = CreateDb();
        SeedDevice(db);
        await db.SaveChangesAsync();

        using var doc = await BackupAsync(db);

        Assert.EndsWith("Z", doc.RootElement.GetProperty("exportedAt").GetString());
    }

    // --- Read-only ---

    [Fact]
    public async Task NoStoredEntryDiffersAfterABackupAndTheChangeTrackerHoldsNothing()
    {
        using var db = CreateDb();
        SeedDevice(db);
        db.AnimeMetadata.Add(Anime(1));
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, EpisodesWatched = 3 });
        await db.SaveChangesAsync();
        var before = (await db.UserAnimeEntries.AsNoTracking().SingleAsync()).EpisodesWatched;

        await CreateService(db).ExportAsync(null);

        Assert.False(db.ChangeTracker.HasChanges());
        Assert.Equal(before, (await db.UserAnimeEntries.AsNoTracking().SingleAsync()).EpisodesWatched);
    }

    [Fact]
    public async Task TwoBackupsWithNoChangeInBetweenDifferOnlyInExportedAt()
    {
        using var db = CreateDb();
        SeedDevice(db);
        db.AnimeMetadata.Add(Anime(1));
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, EpisodesWatched = 3 });
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var (firstBytes, _) = await service.ExportAsync(null);
        var (secondBytes, _) = await service.ExportAsync(null);

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
