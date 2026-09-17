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

    /// <summary>Marks the cache stale so the next <see cref="GetAsync"/>
    /// rebuilds it. Every write that sets <c>Title</c>, <c>EnglishTitle</c>,
    /// <c>PopularityRank</c>, or <c>PictureUrl</c> (via <c>ResolvePictureUrl()</c>)
    /// on an <c>AnimeMetadata</c> row MUST call this after its
    /// <c>SaveChangesAsync</c> succeeds, never before. The current callers are
    /// the seven writers listed in the <c>cache-type-ahead-search-index</c>
    /// change's proposal: <c>TopAnimeService</c>, <c>SeasonBrowseService</c>,
    /// <c>ReconciliationService</c>, <c>InitialImportService</c>,
    /// <c>MetadataRefreshService</c> (two methods), and
    /// <c>ArtworkSelectionService</c> (three methods).</summary>
    void Invalidate();
}
