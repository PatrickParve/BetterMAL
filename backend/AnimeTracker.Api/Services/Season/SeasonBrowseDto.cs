using System.Text.Json.Serialization;
using AnimeTracker.Api.Services.Library;

namespace AnimeTracker.Api.Services.Season;

// LastFetchedAt/HasListing together spell out the season's three-way empty
// state: LastFetchedAt null means never cached (client stays loading);
// LastFetchedAt set with HasListing false means MAL has no listing for this
// season (a 404, or a hypothetical 200 with zero entries — decision 2, both
// mean the same thing to the user); LastFetchedAt set with HasListing true
// but Items empty means the cached listing exists but nothing matches the
// current filters.
public record SeasonPageDto(int Year, string Season, List<AnimeBrowseItemDto> Items, int Offset, int Limit, int TotalCount, DateTimeOffset? LastFetchedAt, bool HasListing);

// Camel-cased on the wire via JsonStringEnumMemberName below — the global
// JsonStringEnumConverter registered in Program.cs defaults to each member's
// exact (PascalCase) name, but the frontend's outcome union is camelCase.
[JsonConverter(typeof(JsonStringEnumConverter<SeasonRefreshOutcome>))]
public enum SeasonRefreshOutcome
{
    [JsonStringEnumMemberName("fetched")]
    Fetched,
    [JsonStringEnumMemberName("notListed")]
    NotListed,
    [JsonStringEnumMemberName("skipped")]
    Skipped,
    [JsonStringEnumMemberName("failed")]
    Failed,
}

public record SeasonRefreshResultDto(SeasonRefreshOutcome Outcome);

/// <summary>The furthest season the user may navigate to — see
/// SeasonHorizon.Resolve for how it's computed. Never calls MAL; a pure cache
/// read like SeasonPageDto.</summary>
public record SeasonBoundsDto(int LatestYear, string LatestSeason);
