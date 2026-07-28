import { Fragment, useEffect, useMemo, useRef, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { getSeasonPage, refreshSeason } from '../api/client.ts'
import type { AnimeBrowseItemDto } from '../api/types.ts'
import { AnimeCard, AnimeCardMeta } from '../components/AnimeCard.tsx'
import { useContentFilter } from '../context/ContentFilterContext.tsx'
import { useDebouncedValue } from '../hooks/useDebouncedValue.ts'
import { useLatestRequest } from '../hooks/useLatestRequest.ts'
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

// How long the season selection must sit still before a background refresh
// fires — so stepping quickly through seasons with the arrows only fetches
// the season actually landed on.
const REFRESH_DEBOUNCE_MS = 400

// Earliest year selectable in the quick-jump dropdown — anime predate this,
// but a bounded range keeps the <select> from growing unbounded.
const EARLIEST_YEAR = 1989

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
// IntersectionObserver sentinel below the grid. Year/season/sort/inMyList
// live in the URL (not component state) so the selection survives
// back-navigation from an anime detail page, and default to the current
// season when absent.
//
// Reads and refreshes are two separate effects (design §4): reading from
// cache is instant and re-runs on any sort/filter/season change, while the
// MAL refresh is debounced and keyed on season alone, so changing sort or
// the "in my list" filter never triggers a MAL fetch.
export function SeasonPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const fallback = useMemo(currentSeasonTarget, [])

  const yearParam = Number(searchParams.get('year'))
  const seasonParam = searchParams.get('season')
  const sortParam = searchParams.get('sort')
  const inMyListParam = searchParams.get('inMyList')

  const year = Number.isInteger(yearParam) && yearParam > 0 ? yearParam : fallback.year
  const season = isSeasonName(seasonParam) ? seasonParam : fallback.season
  const sort = isSortKey(sortParam) ? sortParam : 'popularity'
  const inMyList = inMyListParam !== '0'
  const { hideHentai } = useContentFilter()

  const [items, setItems] = useState<AnimeBrowseItemDto[]>([])
  const [totalCount, setTotalCount] = useState(0)
  const [loading, setLoading] = useState(false)
  const [lastFetchedAt, setLastFetchedAt] = useState<string | null>(null)
  const [refreshing, setRefreshing] = useState(false)
  const sentinelRef = useRef<HTMLDivElement>(null)
  const { start, current, isLatest } = useLatestRequest()

  // Read by the debounced refresh effect so it re-reads with whatever sort,
  // filter, and loaded-page-count are current when the refresh completes,
  // not whatever was current when the season settled.
  const sortRef = useRef(sort)
  sortRef.current = sort
  const inMyListRef = useRef(inMyList)
  inMyListRef.current = inMyList
  const hideHentaiRef = useRef(hideHentai)
  hideHentaiRef.current = hideHentai
  const itemsLengthRef = useRef(items.length)
  itemsLengthRef.current = items.length

  const hasMore = items.length < totalCount
  const neverCached = lastFetchedAt === null && items.length === 0
  const firstUnwatchedIndex = sort === 'myScore' ? items.findIndex((item) => item.myScore === null) : -1
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

  function setInMyList(next: boolean) {
    setSearchParams((prev) => {
      const params = new URLSearchParams(prev)
      if (next) params.delete('inMyList')
      else params.set('inMyList', '0')
      return params
    })
  }

  // Cache-first read: fires on every season/sort/filter change and never
  // touches MAL. Resets to page one so a sort or filter change behaves like a
  // fresh navigation.
  useEffect(() => {
    const requestId = start()
    setItems([])
    setTotalCount(0)
    setLoading(true)
    getSeasonPage(year, season, { sort, includeMyList: inMyList, hideHentai, offset: 0, limit: PAGE_SIZE })
      .then((page) => {
        if (!isLatest(requestId)) return
        setItems(page.items)
        setTotalCount(page.totalCount)
        setLastFetchedAt(page.lastFetchedAt)
      })
      .catch(() => {
        // Season page just stays empty; the user can retry via season nav.
      })
      .finally(() => {
        if (isLatest(requestId)) setLoading(false)
      })
  }, [year, season, sort, inMyList, hideHentai])

  // Visit-triggered background refresh: keyed on season alone (via the
  // debounced key below) so sort/filter changes never cause a MAL fetch.
  // Debounced so arrow-stepping through seasons only refreshes the one
  // settled on; the very first render's value is applied immediately since
  // useDebouncedValue seeds its state with the initial value.
  const debouncedSeasonKey = useDebouncedValue(`${year}/${season}`, REFRESH_DEBOUNCE_MS)

  useEffect(() => {
    const [yearPart, seasonPart] = debouncedSeasonKey.split('/')
    const targetYear = Number(yearPart)
    const targetSeason = seasonPart as SeasonName
    // Captured now, at the moment this season's refresh starts (the read
    // effect above already bumped it for this same season) — not after
    // refreshSeason resolves, so a slow refresh whose season has since been
    // navigated away from is still correctly recognized as stale.
    const requestId = current()

    let cancelled = false
    setRefreshing(true)

    refreshSeason(targetYear, targetSeason)
      .then((result) => {
        if (cancelled || !result.refreshed) return undefined
        return getSeasonPage(targetYear, targetSeason, {
          sort: sortRef.current,
          includeMyList: inMyListRef.current,
          hideHentai: hideHentaiRef.current,
          offset: 0,
          limit: Math.max(itemsLengthRef.current, PAGE_SIZE),
        }).then((page) => {
          if (cancelled || !isLatest(requestId)) return
          setItems(page.items)
          setTotalCount(page.totalCount)
          setLastFetchedAt(page.lastFetchedAt)
        })
      })
      .catch(() => {
        // A failed refresh leaves the cached page exactly as it is — no
        // error is surfaced, the indicator just clears below.
      })
      .finally(() => {
        if (!cancelled) setRefreshing(false)
      })

    return () => {
      cancelled = true
    }
  }, [debouncedSeasonKey])

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
      const requestId = current()
      setLoading(true)
      getSeasonPage(year, season, { sort, includeMyList: inMyList, hideHentai, offset: items.length, limit: PAGE_SIZE })
        .then((page) => {
          if (!isLatest(requestId)) return
          setItems((prev) => [...prev, ...page.items])
          setTotalCount(page.totalCount)
        })
        .catch(() => {})
        .finally(() => {
          if (isLatest(requestId)) setLoading(false)
        })
    }
  }, [items, loading, hasMore, year, season, sort, inMyList, hideHentai])

  return (
    <div className="season-page">
      <div className="season-page__header">
        <h1 className="season-page__title">Seasonal anime</h1>

        <div className="season-page__center">
          <div className="season-page__nav">
            <button type="button" onClick={() => setTarget(shiftSeason(year, season, -1))} aria-label="Previous season">
              &lsaquo;
            </button>
            <span className="season-page__label-wrap">
              <span className="season-page__label">
                {seasonLabel(season)} {year}
              </span>
              {refreshing && <span className="season-page__updating">Updating…</span>}
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
        </div>

        <div className="season-page__controls">
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
          <label className="season-page__checkbox">
            <input type="checkbox" checked={inMyList} onChange={(event) => setInMyList(event.target.checked)} />
            In my list
          </label>
        </div>
      </div>

      {items.length === 0 && !loading && !neverCached ? (
        <p className="season-page__empty">No anime found for this season.</p>
      ) : items.length > 0 ? (
        <div className="season-page__grid">
          {items.map((item, index) => (
            <Fragment key={item.animeId}>
              {index === firstUnwatchedIndex && index > 0 && <div className="season-page__divider">Unwatched</div>}
              <AnimeCard
                animeId={item.animeId}
                title={item.title}
                englishTitle={item.englishTitle}
                pictureUrl={item.pictureUrl}
                className="anime-card--fluid"
              >
                <AnimeCardMeta mediaType={item.mediaType} totalEpisodes={item.totalEpisodes} />
              </AnimeCard>
            </Fragment>
          ))}
        </div>
      ) : null}

      <div ref={sentinelRef} className="season-page__sentinel" />
      {(loading || neverCached) && <p className="season-page__loading">Loading…</p>}
    </div>
  )
}
