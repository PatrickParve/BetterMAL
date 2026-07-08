namespace AnimeTracker.Api.Data.Repositories;

/// <summary>Persistence for which anime the user has manually chosen to fill
/// the remaining "My top anime" slots, overriding the default next-highest
/// auto-fill.</summary>
public interface ITopAnimeSelectionRepository
{
    Task<List<int>> GetSelectedAnimeIdsAsync(CancellationToken ct = default);

    /// <summary>Replaces the entire manual selection with the given anime ids.
    /// An empty collection clears the manual selection, reverting the profile
    /// page to the default auto-fill.</summary>
    Task ReplaceSelectionAsync(IReadOnlyCollection<int> animeIds, CancellationToken ct = default);
}
