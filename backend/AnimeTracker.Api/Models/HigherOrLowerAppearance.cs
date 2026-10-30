namespace AnimeTracker.Api.Models;

/// <summary>One anime shown in Higher or Lower, in the order shown
/// (add-games-higher-or-lower design.md D2). It holds only which anime it was:
/// no game id and no streak, so it records nothing about how any game went. It
/// outlives the game that showed it, because holding recently shown anime back
/// across games is its whole purpose, and is removed with the anime.</summary>
public class HigherOrLowerAppearance
{
    public long Id { get; set; }
    public int AnimeId { get; set; } // MAL id
}
