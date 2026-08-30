namespace AnimeTracker.Api.Services.Series;

/// <summary>Fixed display order for series extras' relation groups
/// (split-series-by-version design.md decision 4) — Alternative version,
/// Alternative setting, Prequel, Sequel, Parent story, Side story, Full
/// story, Summary, Spin-off, Character, Adaptation, Other. Supersedes the
/// media-type grouping this replaced; both <c>SeriesGraphBuilder</c>'s
/// per-telling classification and <c>SeriesService</c>'s projection group by
/// relation now.</summary>
public static class SeriesRelationGroupOrder
{
    private static readonly Dictionary<RelationGroup, int> OrderByGroup = new()
    {
        [RelationGroup.AlternativeVersion] = 0,
        [RelationGroup.AlternativeSetting] = 1,
        [RelationGroup.Prequel] = 2,
        [RelationGroup.Sequel] = 3,
        [RelationGroup.ParentStory] = 4,
        [RelationGroup.SideStory] = 5,
        [RelationGroup.FullStory] = 6,
        [RelationGroup.Summary] = 7,
        [RelationGroup.SpinOff] = 8,
        [RelationGroup.Character] = 9,
        [RelationGroup.Adaptation] = 10,
        [RelationGroup.Other] = 11,
    };

    public static int GroupOf(RelationGroup group) => OrderByGroup[group];
}
