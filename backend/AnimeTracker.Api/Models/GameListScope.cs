namespace AnimeTracker.Api.Models;

/// <summary>Which anime a game may draw on, by whether they are on my list
/// (add-games-higher-or-lower design.md D4), the same for every game on the
/// Games page. Stored by name, as WatchStatus is.</summary>
public enum GameListScope
{
    All,
    OnlyMyList,
    NotOnMyList,
}
