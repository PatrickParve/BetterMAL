import { useEffect, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { getSearchPage } from '../api/client.ts'
import type { AnimeBrowseItemDto } from '../api/types.ts'
import { AnimeCard, AnimeCardMeta } from '../components/AnimeCard.tsx'
import { Pagination } from '../components/Pagination.tsx'
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

const PAGE_SIZE = 50

function isSortKey(value: string | null): value is SortKey {
  return value !== null && SORT_OPTIONS.some((option) => option.value === value)
}

// Full search results page: paginated (not infinite scroll, unlike Season),
// sourced from MAL's own search relevance by default. Query/sort/page all
// live in the URL so back-navigation from an anime detail page restores
// exactly where the user left off.
export function SearchPage() {
  const [searchParams, setSearchParams] = useSearchParams()

  const q = searchParams.get('q') ?? ''
  const sortParam = searchParams.get('sort')
  const sort = isSortKey(sortParam) ? sortParam : 'relevance'
  const pageParam = Number(searchParams.get('page'))
  const page = Number.isInteger(pageParam) && pageParam > 0 ? pageParam : 1

  const [items, setItems] = useState<AnimeBrowseItemDto[]>([])
  const [totalCount, setTotalCount] = useState(0)
  const [loading, setLoading] = useState(false)
  const { start, isLatest } = useLatestRequest()

  function setSort(next: SortKey) {
    setSearchParams((prev) => {
      const params = new URLSearchParams(prev)
      params.set('sort', next)
      params.set('page', '1')
      return params
    })
  }

  function setPage(next: number) {
    setSearchParams((prev) => {
      const params = new URLSearchParams(prev)
      params.set('page', String(next))
      return params
    })
  }

  // Query, sort, or page changed: (re)fetch this page. An empty query skips
  // the request entirely — there's nothing to search for.
  useEffect(() => {
    if (q.length === 0) {
      setItems([])
      setTotalCount(0)
      setLoading(false)
      return
    }

    const requestId = start()
    setLoading(true)
    getSearchPage(q, { sort, offset: (page - 1) * PAGE_SIZE, limit: PAGE_SIZE })
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
  }, [q, sort, page])

  const totalPages = Math.ceil(totalCount / PAGE_SIZE)

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
          {items.map((item) => (
            <AnimeCard
              key={item.animeId}
              animeId={item.animeId}
              title={item.title}
              englishTitle={item.englishTitle}
              pictureUrl={item.pictureUrl}
            >
              <AnimeCardMeta mediaType={item.mediaType} totalEpisodes={item.totalEpisodes} />
            </AnimeCard>
          ))}
        </div>
      )}

      {loading && <p className="search-page__loading">Loading…</p>}

      <Pagination currentPage={page} totalPages={totalPages} onPageChange={setPage} variant="full" />
    </div>
  )
}
