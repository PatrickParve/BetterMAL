using AnimeTracker.Api.Services.Mal.Dto;

namespace AnimeTracker.Api.Services.Mal;

/// <summary>The two reads only first-run setup makes of MyAnimeList (add-first-run-setup
/// design D7), kept apart from <see cref="IMalClient"/> because nothing else asks for them.
/// Both are bearer-authenticated, so a refused login surfaces as
/// <see cref="MalAuthorizationRequiredException"/>.</summary>
public interface IMalSetupClient
{
    /// <summary>Pages through the whole list, 100 entries a page, asking for
    /// <see cref="MalClient.SetupListFields"/>: the list status and every field a basic anime
    /// row holds. Each page is handed over as it arrives, so setup can store it before the
    /// next request. Paging follows MyAnimeList's own <c>next</c> link, like every full-list
    /// read.</summary>
    IAsyncEnumerable<List<MalUserAnimeListEdge>> GetUserAnimeListPagesAsync(CancellationToken ct = default);

    /// <summary>How many entries MyAnimeList says the list holds, or null when its answer
    /// carries no count. The list's own pages carry no total (design D7), so this is the
    /// only way to show a real total while the list is still being read.</summary>
    Task<int?> GetUserAnimeCountAsync(CancellationToken ct = default);
}
