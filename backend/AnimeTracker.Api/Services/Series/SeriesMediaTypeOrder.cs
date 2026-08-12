namespace AnimeTracker.Api.Services.Series;

/// <summary>Fixed display order for series extras (design.md decision 3) —
/// Movie, OVA, ONA, Special, Music, TV, Other. Shared by
/// <see cref="SeriesGraphBuilder"/> (which assigns each extra's <c>Order</c>
/// within its group) and <see cref="SeriesService"/> (which groups the same
/// way when rendering the More section), so the two orderings can't drift.</summary>
public static class SeriesMediaTypeOrder
{
    private static readonly Dictionary<string, int> GroupByMediaType = new()
    {
        ["movie"] = 0,
        ["ova"] = 1,
        ["ona"] = 2,
        ["special"] = 3,
        ["music"] = 4,
        ["tv"] = 5,
    };
    private const int OtherGroup = 6;

    public static int GroupOf(string? mediaType) =>
        mediaType is not null && GroupByMediaType.TryGetValue(mediaType, out var group) ? group : OtherGroup;
}
