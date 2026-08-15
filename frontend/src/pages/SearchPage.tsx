import { useEffect, useMemo, useRef } from 'react'
import { useSearchParams } from 'react-router-dom'
import { getSearchPage } from '../api/client.ts'
import type { AnimeBrowseItemDto, SeriesSearchResultDto } from '../api/types.ts'
import { AnimeCard, AnimeCardMeta } from '../components/AnimeCard.tsx'
import { FilterMultiSelect, type FilterMultiSelectOption } from '../components/FilterMultiSelect.tsx'
import { SeriesBadge } from '../components/SeriesBadge.tsx'
import { usePageData } from '../hooks/usePageData.ts'
import { useRestorableState } from '../hooks/useRestorableState.ts'
import { MEDIA_TYPE_ORDER, mediaTypeLabel } from '../utils/anime.ts'
import './SearchPage.css'

type SortKey = 'relevance' | 'popularity' | 'malScore' | 'alphabetical' | 'myScore'

interface SearchReadState {
  items: AnimeBrowseItemDto[]
  totalCount: number
  series: SeriesSearchResultDto[]
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

// The whole candidate set for a query is fetched once (MAL's own cap), so
// scrolling further just reveals more of what's already in memory instead of
// re-running the live MAL search per chunk.
const CANDIDATE_LIMIT = 90

function isSortKey(value: string | null): value is SortKey {
  return value !== null && SORT_OPTIONS.some((option) => option.value === value)
}

// Full search results page: fetches the whole (≤90) candidate set once per
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
  const typeFilter = typeParam ? typeParam.split(',').filter(Boolean) : []

  // Keyed on the query alone: a different query is a different history
  // snapshot, so restoring one restores its results regardless of which sort
  // was active when it was left (D5). A sort change within the same query is
  // handled below via `reload`, not a second key.
  const { data, loading, reload } = usePageData<SearchReadState>(`search:${q}`, () =>
    q.length === 0
      ? Promise.resolve({ items: [], totalCount: 0, series: [] })
      : getSearchPage(q, { sort, offset: 0, limit: CANDIDATE_LIMIT }).then((result) => ({
          items: result.items,
          totalCount: result.totalCount,
          series: result.series,
        })),
  )
  const items = data?.items ?? []
  const totalCount = data?.totalCount ?? 0
  const series = data?.series ?? []
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

  function setTypeFilter(next: string[]) {
    setSearchParams((prev) => {
      const params = new URLSearchParams(prev)
      if (next.length > 0) params.set('type', next.join(','))
      else params.delete('type')
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
  const typeOptions = useMemo(() => {
    const presentTypes = new Set<string>()
    let hasUnknownType = false
    for (const item of items) {
      if (item.mediaType) presentTypes.add(item.mediaType)
      else hasUnknownType = true
    }
    const options: FilterMultiSelectOption[] = MEDIA_TYPE_ORDER.filter((value) => presentTypes.has(value)).map(
      (value) => ({ value, label: mediaTypeLabel(value) }),
    )
    if (hasUnknownType) options.push({ value: 'unknown', label: 'Unknown' })
    return options
  }, [items])

  const filteredItems = useMemo(
    () =>
      typeFilter.length === 0 ? items : items.filter((item) => typeFilter.includes(item.mediaType ?? 'unknown')),
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

  return (
    <div className="search-page">
      <div className="search-page__header">
        <h1>{q.length > 0 ? <>Results for “{q}”</> : 'Search'}</h1>
        <div className="search-page__controls">
          {totalCount > 0 && <span className="search-page__count">{filteredItems.length} results</span>}
          {typeOptions.length > 0 && (
            <FilterMultiSelect label="Type" options={typeOptions} selected={typeFilter} onChange={setTypeFilter} />
          )}
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

      {q.length === 0 ? (
        <p className="search-page__empty">Enter a search term to begin.</p>
      ) : items.length === 0 && series.length === 0 && !loading ? (
        <p className="search-page__empty">No anime found.</p>
      ) : (
        <div className="search-page__grid">
          {series.map((s) => (
            <AnimeCard
              key={`series-${s.seriesId}`}
              animeId={s.rootAnimeId}
              title={s.title}
              englishTitle={s.englishTitle}
              pictureUrl={s.pictureUrl}
              to={`/series/${s.rootAnimeId}`}
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
              <AnimeCardMeta mediaType={item.mediaType} totalEpisodes={item.totalEpisodes} />
            </AnimeCard>
          ))}
        </div>
      )}

      <div ref={sentinelRef} className="search-page__sentinel" />
      {loading && <p className="search-page__loading">Loading…</p>}
    </div>
  )
}
