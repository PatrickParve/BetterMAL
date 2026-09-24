using System.Text.Json;
using System.Text.Json.Serialization;
using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Services.IdMapping;

/// <summary>One entry of the custom id-mapping file (design.md D19, spec
/// `external-id-mapping`): an entry shaped like the source file's, plus two
/// fields of its own. Declared apart from <see cref="FribbEntry"/> so that
/// nothing the downloaded file might one day carry under these names could
/// change how that file is read.</summary>
public class CustomIdMappingEntry : FribbEntry
{
    /// <summary>True: the entry wins over the synced mapping for every group it
    /// provides. False or absent: it only fills a group the synced mapping has
    /// nothing for.</summary>
    [JsonPropertyName("override")]
    public bool? Override { get; set; }

    /// <summary>Free text for whoever edits the file: which show this is and
    /// why it is here. Quoted in log messages, and used for nothing
    /// else.</summary>
    [JsonPropertyName("note")]
    public string? Note { get; set; }
}

/// <summary>A valid custom entry, ready to merge: the ids it provides, held as
/// a mapping, and how they apply.</summary>
public sealed record CustomIdMapping(AnimeIdMapping Mapping, bool Override, string? Note);

/// <summary>The hand-edited custom id mapping (design.md D19).</summary>
public interface ICustomIdMappings
{
    /// <summary>The valid entries by MAL id, as of the file's latest readable
    /// version. Empty when there is no file. Cheap to call: the file is only
    /// looked at again after a couple of seconds, and only re-read when it
    /// changed.</summary>
    IReadOnlyDictionary<int, CustomIdMapping> Current { get; }

    /// <summary>The file being read, or the one that would be read once it
    /// exists. For log messages.</summary>
    string FilePath { get; }
}

/// <summary>Reads the custom id-mapping file: a JSON array of entries shaped
/// like the source file's (spec `external-id-mapping` "A custom mapping file
/// supplements and corrects the synced mapping"). It is written by a person, so
/// it is read to be forgiven: comments and trailing commas are accepted, an
/// invalid entry costs only itself, and a file that does not parse leaves the
/// entries last read in force, so a typo can never silently switch every
/// override off. The file is looked at again after at most a few seconds and
/// re-read only when its timestamp or length changed, so an edit applies
/// without a restart.</summary>
public sealed class CustomIdMappings : ICustomIdMappings
{
    /// <summary>How long the file is trusted not to have changed. Every read of
    /// the mapping goes through <see cref="Current"/>, and a stat per page load
    /// is more often than an edit needs.</summary>
    public static readonly TimeSpan DefaultCheckInterval = TimeSpan.FromSeconds(2);

    private static readonly IReadOnlyDictionary<int, CustomIdMapping> NoEntries = new Dictionary<int, CustomIdMapping>();

    // A person edits this file: allow the two things editors and people add.
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    private static readonly JsonSerializerOptions EntryOptions = new()
    {
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    // A signature no file can have, stored after a failed read that another
    // read might cure (the editor was still writing), so the next look tries
    // again rather than waiting for a change that may never come.
    private static readonly (DateTime LastWriteUtc, long Length) RetryNextTime = (DateTime.MinValue, -1);

    private readonly IReadOnlyList<string> candidates;
    private readonly TimeSpan checkInterval;
    private readonly ILogger<CustomIdMappings> logger;
    private readonly object gate = new();

    private IReadOnlyDictionary<int, CustomIdMapping> current = NoEntries;
    private bool loaded;
    private long checkedAt;
    private string? loadedFrom;
    private (DateTime LastWriteUtc, long Length)? loadedSignature;

    public CustomIdMappings(IReadOnlyList<string> candidatePaths, ILogger<CustomIdMappings> logger, TimeSpan? checkInterval = null)
    {
        if (candidatePaths.Count == 0)
            throw new ArgumentException("At least one candidate path is required.", nameof(candidatePaths));

        candidates = candidatePaths;
        this.logger = logger;
        this.checkInterval = checkInterval ?? DefaultCheckInterval;
    }

    /// <summary>Where to look. The configured file alone when there is one.
    /// Otherwise <c>custom/id-mapping.json</c> in the working directory and in
    /// the two folders above it, so a run from the project folder
    /// (<c>backend/AnimeTracker.Api</c>) finds <c>backend/custom</c>, and the
    /// container (working directory <c>/app</c>) finds the folder its image was
    /// built with.</summary>
    public static IReadOnlyList<string> CandidatePaths(string? configured, string workingDirectory)
    {
        if (!string.IsNullOrWhiteSpace(configured))
            return [Path.GetFullPath(configured, workingDirectory)];

        var paths = new List<string>();
        for (var dir = new DirectoryInfo(workingDirectory); dir is not null && paths.Count < 3; dir = dir.Parent)
            paths.Add(Path.Combine(dir.FullName, "custom", "id-mapping.json"));

        return paths;
    }

    public string FilePath => candidates.FirstOrDefault(File.Exists) ?? candidates[0];

    public IReadOnlyDictionary<int, CustomIdMapping> Current
    {
        get
        {
            lock (gate)
            {
                var now = Environment.TickCount64;
                if (!loaded || now - checkedAt >= checkInterval.TotalMilliseconds)
                {
                    checkedAt = now;
                    LookAtFile();
                }

                return current;
            }
        }
    }

    private void LookAtFile()
    {
        var path = FilePath;
        (DateTime, long)? signature = null;
        if (File.Exists(path))
        {
            var info = new FileInfo(path);
            signature = (info.LastWriteTimeUtc, info.Length);
        }

        if (loaded && path == loadedFrom && signature == loadedSignature)
            return;

        var hadEntries = current.Count > 0;
        loaded = true;
        loadedFrom = path;
        loadedSignature = signature;

        if (signature is null)
        {
            // The file is optional, so a start without one says nothing. A file
            // that disappears while running is worth a line: its entries go.
            if (hadEntries)
                logger.LogInformation("The custom id mapping file {Path} is gone; its {Count} entries no longer apply.", path, current.Count);
            current = NoEntries;
            return;
        }

        try
        {
            current = Read(path);
        }
        catch (JsonException ex)
        {
            // What is in the file is wrong, and stays wrong until it is edited:
            // say so once, and keep the entries that were last read.
            logger.LogError(
                "The custom id mapping file {Path} could not be read, so its {Count} previously read entries stay in force: {Reason}",
                path, current.Count, ex.Message);
        }
        catch (IOException ex)
        {
            // Probably the editor was still writing. Look again next time.
            loadedSignature = RetryNextTime;
            logger.LogWarning(ex, "The custom id mapping file {Path} could not be opened; trying again shortly.", path);
        }
    }

    private IReadOnlyDictionary<int, CustomIdMapping> Read(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path), DocumentOptions);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            throw new JsonException("The file must hold a JSON array of entries.");

        var entries = new Dictionary<int, CustomIdMapping>();
        var position = 0;
        foreach (var element in document.RootElement.EnumerateArray())
        {
            position++;
            if (!TryReadEntry(element, position, path, out var entry))
                continue;

            if (!entries.TryAdd(entry.Mapping.AnimeId, entry))
                logger.LogWarning(
                    "Custom id mapping entry #{Position} in {Path} skipped: MAL id {AnimeId} already has an entry, and the first one counts.",
                    position, path, entry.Mapping.AnimeId);
        }

        logger.LogInformation(
            "Custom id mappings loaded from {Path}: {Count} entries ({Fill} fill, {Override} override).",
            path, entries.Count, entries.Values.Count(e => !e.Override), entries.Values.Count(e => e.Override));
        return entries;
    }

    private bool TryReadEntry(JsonElement element, int position, string path, out CustomIdMapping entry)
    {
        entry = null!;

        CustomIdMappingEntry? parsed;
        try
        {
            parsed = element.Deserialize<CustomIdMappingEntry>(EntryOptions);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            logger.LogWarning("Custom id mapping entry #{Position} in {Path} skipped: {Reason}", position, path, ex.Message);
            return false;
        }

        if (parsed?.MalId is not > 0)
        {
            logger.LogWarning("Custom id mapping entry #{Position} in {Path} skipped: it has no positive mal_id.", position, path);
            return false;
        }

        var mapping = AnimeIdMappingParser.ToMapping(parsed);
        if (mapping is null)
        {
            logger.LogWarning(
                "Custom id mapping entry #{Position} in {Path} skipped: MAL id {AnimeId} gives no TMDB id and no valid IMDb id.",
                position, path, parsed.MalId);
            return false;
        }

        // What the mapping quietly drops, an editor should hear about: a typo
        // there would otherwise just look like the entry not working.
        if (parsed.Season?.Tmdb is not null && parsed.TmdbIds?.Tv is null)
            logger.LogWarning(
                "Custom id mapping entry for MAL id {AnimeId} in {Path}: the season is ignored because it has no TMDB TV id.",
                parsed.MalId, path);
        foreach (var imdbId in parsed.ImdbIds ?? [])
        {
            if (!AnimeIdMappingParser.IsImdbId(imdbId))
                logger.LogWarning(
                    "Custom id mapping entry for MAL id {AnimeId} in {Path}: '{ImdbId}' is not an IMDb id (tt and digits) and is ignored.",
                    parsed.MalId, path, imdbId);
        }

        entry = new CustomIdMapping(mapping, parsed.Override ?? false, parsed.Note);
        return true;
    }
}
