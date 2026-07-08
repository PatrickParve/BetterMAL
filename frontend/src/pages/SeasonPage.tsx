import { useEffect, useMemo, useRef, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { getSeasonPage } from '../api/client.ts'
import type { SeasonAnimeItemDto } from '../api/types.ts'
import { AnimeCard } from '../components/AnimeCard.tsx'
import './SeasonPage.css'

const SEASON_ORDER = ['winter', 'spring', 'summer', 'fall'] as const
type SeasonName = (typeof SEASON_ORDER)[number]
type SortKey = 'popularity' | 'malScore' | 'alphabetical' | 'myScore'

const SORT_OPTIONS: { value: SortKey; label: string }[] = [
  { value: 'popularity', label: 'Popularity' },
  { value: 'malScore', label: 'MAL score' },
  { value: 'alphabetical', label: 'Alphabetical' },
  { value: 'myScore', label: 'My score' },
]

const PAGE_SIZE = 24

// Earliest year selectable in the quick-jump dropdown — anime predate this,
// but a bounded range keeps the <select> from growing unbounded.
const EARLIEST_YEAR = 1960

function currentSeasonTarget(): { year: number; season: SeasonName } {
  const now = new Date()
  return { year: now.getFullYear(), season: SEASON_ORDER[Math.floor(now.getMonth() / 3)] }
}

function shiftSeason(year: number, season: SeasonName, direction: 1 | -1): { year: number; season: SeasonName } {
  const index = SEASON_ORDER.indexOf(season)
  const shifted = index + direction
  const nextIndex = (shifted + 4) % 4
  const yearDelta = shifted < 0 ? -1 : shifted > 3 ? 1 : 0
  return { year: year + yearDelta, season: SEASON_ORDER[nextIndex] }
}

function seasonLabel(season: SeasonName): string {
  return season.charAt(0).toUpperCase() + season.slice(1)
}

function isSeasonName(value: string | null): value is SeasonName {
  return value !== null && (SEASON_ORDER as readonly string[]).includes(value)
}

function isSortKey(value: string | null): value is SortKey {
  return value !== null && SORT_OPTIONS.some((option) => option.value === value)
}

// Season page: all anime airing in the selected season (not just my list),
// with a sort/filter control and hand-rolled infinite scroll via an
// IntersectionObserver sentinel below the grid. Year/season/sort live in the
// URL (not component state) so the selection survives back-navigation from
// an anime detail page, and default to the current season when absent.
export function SeasonPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const fallback = useMemo(currentSeasonTarget, [])

  const yearParam = Number(searchParams.get('year'))
  const seasonParam = searchParams.get('season')
  const sortParam = searchParams.get('sort')

  const year = Number.isInteger(yearParam) && yearParam > 0 ? yearParam : fallback.year
  const season = isSeasonName(seasonParam) ? seasonParam : fallback.season
  const sort = isSortKey(sortParam) ? sortParam : 'popularity'

  const [items, setItems] = useState<SeasonAnimeItemDto[]>([])
  const [totalCount, setTotalCount] = useState(0)
  const [loading, setLoading] = useState(false)
  const sentinelRef = useRef<HTMLDivElement>(null)
  const requestIdRef = useRef(0)

  const hasMore = items.length < totalCount
  const yearOptions = useMemo(() => {
    const latest = Math.max(year, fallback.year) + 1
    return Array.from({ length: latest - EARLIEST_YEAR + 1 }, (_, i) => latest - i)
  }, [year, fallback.year])

  function setTarget(next: { year: number; season: SeasonName }) {
    setSearchParams((prev) => {
      const params = new URLSearchParams(prev)
      params.set('year', String(next.year))
      params.set('season', next.season)
      return params
    })
  }

  function setSort(next: SortKey) {
    setSearchParams((prev) => {
      const params = new URLSearchParams(prev)
      params.set('sort', next)
      return params
    })
  }

  // Season or sort changed: start over from page one.
  useEffect(() => {
    const requestId = ++requestIdRef.current
    setItems([])
    setTotalCount(0)
    setLoading(true)
    getSeasonPage(year, season, { sort, offset: 0, limit: PAGE_SIZE })
      .then((page) => {
        if (requestIdRef.current !== requestId) return
        setItems(page.items)
        setTotalCount(page.totalCount)
      })
      .catch(() => {
        // Season page just stays empty; the user can retry via season nav.
      })
      .finally(() => {
        if (requestIdRef.current === requestId) setLoading(false)
      })
  }, [year, season, sort])

  // Infinite scroll: load the next page once the sentinel enters view.
  useEffect(() => {
    const node = sentinelRef.current
    if (!node) return

    const observer = new IntersectionObserver((entries) => {
      if (entries[0]?.isIntersecting) loadMore()
    })
    observer.observe(node)
    return () => observer.disconnect()

    function loadMore() {
      if (loading || !hasMore) return
      const requestId = requestIdRef.current
      setLoading(true)
      getSeasonPage(year, season, { sort, offset: items.length, limit: PAGE_SIZE })
        .then((page) => {
          if (requestIdRef.current !== requestId) return
          setItems((prev) => [...prev, ...page.items])
          setTotalCount(page.totalCount)
        })
        .catch(() => {})
        .finally(() => {
          if (requestIdRef.current === requestId) setLoading(false)
        })
    }
  }, [items, loading, hasMore, year, season, sort])

  return (
    <div className="season-page">
      <div className="season-page__header">
        <h1>Seasonal anime</h1>
        <div className="season-page__nav">
          <button type="button" onClick={() => setTarget(shiftSeason(year, season, -1))} aria-label="Previous season">
            &lsaquo;
          </button>
          <span className="season-page__label">
            {seasonLabel(season)} {year}
          </span>
          <button type="button" onClick={() => setTarget(shiftSeason(year, season, 1))} aria-label="Next season">
            &rsaquo;
          </button>
        </div>
        <div className="season-page__jump">
          <select
            className="season-page__sort"
            value={season}
            onChange={(event) => setTarget({ year, season: event.target.value as SeasonName })}
            aria-label="Jump to season"
          >
            {SEASON_ORDER.map((option) => (
              <option key={option} value={option}>
                {seasonLabel(option)}
              </option>
            ))}
          </select>
          <select
            className="season-page__sort"
            value={year}
            onChange={(event) => setTarget({ year: Number(event.target.value), season })}
            aria-label="Jump to year"
          >
            {yearOptions.map((option) => (
              <option key={option} value={option}>
                {option}
              </option>
            ))}
          </select>
        </div>
        <select
          className="season-page__sort"
          value={sort}
          onChange={(event) => setSort(event.target.value as SortKey)}
          aria-label="Sort season anime"
        >
          {SORT_OPTIONS.map((option) => (
            <option key={option.value} value={option.value}>
              {option.label}
            </option>
          ))}
        </select>
      </div>

      {items.length === 0 && !loading ? (
        <p className="season-page__empty">No anime found for this season.</p>
      ) : (
        <div className="season-page__grid">
          {items.map((item) => (
            <AnimeCard
              key={item.animeId}
              animeId={item.animeId}
              title={item.title}
              englishTitle={item.englishTitle}
              pictureUrl={item.pictureUrl}
            >
              <span className="season-card__meta">
                {item.mediaType ? item.mediaType.toUpperCase() : 'Unknown'} · {item.totalEpisodes ?? '?'} ep
              </span>
            </AnimeCard>
          ))}
        </div>
      )}

      <div ref={sentinelRef} className="season-page__sentinel" />
      {loading && <p className="season-page__loading">Loading…</p>}
    </div>
  )
}
