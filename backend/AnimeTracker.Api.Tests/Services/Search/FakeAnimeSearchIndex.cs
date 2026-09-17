using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Services.Search;

namespace AnimeTracker.Api.Tests.Services.Search;

// Shared across the writers' test files (design.md D6/D7) and
// AnimeSearchServiceTests: a settable list stands in for the real cache's
// rows, and InvalidateCallCount is what each writer's own tests assert on.
internal sealed class FakeAnimeSearchIndex(List<AnimeTitleProjection>? rows = null) : IAnimeSearchIndex
{
    public List<AnimeTitleProjection> Rows { get; set; } = rows ?? [];
    public int InvalidateCallCount { get; private set; }

    public Task<List<AnimeTitleProjection>> GetAsync(CancellationToken ct = default) => Task.FromResult(Rows);
    public void Invalidate() => InvalidateCallCount++;
}
