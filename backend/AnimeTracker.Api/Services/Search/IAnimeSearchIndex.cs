using AnimeTracker.Api.Data.Repositories;

namespace AnimeTracker.Api.Services.Search;

/// <summary>In-memory cache of <see cref="IAnimeMetadataRepository.GetSearchIndexAsync"/>'s
/// rows, held for the type-ahead search's local-cache stage so it need not
/// read the whole <c>AnimeMetadata</c> table on every request.</summary>
public interface IAnimeSearchIndex
{
    /// <summary>Returns the cached rows, rebuilding first if the cache is
    /// empty or stale. The returned list is shared across callers and MUST
    /// NOT be mutated. There is no TTL: only <see cref="Invalidate"/> can
    /// make the cache stale, never the passage of time.</summary>
    Task<List<AnimeTitleProjection>> GetAsync(CancellationToken ct = default);

    /// <summary>The same rows as <see cref="GetAsync"/>, keyed by anime id, so a
    /// caller holding a list of ids (the search page's up-to-60 MAL candidates)
    /// can look each one up without scanning the whole list or loading the
    /// anime again (improve-search-page design.md D1). The returned dictionary is
    /// shared across callers and MUST NOT be mutated.
    /// <para>The real cache publishes this view in the same entry as the rows, so
    /// the two can never describe different generations. The default
    /// implementation builds it from <see cref="GetAsync"/> on every call, which is
    /// what keeps an implementation that only supplies rows (the test fakes)
    /// compiling.</para></summary>
    async Task<IReadOnlyDictionary<int, AnimeTitleProjection>> GetByIdAsync(CancellationToken ct = default) =>
        IndexById(await GetAsync(ct));

    /// <summary>Keys <paramref name="rows"/> by anime id. The id is
    /// <c>AnimeMetadata</c>'s primary key, so a real index has no duplicates; were
    /// one given, the later row would win rather than the call throwing.</summary>
    static Dictionary<int, AnimeTitleProjection> IndexById(IEnumerable<AnimeTitleProjection> rows)
    {
        var byId = new Dictionary<int, AnimeTitleProjection>();
        foreach (var row in rows)
            byId[row.Id] = row;

        return byId;
    }

    /// <summary>Marks the cache stale so the next <see cref="GetAsync"/>
    /// rebuilds it. Every write that sets <c>Title</c>, <c>EnglishTitle</c>,
    /// <c>PopularityRank</c>, or <c>PictureUrl</c> (via <c>ResolvePictureUrl()</c>)
    /// on an <c>AnimeMetadata</c> row MUST call this after its
    /// <c>SaveChangesAsync</c> succeeds, never before. The current callers are
    /// the seven writers listed in the <c>cache-type-ahead-search-index</c>
    /// change's proposal: <c>TopAnimeService</c>, <c>SeasonBrowseService</c>,
    /// <c>ReconciliationService</c>, <c>InitialImportService</c>,
    /// <c>MetadataRefreshService</c> (two methods),
    /// <c>ArtworkSelectionService</c> (three methods), and first-run setup's
    /// list and details steps (<c>SetupListStep</c>, <c>SetupDetailsStep</c>).</summary>
    void Invalidate();
}
