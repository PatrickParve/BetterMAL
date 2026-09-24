using System.Text.Json;
using System.Text.Json.Serialization;

namespace AnimeTracker.Api.Services.IdMapping;

/// <summary>One entry of the Fribb anime-lists <c>anime-list-mini.json</c>,
/// declaring only the four values the mapping keeps (spec
/// `external-id-mapping`). System.Text.Json skips every other property, so an
/// entry's type, its AniList/AniDB/Kitsu ids, its TheTVDB ids and its episode
/// offsets can never reach the stored mapping. Each of the four declared
/// shapes is strict: a value of any other shape throws, and the sync then
/// keeps its last good mapping rather than guess (design.md D3).</summary>
public class FribbEntry
{
    [JsonPropertyName("mal_id")]
    public int? MalId { get; set; }

    [JsonPropertyName("themoviedb_id")]
    public FribbTmdbIds? TmdbIds { get; set; }

    [JsonPropertyName("season")]
    public FribbSeason? Season { get; set; }

    [JsonPropertyName("imdb_id")]
    [JsonConverter(typeof(FribbImdbIdsConverter))]
    public List<string>? ImdbIds { get; set; }
}

/// <summary>The file's <c>themoviedb_id</c>: an object holding a TV id or a
/// list of movie ids (never both today). It was once a bare number, which is
/// deliberately not accepted now — guessing what a bare number means would
/// silently corrupt the mapping.</summary>
public class FribbTmdbIds
{
    [JsonPropertyName("tv")]
    public int? Tv { get; set; }

    [JsonPropertyName("movie")]
    public List<int>? Movie { get; set; }
}

/// <summary>The file's <c>season</c>: the entry's season number on each site.
/// Only the TMDB one is read; the TheTVDB one is skipped.</summary>
public class FribbSeason
{
    [JsonPropertyName("tmdb")]
    public int? Tmdb { get; set; }
}

/// <summary>Reads <c>imdb_id</c> as a list of strings. The file gives a JSON
/// array; a bare string is accepted as a one-element list, which is
/// unambiguous. Any other shape, including a non-string element, throws
/// (design.md D3).</summary>
public sealed class FribbImdbIdsConverter : JsonConverter<List<string>>
{
    public override List<string>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            return [reader.GetString()!];

        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("imdb_id must be a JSON array or a string.");

        var ids = new List<string>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType != JsonTokenType.String)
                throw new JsonException("imdb_id entries must be strings.");
            ids.Add(reader.GetString()!);
        }

        return ids;
    }

    public override void Write(Utf8JsonWriter writer, List<string> value, JsonSerializerOptions options) =>
        throw new NotSupportedException("The mapping file is only ever read.");
}
