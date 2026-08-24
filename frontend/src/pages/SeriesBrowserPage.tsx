import { useEffect, useMemo, useRef } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { getSeriesList } from '../api/client.ts'
import type { SeriesListItemDto } from '../api/types.ts'
import { SeriesCard } from '../components/SeriesCard.tsx'
import { usePageData } from '../hooks/usePageData.ts'
import { useRestorableState } from '../hooks/useRestorableState.ts'
import {
  filterSeries,
  SERIES_PROGRESS_FILTER_OPTIONS,
  SERIES_STATUS_FILTER_OPTIONS,
  type SeriesProgressFilterValue,
  type SeriesSortKey,
  type SeriesStatusFilterValue,
  sortSeries,
} from '../utils/anime.ts'
import './SeriesBrowserPage.css'

interface SeriesBrowserReadState {
  items: SeriesListItemDto[]
  loadFailed: boolean
}

const SORT_OPTIONS: { value: SeriesSortKey; label: string }[] = [
  { value: 'myScore', label: 'My average' },
  { value: 'malScore', label: 'MAL average' },
  { value: 'alphabetical', label: 'Alphabetical' },
  { value: 'status', label: 'Status' },
  { value: 'newest', label: 'Newest' },
  { value: 'oldest', label: 'Oldest' },
  { value: 'myProgress', label: 'My progress' },
]

const DEFAULT_SORT: SeriesSortKey = 'myScore'

// Matches SeasonPage's own reveal chunk size — the whole list is already
// loaded (design.md D1 of add-series-browser), so this only controls how
// much of it is rendered at once, not a network page.
const PAGE_SIZE = 24

function isSortKey(value: string | null): value is SeriesSortKey {
  return value !== null && SORT_OPTIONS.some((option) => option.value === value)
}

function parseListParam<T extends string>(value: string | null, options: { value: T }[]): T[] {
  if (!value) return []
  const known = new Set(options.map((o) => o.value))
  return value.split(',').filter((v): v is T => known.has(v as T))
}

// The Series page: every stored series with a member in my list, as cards in
// the Season/Search grid form. The whole eligible set is fetched once
// (design.md D1) and revealed incrementally through an IntersectionObserver
// sentinel over the already-loaded array — no network paging — mirroring
// SearchPage's own whole-candidate-set-then-reveal pattern. Sort and the two
// filter groups live in the URL so a shared link and back-navigation land on
// the same view; the revealed count lives in useRestorableState (not
// useState) so a deep-scrolled restore has the cards to scroll back to
// (design.md D9, page-state-restoration delta).
export function SeriesBrowserPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const sortParam = searchParams.get('sort')
  const sort = isSortKey(sortParam) ? sortParam : DEFAULT_SORT
  const progressFilter = parseListParam<SeriesProgressFilterValue>(searchParams.get('progress'), SERIES_PROGRESS_FILTER_OPTIONS)
  const statusFilter = parseListParam<SeriesStatusFilterValue>(searchParams.get('status'), SERIES_STATUS_FILTER_OPTIONS)

  const { data, loading } = usePageData<SeriesBrowserReadState>('series-browser', () =>
    getSeriesList()
      .then((result) => ({ items: result.items, loadFailed: false }))
      .catch(() => ({ items: [], loadFailed: true })),
  )
  const items = data?.items ?? []
  const loadFailed = data?.loadFailed ?? false

  const [visibleCount, setVisibleCount] = useRestorableState('visibleCount', PAGE_SIZE)
  const sentinelRef = useRef<HTMLDivElement>(null)

  // Filtering (polish-series-badges-and-filters design.md D6) runs before
  // sorting; order between the two is immaterial since both are pure array
  // transforms over the whole list — the filtered set is still sorted by
  // whatever sort is active.
  const filteredItems = useMemo(
    () => filterSeries(items, progressFilter, statusFilter),
    [items, progressFilter, statusFilter],
  )
  const sortedItems = useMemo(() => sortSeries(filteredItems, sort), [filteredItems, sort])

  function setSort(next: SeriesSortKey) {
    setSearchParams((prev) => {
      const params = new URLSearchParams(prev)
      params.set('sort', next)
      return params
    })
  }

  function toggleParam(key: 'progress' | 'status', current: string[], value: string) {
    setSearchParams((prev) => {
      const params = new URLSearchParams(prev)
      const next = current.includes(value) ? current.filter((v) => v !== value) : [...current, value]
      if (next.length > 0) params.set(key, next.join(','))
      else params.delete(key)
      return params
    })
  }

  // Reveal more of the already-loaded, already-filtered-and-sorted array
  // once the sentinel enters view — no network call, everything is already
  // in memory.
  useEffect(() => {
    const node = sentinelRef.current
    if (!node) return

    const observer = new IntersectionObserver((entries) => {
      if (entries[0]?.isIntersecting) {
        setVisibleCount((prev) => Math.min(prev + PAGE_SIZE, sortedItems.length))
      }
    })
    observer.observe(node)
    return () => observer.disconnect()
  }, [sortedItems, setVisibleCount])

  const visibleItems = sortedItems.slice(0, visibleCount)

  // The page's terminal states: the grid whenever there's anything to show;
  // otherwise a loading indicator while the read is in flight; otherwise,
  // once loaded, either "no series match the filters" (something is stored
  // but the current filters exclude all of it) or "still being discovered"
  // (nothing is stored at all) — or "couldn't be loaded" on failure.
  // Different facts get different messages (spec "The Series page states
  // which empty situation it is in").
  const terminalState: 'grid' | 'loading' | 'loadFailed' | 'filtersEmpty' | 'empty' =
    visibleItems.length > 0
      ? 'grid'
      : loading
        ? 'loading'
        : loadFailed
          ? 'loadFailed'
          : items.length > 0
            ? 'filtersEmpty'
            : 'empty'

  return (
    <div className="series-browser-page">
      <div className="series-browser-page__header">
        <h1 className="series-browser-page__title">Series</h1>
        <div className="series-browser-page__controls">
          {/* Status (MAL's airing status) before Progress (my watch
              standing) — each group carries its own label so it's clear
              which is which, and each button takes the same colour its
              status pill/badge uses elsewhere. */}
          <div className="series-browser-page__filter-group">
            <span className="series-browser-page__filter-label">Status</span>
            <div role="group" aria-label="Filter by status" className="series-browser-page__filter-buttons">
              {SERIES_STATUS_FILTER_OPTIONS.map((option) => (
                <button
                  key={option.value}
                  type="button"
                  className={`series-browser-page__filter-button series-browser-page__filter-button--${option.value}${statusFilter.includes(option.value) ? ' series-browser-page__filter-button--active' : ''}`}
                  aria-pressed={statusFilter.includes(option.value)}
                  onClick={() => toggleParam('status', statusFilter, option.value)}
                >
                  {option.label}
                </button>
              ))}
            </div>
          </div>
          <div className="series-browser-page__filter-group">
            <span className="series-browser-page__filter-label">Progress</span>
            <div role="group" aria-label="Filter by progress" className="series-browser-page__filter-buttons">
              {SERIES_PROGRESS_FILTER_OPTIONS.map((option) => (
                <button
                  key={option.value}
                  type="button"
                  className={`series-browser-page__filter-button series-browser-page__filter-button--${option.value}${progressFilter.includes(option.value) ? ' series-browser-page__filter-button--active' : ''}`}
                  aria-pressed={progressFilter.includes(option.value)}
                  onClick={() => toggleParam('progress', progressFilter, option.value)}
                >
                  {option.label}
                </button>
              ))}
            </div>
          </div>
          <select
            className="series-browser-page__sort"
            value={sort}
            onChange={(event) => setSort(event.target.value as SeriesSortKey)}
            aria-label="Sort series"
          >
            {SORT_OPTIONS.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
        </div>
      </div>

      {terminalState === 'grid' && (
        <div className="series-browser-page__grid">
          {visibleItems.map((item) => (
            <SeriesCard key={item.seriesId} item={item} />
          ))}
        </div>
      )}
      {terminalState === 'filtersEmpty' && (
        <p className="series-browser-page__empty">No series match the selected filters.</p>
      )}
      {terminalState === 'empty' && (
        <p className="series-browser-page__empty">
          Series are still being discovered from your list. Use{' '}
          <Link to="/settings">"Build all series from my list"</Link> on the Settings page to fill this in now.
        </p>
      )}
      {terminalState === 'loadFailed' && (
        <p className="series-browser-page__empty">
          The series list couldn't be loaded — it'll be retried next time you open it.
        </p>
      )}

      <div ref={sentinelRef} className="series-browser-page__sentinel" />
      {terminalState === 'loading' && <p className="series-browser-page__loading">Loading…</p>}
    </div>
  )
}
