namespace AnimeTracker.Api.Services.Profile;

public interface IProfileService
{
    Task<ProfileDto> GetProfileAsync(CancellationToken ct = default);

    Task<List<ActivityFeedItemDto>> GetActivityHistoryAsync(CancellationToken ct = default);

    /// <summary>The "My top anime" section for one scope (all or one media
    /// type), computed from the same ranking rules as the section embedded in
    /// <see cref="GetProfileAsync"/>.</summary>
    Task<TopAnimeSectionDto> GetTopAnimeSectionAsync(string mediaType, CancellationToken ct = default);

    /// <summary>The "Most rewatched" section for one scope (all or one media
    /// type), computed from the same ranking rules as the section embedded in
    /// <see cref="GetProfileAsync"/>.</summary>
    Task<RewatchedSectionDto> GetRewatchedSectionAsync(string mediaType, CancellationToken ct = default);

    /// <summary>The "Top series" section: every franchise with a member in my
    /// list, each carrying both main-series averages computed the same way
    /// the series page computes them (design.md decision 1/2), pre-ordered by
    /// my average descending. Also schedules background builds for a bounded
    /// batch of my-list anime that belong to no stored series yet (design.md
    /// decision 6), so the ranking's coverage grows a little on every
    /// read.</summary>
    Task<TopSeriesSectionDto> GetTopSeriesSectionAsync(CancellationToken ct = default);

    /// <summary>"Most rewatched"'s Series scope: every franchise with
    /// above-zero total rewatch time, summed across every member — main line
    /// and extras alike — using the same per-entry arithmetic <c>WatchMath</c>
    /// uses for the profile's own rewatch-inclusive stats (design.md
    /// D8/D9), so a franchise total and those stats can never disagree.
    /// Deliberately does not schedule background series builds itself — the
    /// same profile page's Top series read already does that (design.md
    /// D10), and queueing the same ids twice only contends on one
    /// queue.</summary>
    Task<RewatchedSeriesSectionDto> GetRewatchedSeriesSectionAsync(CancellationToken ct = default);

    /// <summary>Applies an edited tier order — as displayed under some scope,
    /// which may omit tier members that scope's filter hides — via the
    /// slot-preserving merge: every edited tier's full membership becomes
    /// explicitly ordered, with hidden members kept in their existing
    /// relative positions.</summary>
    Task ApplyTopAnimeOrderAsync(List<TopAnimeTierOrderRequest> tiers, CancellationToken ct = default);
}

public record TopAnimeTierOrderRequest(int Score, List<int> AnimeIds);
