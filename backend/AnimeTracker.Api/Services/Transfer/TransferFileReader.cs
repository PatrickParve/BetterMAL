using System.Text.Json;
using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Services.Transfer;

/// <summary>Reads an export file into a <see cref="TransferFile"/>, applying
/// the file-shape refusals of design.md D2 (steps 1-3; step 4, refusing a
/// file exported from this very device, is <c>ITransferImportService</c>'s,
/// since it needs database state to decide). Deliberately its own reading,
/// not a reuse of <c>ExportFile</c>'s records (D3 Alternatives): those carry
/// no notion of a required member, and one change type this build doesn't
/// know would fail deserializing the whole file through
/// <c>ExportActivity</c>'s enum-typed <c>ChangeType</c>.</summary>
public static class TransferFileReader
{
    public const int ReadableFormatVersion = 1;

    private static readonly JsonSerializerOptions Options = new(ExportJson.Options)
    {
        // A missing member is refused rather than silently defaulted;
        // JsonException.Path names it. TransferSeries.Title/Picture opt out
        // via their own default value, since a series block may legitimately
        // be absent (design.md D3 "Absent versus cleared series blocks").
        RespectRequiredConstructorParameters = true,
        // A null where the format never writes one (e.g. a non-nullable
        // member written null) is refused rather than silently accepted.
        RespectNullableAnnotations = true,
    };

    /// <summary>Throws <see cref="TransferFileRefusedException"/> for D2
    /// steps 1-3. Unknown members are ignored — a later build may add one an
    /// older import can safely skip, without raising <c>formatVersion</c>.</summary>
    public static TransferFile Read(byte[] bytes)
    {
        var formatVersion = PeekFormatVersion(bytes);

        if (formatVersion > ReadableFormatVersion)
            throw new TransferFileRefusedException(
                $"This file is format {formatVersion}; this build reads format {ReadableFormatVersion}. Update this device first.");

        try
        {
            return JsonSerializer.Deserialize<TransferFile>(bytes, Options)
                ?? throw new TransferFileRefusedException("This is not a BetterMAL export file.");
        }
        catch (JsonException ex)
        {
            throw new TransferFileRefusedException(
                ex.Path is { } path ? $"The file is damaged: '{path}' is missing or invalid." : "The file is damaged.");
        }
    }

    /// <summary>Reads <c>formatVersion</c> alone, ahead of the full
    /// deserialize, so a newer format is refused as newer rather than as
    /// damaged even when the rest of its shape has since changed (design.md
    /// D2: the version is checked before the rest of the file's shape).</summary>
    private static int PeekFormatVersion(byte[] bytes)
    {
        using JsonDocument document = ParseOrRefuse(bytes);

        if (document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty("formatVersion", out var versionElement)
            || versionElement.ValueKind != JsonValueKind.Number
            || !versionElement.TryGetInt32(out var formatVersion)
            || formatVersion < 1)
            throw new TransferFileRefusedException("This is not a BetterMAL export file.");

        return formatVersion;
    }

    private static JsonDocument ParseOrRefuse(byte[] bytes)
    {
        try
        {
            return JsonDocument.Parse(bytes);
        }
        catch (JsonException)
        {
            throw new TransferFileRefusedException("This is not a BetterMAL export file.");
        }
    }

    /// <summary>Maps a change type by exact name (design.md D3). Numeric
    /// strings are rejected even though <see cref="Enum.TryParse{TEnum}(string?, out TEnum)"/>
    /// would otherwise accept one matching a defined value's underlying
    /// number. An unmapped name returns null so its record can stay in the
    /// file rather than refusing the whole import — <c>ActivityChangeType</c>
    /// is append-only, and a value a later build added doesn't change the
    /// file's shape.</summary>
    public static ActivityChangeType? MapChangeType(string changeType) =>
        !int.TryParse(changeType, out _) && Enum.TryParse<ActivityChangeType>(changeType, out var result)
            ? result
            : null;
}
