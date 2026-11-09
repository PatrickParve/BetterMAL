namespace AnimeTracker.Api.Services.Ranking.ThisOrThat;

/// <summary>How one pick came out, from the side of the anime being placed (the
/// challenger): it was ranked above the anime it was shown with, below it, or
/// neither (add-this-or-that-ranking design.md D3). The numbers are what the
/// stored answer list holds.</summary>
public enum ThisOrThatOutcome
{
    Above = 1,
    Below = -1,

    /// <summary>Can't decide: the challenger is placed directly below the anime
    /// it was shown with, which ends its placement.</summary>
    CantDecide = 0,
}
