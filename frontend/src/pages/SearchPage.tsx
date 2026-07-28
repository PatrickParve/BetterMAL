import { useEffect, useRef, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { getSearchPage } from '../api/client.ts'
import type { AnimeBrowseItemDto } from '../api/types.ts'
import { AnimeCard, AnimeCardMeta } from '../components/AnimeCard.tsx'
import { useLatestRequest } from '../hooks/useLatestRequest.ts'
import './SearchPage.css'

type SortKey = 'relevance' | 'popularity' | 'malScore' | 'alphabetical' | 'myScore'

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
// from an anime detail page restores exactly where the user left off; the
// revealed-chunk count is not restored, matching Season's infinite scroll.
// A legacy `?page=N` link is simply ignored — the query itself still resolves.
export function SearchPage() {
  const [searchParams, setSearchParams] = useSearchParams()

  const q = searchParams.get('q') ?? ''
  const sortParam = searchParams.get('sort')
  const sort = isSortKey(sortParam) ? sortParam : 'relevance'

  const [items, setItems] = useState<AnimeBrowseItemDto[]>([])
  const [totalCount, setTotalCount] = useState(0)
  const [loading, setLoading] = useState(false)
  const [visibleCount, setVisibleCount] = useState(CHUNK_SIZE)
  const sentinelRef = useRef<HTMLDivElement>(null)
  const { start, isLatest } = useLatestRequest()

  function setSort(next: SortKey) {
    setSearchParams((prev) => {
      const params = new URLSearchParams(prev)
      params.set('sort', next)
      return params
    })
  }

  // Query or sort changed: fetch the whole candidate set once and reset the
  // reveal to the first chunk. An empty query skips the request entirely.
  useEffect(() => {
    setVisibleCount(CHUNK_SIZE)

    if (q.length === 0) {
      setItems([])
      setTotalCount(0)
      setLoading(false)
      return
    }

    const requestId = start()
    setLoading(true)
    getSearchPage(q, { sort, offset: 0, limit: CANDIDATE_LIMIT })
      .then((result) => {
        if (!isLatest(requestId)) return
        setItems(result.items)
        setTotalCount(result.totalCount)
      })
      .catch(() => {
        if (!isLatest(requestId)) return
        setItems([])
        setTotalCount(0)
      })
      .finally(() => {
        if (isLatest(requestId)) setLoading(false)
      })
  }, [q, sort])

  // Reveal more of the already-loaded array once the sentinel enters view —
  // no network call, everything for this (query, sort) is already in memory.
  useEffect(() => {
    const node = sentinelRef.current
    if (!node) return

    const observer = new IntersectionObserver((entries) => {
      if (entries[0]?.isIntersecting) {
        setVisibleCount((prev) => Math.min(prev + CHUNK_SIZE, items.length))
      }
    })
    observer.observe(node)
    return () => observer.disconnect()
  }, [items])

  const visibleItems = items.slice(0, visibleCount)

  return (
    <div className="search-page">
      <div className="search-page__header">
        <h1>{q.length > 0 ? <>Results for “{q}”</> : 'Search'}</h1>
        <div className="search-page__controls">
          {totalCount > 0 && <span className="search-page__count">{totalCount} results</span>}
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
      ) : items.length === 0 && !loading ? (
        <p className="search-page__empty">No anime found.</p>
      ) : (
        <div className="search-page__grid">
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
