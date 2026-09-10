using System.Text;
using System.Text.Json;
using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Transfer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AnimeTracker.Api.Tests.Services.Transfer;

// TransferFileReader (design.md D2 steps 1-3, D3, tasks.md 1.2-1.4): the
// file-shape refusals an import checks before anything about this device's
// own state, and the contract test pinning the reader to ExportService's
// real output.
public class TransferFileReaderTests
{
    private static byte[] Bytes(string json) => Encoding.UTF8.GetBytes(json);

    // --- Not an export file ---

    [Fact]
    public void Read_NotJsonIsRefused()
    {
        var ex = Assert.Throws<TransferFileRefusedException>(() => TransferFileReader.Read(Bytes("not json at all")));
        Assert.Contains("not a BetterMAL export file", ex.Message);
    }

    [Fact]
    public void Read_NotAnObjectIsRefused()
    {
        Assert.Throws<TransferFileRefusedException>(() => TransferFileReader.Read(Bytes("[1, 2, 3]")));
    }

    [Fact]
    public void Read_MissingFormatVersionIsRefused()
    {
        Assert.Throws<TransferFileRefusedException>(() => TransferFileReader.Read(Bytes("{}")));
    }

    [Fact]
    public void Read_NonIntegerFormatVersionIsRefused()
    {
        Assert.Throws<TransferFileRefusedException>(() => TransferFileReader.Read(Bytes("""{"formatVersion": "1"}""")));
    }

    [Fact]
    public void Read_ZeroFormatVersionIsRefused()
    {
        Assert.Throws<TransferFileRefusedException>(() => TransferFileReader.Read(Bytes("""{"formatVersion": 0}""")));
    }

    // --- A newer format ---

    [Fact]
    public void Read_NewerFormatIsRefused()
    {
        var ex = Assert.Throws<TransferFileRefusedException>(() => TransferFileReader.Read(Bytes("""{"formatVersion": 2}""")));
        Assert.Contains("format 2", ex.Message);
        Assert.Contains("format 1", ex.Message);
    }

    [Fact]
    public void Read_NewerFormatWithABrokenShapeIsRefusedAsNewerNotAsDamaged()
    {
        // A format-2 file whose shape has since changed must not be reached
        // by the deserializer at all — the version check runs first.
        var ex = Assert.Throws<TransferFileRefusedException>(
            () => TransferFileReader.Read(Bytes("""{"formatVersion": 2, "somethingElseEntirely": true}""")));
        Assert.Contains("format 2", ex.Message);
    }

    // --- Damaged ---

    [Fact]
    public void Read_AnActivityRecordWithNoIdIsRefusedNamingItsPath()
    {
        var json = MinimalValidFileWithOneActivity("""{"timestamp": "2026-01-01T00:00:00Z", "animeId": 1, "changeType": "Added", "changeDetail": null, "previousEpisodesWatched": null}""");

        var ex = Assert.Throws<TransferFileRefusedException>(() => TransferFileReader.Read(Bytes(json)));
        Assert.Contains("damaged", ex.Message);
    }

    [Fact]
    public void Read_ANullWhereTheFormatNeverWritesOneIsRefused()
    {
        var json = MinimalValidFileWithOneActivity(
            """{"id": "11111111-1111-1111-1111-111111111111", "timestamp": "2026-01-01T00:00:00Z", "animeId": null, "changeType": "Added", "changeDetail": null, "previousEpisodesWatched": null}""");

        Assert.Throws<TransferFileRefusedException>(() => TransferFileReader.Read(Bytes(json)));
    }

    // --- Ignoring what isn't understood ---

    [Fact]
    public void Read_AnUnknownMemberIsIgnored()
    {
        var json = MinimalValidFile().Replace(
            "\"formatVersion\": 1,",
            "\"formatVersion\": 1, \"somethingThisBuildDoesNotKnow\": \"whatever\",");

        var file = TransferFileReader.Read(Bytes(json));

        Assert.Equal(1, file.FormatVersion);
    }

    [Fact]
    public void Read_AnAbsentSeriesBlockIsToldApartFromAClearedOne()
    {
        var json = MinimalValidFileWithOneSeries(
            """{"seriesId": 5, "picture": {"value": null, "modifiedAt": "2026-01-01T00:00:00Z"}}""");

        var file = TransferFileReader.Read(Bytes(json));
        var series = Assert.Single(file.Series);

        Assert.Null(series.Title); // no block at all
        Assert.NotNull(series.Picture); // a present block, whose value happens to be a clear
        Assert.Null(series.Picture!.Value);
    }

    // --- Change type mapping (task 1.3) ---

    [Fact]
    public void Read_AnUnknownChangeTypeIsKeptWithItsRecord()
    {
        var json = MinimalValidFileWithOneActivity(
            """{"id": "11111111-1111-1111-1111-111111111111", "timestamp": "2026-01-01T00:00:00Z", "animeId": 1, "changeType": "SomethingFromTheFuture", "changeDetail": null, "previousEpisodesWatched": null}""");

        var file = TransferFileReader.Read(Bytes(json));

        var activity = Assert.Single(file.Activity);
        Assert.Equal("SomethingFromTheFuture", activity.ChangeType);
        Assert.Null(TransferFileReader.MapChangeType(activity.ChangeType));
    }

    [Theory]
    [InlineData("Added", ActivityChangeType.Added)]
    [InlineData("ScoreChanged", ActivityChangeType.ScoreChanged)]
    public void MapChangeType_MapsKnownNamesByExactCase(string name, ActivityChangeType expected)
    {
        Assert.Equal(expected, TransferFileReader.MapChangeType(name));
    }

    [Fact]
    public void MapChangeType_ANumericChangeTypeIsNotMapped()
    {
        // ActivityChangeType.Added is 0 — Enum.TryParse would otherwise
        // happily parse "0" into it.
        Assert.Null(TransferFileReader.MapChangeType("0"));
    }

    [Fact]
    public void MapChangeType_WrongCasingIsNotMapped()
    {
        Assert.Null(TransferFileReader.MapChangeType("added"));
    }

    // --- The contract test ---

    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    [Fact]
    public async Task Read_AnExportsRealOutputReadsBackFieldForField()
    {
        using var db = CreateDb();
        var deviceId = Guid.NewGuid();
        db.DeviceIdentities.Add(new DeviceIdentity { DeviceId = deviceId });

        var anime1 = new AnimeMetadata
        {
            Id = 1, Title = "Anime 1",
            SelectedPictureUrl = "https://mal/1.jpg", SelectedPictureModifiedAt = DateTimeOffset.UtcNow.AddDays(-1),
        };
        db.AnimeMetadata.Add(anime1);
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1 });
        db.Series.Add(new AnimeTracker.Api.Models.Series
        {
            Id = 1, BuiltAt = DateTimeOffset.UtcNow,
            SelectedTitle = "Custom Title", SelectedTitleModifiedAt = DateTimeOffset.UtcNow.AddHours(-2),
        });
        db.SeriesMembers.Add(new SeriesMember { SeriesId = 1, AnimeId = 1, IsMainLine = true, Order = 0 });
        db.ActivityLogs.Add(new ActivityLog
        {
            EventId = Guid.NewGuid(), Timestamp = DateTimeOffset.UtcNow.AddDays(-3), AnimeId = 1,
            ChangeType = ActivityChangeType.Added, ChangeDetail = "Added as Watching",
        });
        await db.SaveChangesAsync();

        var exportService = new ExportService(db, new TopAnimeSelectionRepository(db), new ActivityLogRepository(db));
        var (bytes, _) = await exportService.ExportAsync(userAgent: null);

        var file = TransferFileReader.Read(bytes);

        using var original = JsonDocument.Parse(bytes);
        Assert.Equal(original.RootElement.GetProperty("formatVersion").GetInt32(), file.FormatVersion);
        Assert.Equal(deviceId, file.Device.Id);
        Assert.Single(file.AnimePictures);
        Assert.Equal("https://mal/1.jpg", file.AnimePictures[0].SelectedPictureUrl);
        Assert.Single(file.Series);
        Assert.Equal("Custom Title", file.Series[0].Title?.Value);
        Assert.Null(file.Series[0].Picture); // never chosen, so no block at all
        Assert.Single(file.Activity);
        Assert.Equal(ActivityChangeType.Added, TransferFileReader.MapChangeType(file.Activity[0].ChangeType));
    }

    // --- Fixtures ---

    private static string MinimalValidFile() =>
        """
        {
          "formatVersion": 1,
          "device": {"id": "22222222-2222-2222-2222-222222222222", "name": "A Device"},
          "exportedAt": "2026-01-01T00:00:00Z",
          "ranking": {"animeIds": [], "modifiedAt": null},
          "animePictures": [],
          "series": [],
          "activity": []
        }
        """;

    private static string MinimalValidFileWithOneActivity(string activityJson) =>
        MinimalValidFile().Replace("\"activity\": []", $"\"activity\": [{activityJson}]");

    private static string MinimalValidFileWithOneSeries(string seriesJson) =>
        MinimalValidFile().Replace("\"series\": []", $"\"series\": [{seriesJson}]");
}
