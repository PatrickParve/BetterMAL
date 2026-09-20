using System.Text.Json.Serialization;

namespace AnimeTracker.Api.Services.Library;

// Camel-cased on the wire via JsonStringEnumMemberName below, matching the
// frontend's outcome union — see SeasonRefreshOutcome, which this mirrors.
[JsonConverter(typeof(JsonStringEnumConverter<TopAnimeRefreshOutcome>))]
public enum TopAnimeRefreshOutcome
{
    [JsonStringEnumMemberName("fetched")]
    Fetched,
    [JsonStringEnumMemberName("skipped")]
    Skipped,
    [JsonStringEnumMemberName("failed")]
    Failed,
}

public record TopAnimeRefreshResultDto(TopAnimeRefreshOutcome Outcome);
