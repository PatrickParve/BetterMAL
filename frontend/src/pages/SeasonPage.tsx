import { useEffect, useRef, useState } from 'react'
import { getSeasonPage } from '../api/client.ts'
import type { SeasonAnimeItemDto } from '../api/types.ts'
import { AnimeCard } from '../components/AnimeCard.tsx'
import { ScoreValue } from '../components/ScoreValue.tsx'
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

// Season page: all anime airing in the selected season (not just my list),
// with a sort/filter control and hand-rolled infinite scroll via an
// IntersectionObserver sentinel below the grid.
export function SeasonPage() {
  const [target, setTarget] = useState(currentSeasonTarget)
  const [sort, setSort] = useState<SortKey>('popularity')
  const [items, setItems] = useState<SeasonAnimeItemDto[]>([])
  const [totalCount, setTotalCount] = useState(0)
  const [loading, setLoading] = useState(false)
  const sentinelRef = useRef<HTMLDivElement>(null)
  const requestIdRef = useRef(0)

  const hasMore = items.length < totalCount

  // Season or sort changed: start over from page one.
  useEffect(() => {
    const requestId = ++requestIdRef.current
    setItems([])
    setTotalCount(0)
    setLoading(true)
    getSeasonPage(target.year, target.season, { sort, offset: 0, limit: PAGE_SIZE })
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
  }, [target.year, target.season, sort])

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
      getSeasonPage(target.year, target.season, { sort, offset: items.length, limit: PAGE_SIZE })
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
  }, [items, loading, hasMore, target, sort])

  return (
    <div className="season-page">
      <div className="season-page__header">
        <h1>Seasonal anime</h1>
        <div className="season-page__nav">
          <button type="button" onClick={() => setTarget((t) => shiftSeason(t.year, t.season, -1))} aria-label="Previous season">
            &lsaquo;
          </button>
          <span className="season-page__label">
            {seasonLabel(target.season)} {target.year}
          </span>
          <button type="button" onClick={() => setTarget((t) => shiftSeason(t.year, t.season, 1))} aria-label="Next season">
            &rsaquo;
          </button>
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
            <AnimeCard key={item.animeId} animeId={item.animeId} title={item.title} pictureUrl={item.pictureUrl}>
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
