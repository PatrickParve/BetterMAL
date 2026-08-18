namespace AnimeTracker.Api.Services.Recap;

public interface IRecapAvailabilityService
{
    Task<RecapAvailabilityDto> GetAvailabilityAsync(CancellationToken ct = default);
}
