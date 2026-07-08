namespace AnimeTracker.Api.Services.Profile;

public interface IProfileService
{
    Task<ProfileDto> GetProfileAsync(CancellationToken ct = default);

    Task<List<ActivityFeedItemDto>> GetActivityHistoryAsync(CancellationToken ct = default);
}
