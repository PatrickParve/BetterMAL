namespace AnimeTracker.Api.Services.Season;

/// <summary>Backs the Season page: all anime airing in a given season (not
/// just my list), live-fetched from MAL on first visit and cached
/// indefinitely thereafter — except the current and immediately-upcoming
/// season, which re-fetch once per local calendar day.</summary>
public interface ISeasonBrowseService
{
    Task<SeasonPageDto> GetPageAsync(int year, string season, string sortKey, int offset, int limit, CancellationToken ct = default);
}
