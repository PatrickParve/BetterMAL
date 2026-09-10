using System.Text.Json;
using System.Text.Json.Serialization;

namespace AnimeTracker.Api.Services.Transfer;

/// <summary>The export file's own serializer options, deliberately
/// independent of MVC's <c>JsonOptions</c> (Program.cs). The file's shape is
/// a contract with 04 and with future builds, and should not change when
/// someone adjusts the API's JSON settings (design.md D4).</summary>
public static class ExportJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        // No naming policy: ChangeType is written under the same name
        // ActivityLog.ChangeType's HasConversion<string>() stores it as
        // (e.g. "EpisodeIncremented"), never renamed or recased.
        Converters = { new JsonStringEnumConverter() },
        // The file is meant to be opened and checked by eye; at these row
        // counts (a few hundred KB at most) the size cost is irrelevant.
        WriteIndented = true,
    };
}
