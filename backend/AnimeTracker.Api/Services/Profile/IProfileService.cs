namespace AnimeTracker.Api.Services.Profile;

public interface IProfileService
{
    Task<ProfileDto> GetProfileAsync(CancellationToken ct = default);

    Task<List<ActivityFeedItemDto>> GetActivityHistoryAsync(CancellationToken ct = default);

    /// <summary>The "My top anime" section for one scope (all or one media
    /// type), computed from the same ranking rules as the section embedded in
    /// <see cref="GetProfileAsync"/>.</summary>
    Task<TopAnimeSectionDto> GetTopAnimeSectionAsync(string mediaType, CancellationToken ct = default);

    /// <summary>Applies an edited tier order — as displayed under some scope,
    /// which may omit tier members that scope's filter hides — via the
    /// slot-preserving merge: every edited tier's full membership becomes
    /// explicitly ordered, with hidden members kept in their existing
    /// relative positions.</summary>
    Task ApplyTopAnimeOrderAsync(List<TopAnimeTierOrderRequest> tiers, CancellationToken ct = default);
}

public record TopAnimeTierOrderRequest(int Score, List<int> AnimeIds);
