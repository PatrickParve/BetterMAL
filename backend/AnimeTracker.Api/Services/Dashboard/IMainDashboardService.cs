namespace AnimeTracker.Api.Services.Dashboard;

/// <summary>Assembles the main page's three sections (currently watching,
/// airing today, current season) entirely from cached Postgres data — no
/// live MAL calls on this read path.</summary>
public interface IMainDashboardService
{
    Task<MainDashboardDto> GetDashboardAsync(CancellationToken ct = default);
}
