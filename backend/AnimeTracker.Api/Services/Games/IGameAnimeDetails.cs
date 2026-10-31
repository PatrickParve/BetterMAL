namespace AnimeTracker.Api.Services.Games;

/// <summary>The one step a game takes to have an anime's stats to show or compare
/// (add-guess-the-anime design.md D6). A separate interface so the game service
/// can be tested without MyAnimeList behind it.</summary>
public interface IGameAnimeDetails
{
    /// <summary>Makes a full-detail fetch of <paramref name="animeId"/> from
    /// MyAnimeList when it isn't stored or is missing one of the game's stats
    /// (<see cref="AnimeStats.IsMissing"/>), and returns true when this call made
    /// it. An anime holding every stat is not fetched, however old they are; one
    /// that another caller has just fetched is not fetched again. A fetch that
    /// fails throws to the caller, and leaves nothing locked.</summary>
    Task<bool> EnsureFetchedAsync(int animeId, CancellationToken ct = default);
}
