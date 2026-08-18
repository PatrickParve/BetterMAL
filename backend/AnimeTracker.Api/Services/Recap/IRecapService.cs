namespace AnimeTracker.Api.Services.Recap;

public interface IRecapService
{
    Task<RecapDto> GetRecapAsync(RecapPeriod period, string filter, CancellationToken ct = default);
}
