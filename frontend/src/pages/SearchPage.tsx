import { useEffect, useMemo, useRef } from 'react'
import { useSearchParams } from 'react-router-dom'
import { getSearchPage } from '../api/client.ts'
import type { AnimeBrowseItemDto, SeriesSearchResultDto } from '../api/types.ts'
import { AnimeCard, AnimeCardMeta } from '../components/AnimeCard.tsx'
import { FilterMultiSelect } from '../components/FilterMultiSelect.tsx'
import { SeriesBadge } from '../components/SeriesBadge.tsx'
import { usePageData } from '../hooks/usePageData.ts'
import { useCompleteLastRow } from '../hooks/useCompleteLastRow.ts'
import { useRestorableState } from '../hooks/useRestorableState.ts'
import { MEDIA_TYPE_FILTER_OPTIONS, mediaTypeFilterOptions } from '../utils/anime.ts'
import './SearchPage.css'

type SortKey = 'relevance' | 'popularity' | 'malScore' | 'alphabetical' | 'myScore'

interface SearchReadState {
  items: AnimeBrowseItemDto[]
  totalCount: number
  series: SeriesSearchResultDto[]
  malSearchFailed: boolean
}

const SORT_OPTIONS: { value: SortKey; label: string }[] = [
  { value: 'relevance', label: 'Relevance' },
  { value: 'popularity', label: 'Popularity' },
  { value: 'malScore', label: 'MAL score' },
  { value: 'alphabetical', label: 'Alphabetical' },
  { value: 'myScore', label: 'My score' },
]

// How many revealed cards grow by per scroll-triggered reveal. Distinct from
// CANDIDATE_LIMIT below — this only controls how much of the already-loaded
// array is rendered at once.
const CHUNK_SIZE = 48

// The whole candidate set for a query is fetched once — the 60 highest-
// relevance matches, the one figure the live search, the endpoint's clamp,
// and this page all agree on (design.md D6) — so scrolling further just
// reveals more of what's already in memory instead of re-running the live
// MAL search per chunk.
const CANDIDATE_LIMIT = 60

function isSortKey(value: string | null): value is SortKey {
  return value !== null && SORT_OPTIONS.some((option) => option.value === value)
}

// Full search results page: fetches the whole (≤60) candidate set once per
// (query, sort) — the search endpoint has no cache behind it, so paging would
// re-run the live MAL search per chunk — and reveals it in chunks of
// CHUNK_SIZE via an IntersectionObserver sentinel, the same continuous-scroll
// pattern as the season page. Query/sort live in the URL so back-navigation
// from an anime detail page restores exactly where the user left off,
// including how much of the result set had been revealed.
// A legacy `?page=N` link is simply ignored — the query itself still resolves.
export function SearchPage() {
  const [searchParams, setSearchParams] = useSearchParams()

  const q = searchParams.get('q') ?? ''
  const sortParam = searchParams.get('sort')
  const sort = isSortKey(sortParam) ? sortParam : 'relevance'
  const typeParam = searchParams.get('type')
  // URLSearchParams.get already distinguishes absent (null) from
  // present-and-empty (''), which is what makes a third state — None — free:
  // no parameter is All, `?type=` is None, `?type=tv,movie` is a selection.
  const typeFilter = typeParam === null ? null : typeParam.split(',').filter(Boolean)

  // Keyed on the query alone: a different query is a different history
  // snapshot, so restoring one restores its results regardless of which sort
  // was active when it was left (D5). A sort change within the same query is
  // handled below via `reload`, not a second key.
  const { data, loading, reload } = usePageData<SearchReadState>(`search:${q}`, () =>
    q.length === 0
      ? Promise.resolve({ items: [], totalCount: 0, series: [], malSearchFailed: false })
      : getSearchPage(q, { sort, offset: 0, limit: CANDIDATE_LIMIT }).then((result) => ({
          items: result.items,
          totalCount: result.totalCount,
          series: result.series,
          malSearchFailed: result.malSearchFailed,
        })),
  )
  const items = data?.items ?? []
  const totalCount = data?.totalCount ?? 0
  const series = data?.series ?? []
  const malSearchFailed = data?.malSearchFailed ?? false
  const [visibleCount, setVisibleCount] = useRestorableState('visibleCount', CHUNK_SIZE)
  const sentinelRef = useRef<HTMLDivElement>(null)
  const reloadRef = useRef(reload)
  reloadRef.current = reload

  function setSort(next: SortKey) {
    setSearchParams((prev) => {
      const params = new URLSearchParams(prev)
      params.set('sort', next)
      return params
    })
  }

  function setTypeFilter(next: string[] | null) {
    setSearchParams((prev) => {
      const params = new URLSearchParams(prev)
      if (next === null) params.delete('type')
      else params.set('type', next.join(','))
      return params
    })
  }

  // Sort changed within the same query: the query's initial or restored load
  // is already covered by usePageData itself (above), so this only fires on
  // a later change, reusing `reload`'s own generation guard. Read through a
  // ref so a query change (which gives `reload` a new identity) doesn't also
  // retrigger this effect.
  const isFirstSortRun = useRef(true)
  useEffect(() => {
    if (isFirstSortRun.current) {
      isFirstSortRun.current = false
      return
    }
    reloadRef.current()
  }, [sort])

  // Type filter options: only the media types actually present in the loaded
  // candidate set, mirroring MyListPage's presentTypes/hasUnknownType (D6).
  const typeOptions = useMemo(() => mediaTypeFilterOptions(items.map((item) => item.mediaType)), [items])

  const filteredItems = useMemo(
    () => (typeFilter === null ? items : items.filter((item) => typeFilter.includes(item.mediaType ?? 'unknown'))),
    [items, typeFilter],
  )

  // Reveal more of the already-loaded array once the sentinel enters view —
  // no network call, everything for this (query, sort) is already in memory.
  useEffect(() => {
    const node = sentinelRef.current
    if (!node) return

    const observer = new IntersectionObserver((entries) => {
      if (entries[0]?.isIntersecting) {
        setVisibleCount((prev) => Math.min(prev + CHUNK_SIZE, filteredItems.length))
      }
    })
    observer.observe(node)
    return () => observer.disconnect()
  }, [filteredItems, setVisibleCount])

  const visibleItems = filteredItems.slice(0, visibleCount)
  // Tops the reveal up so its last row is never left with a lone card
  // (below the desktop breakpoint the auto-fill column count needn't divide the step).
  const gridRef = useCompleteLastRow(filteredItems, visibleCount, setVisibleCount)

  return (
    <div className="search-page">
      <div className="search-page__header">
        <h1>{q.length > 0 ? <>Results for “{q}”</> : 'Search'}</h1>
        <div className="search-page__controls">
          {totalCount > 0 && (
            <span className="search-page__count">{filteredItems.length + series.length} results</span>
          )}
          <FilterMultiSelect
            label="Type"
            options={typeOptions}
            widthOptions={MEDIA_TYPE_FILTER_OPTIONS}
            selected={typeFilter}
            onChange={setTypeFilter}
          />
          <select
            className="search-page__sort"
            value={sort}
            onChange={(event) => setSort(event.target.value as SortKey)}
            aria-label="Sort search results"
          >
            {SORT_OPTIONS.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
        </div>
      </div>

      {malSearchFailed && (
        <p className="search-page__fallback-notice" role="status">
          The MAL search couldn't be reached — these are the app's locally stored matches.
        </p>
      )}

      {q.length === 0 ? (
        <p className="search-page__empty">Enter a search term to begin.</p>
      ) : items.length === 0 && series.length === 0 && !loading ? (
        // A failed-search empty result is explained by the notice above
        // instead of the ordinary "not found" message, so it reads as
        // "nothing stored here matches" rather than "this does not exist".
        malSearchFailed ? null : (
          <p className="search-page__empty">No anime found.</p>
        )
      ) : (
        <>
          <div ref={gridRef} className="search-page__grid">
            {series.map((s) => (
              <AnimeCard
                key={`series-${s.seriesId}`}
                animeId={s.seriesId}
                title={s.title}
                englishTitle={s.englishTitle}
                pictureUrl={s.pictureUrl}
                to={`/series/${s.seriesId}`}
                className="anime-card--fluid"
              >
                <SeriesBadge entryCount={s.entryCount} />
              </AnimeCard>
            ))}
            {visibleItems.map((item) => (
              <AnimeCard
                key={item.animeId}
                animeId={item.animeId}
                title={item.title}
                englishTitle={item.englishTitle}
                pictureUrl={item.pictureUrl}
                className="anime-card--fluid"
              >
                <AnimeCardMeta mediaType={item.mediaType} totalEpisodes={item.totalEpisodes} malScore={item.malScore} />
              </AnimeCard>
            ))}
          </div>
          {/* Distinct from "No anime found." above: the query matched anime,
              the type filter just leaves none of them standing. Series cards
              aren't subject to the type filter, so they keep rendering
              alongside this rather than being hidden by it. */}
          {items.length > 0 && filteredItems.length === 0 && (
            <p className="search-page__empty">No anime match the current filters.</p>
          )}
        </>
      )}

      <div ref={sentinelRef} className="search-page__sentinel" />
      {loading && <p className="search-page__loading">Loading…</p>}
    </div>
  )
}
