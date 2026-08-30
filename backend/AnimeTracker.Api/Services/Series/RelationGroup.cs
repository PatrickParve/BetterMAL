namespace AnimeTracker.Api.Services.Series;

/// <summary>An extra's relationship to its series' main line
/// (split-series-by-version design.md decision 4), in the fixed display
/// order the More section renders groups in. Declaration order below is that
/// display order — see <see cref="SeriesRelationGroupOrder"/>.</summary>
public enum RelationGroup
{
    AlternativeVersion,
    AlternativeSetting,
    Prequel,
    Sequel,
    ParentStory,
    SideStory,
    FullStory,
    Summary,
    SpinOff,
    Character,
    Adaptation,
    Other,
}
