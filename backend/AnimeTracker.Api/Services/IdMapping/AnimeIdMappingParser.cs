using System.Text.Json;
using System.Text.RegularExpressions;
using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Services.IdMapping;

/// <summary>Reads the Fribb mapping file into the mapping the app keeps: per
/// MyAnimeList id, the TMDB TV id, TMDB season number, TMDB movie ids and IMDb
/// ids (spec `external-id-mapping`). Streams the file entry by entry rather
/// than loading its 5.8 MB into one buffer, and builds the whole result before
/// returning, so a file that fails part-way yields nothing rather than a
/// partial mapping.</summary>
public static partial class AnimeIdMappingParser
{
    // "tt" and digits only, so an empty string or anything malformed is dropped.
    // [0-9] rather than \d, which also matches other scripts' digits, and \z
    // rather than $, which also matches before a trailing newline.
    [GeneratedRegex(@"^tt[0-9]+\z")]
    private static partial Regex ImdbIdPattern();

    public static async Task<Dictionary<int, AnimeIdMapping>> ParseAsync(Stream stream, CancellationToken ct = default)
    {
        var mappings = new Dictionary<int, AnimeIdMapping>();
        var seen = new HashSet<int>();

        await foreach (var entry in JsonSerializer.DeserializeAsyncEnumerable<FribbEntry>(stream, cancellationToken: ct))
        {
            if (entry?.MalId is not { } malId)
                continue;

            // The first entry for a MAL id wins, even one that keeps nothing
            // (ToMapping): a later duplicate never fills the gap.
            if (!seen.Add(malId))
                continue;

            if (ToMapping(entry) is { } mapping)
                mappings[malId] = mapping;
        }

        return mappings;
    }

    /// <summary>One entry as the mapping keeps it, or null when the entry has
    /// no MAL id or carries none of the four kept values. The single place an
    /// entry's values are kept or dropped, so the custom mapping file (design
    /// D19), which is written by hand in the same shape, follows exactly the
    /// rules the downloaded file does.</summary>
    public static AnimeIdMapping? ToMapping(FribbEntry entry)
    {
        if (entry.MalId is not { } malId)
            return null;

        var tvId = entry.TmdbIds?.Tv;
        var movieIds = entry.TmdbIds?.Movie ?? [];
        var imdbIds = KeptImdbIds(entry.ImdbIds);

        if (tvId is null && movieIds.Count == 0 && imdbIds.Count == 0)
            return null;

        return new AnimeIdMapping
        {
            AnimeId = malId,
            TmdbTvId = tvId,
            // A season number means nothing without its show.
            TmdbSeasonNumber = tvId is null ? null : entry.Season?.Tmdb,
            TmdbMovieIds = [.. movieIds],
            ImdbIds = imdbIds,
        };
    }

    /// <summary>Whether <paramref name="id"/> is an IMDb id the mapping would
    /// keep. The custom mapping file uses it to tell me when a typo made it drop
    /// one.</summary>
    public static bool IsImdbId(string id) => ImdbIdPattern().IsMatch(id);

    private static List<string> KeptImdbIds(List<string>? ids)
    {
        var kept = new List<string>();
        foreach (var id in ids ?? [])
        {
            if (ImdbIdPattern().IsMatch(id) && !kept.Contains(id))
                kept.Add(id);
        }

        return kept;
    }
}
