import { Fragment, useEffect, useMemo, useRef, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { getSeasonPage, refreshSeason } from '../api/client.ts'
import type { AnimeBrowseItemDto } from '../api/types.ts'
import { AnimeCard, AnimeCardMeta } from '../components/AnimeCard.tsx'
import { FilterMultiSelect, type FilterMultiSelectOption } from '../components/FilterMultiSelect.tsx'
import { useContentFilter } from '../context/ContentFilterContext.tsx'
import { useDebouncedValue } from '../hooks/useDebouncedValue.ts'
import { useLatestRequest } from '../hooks/useLatestRequest.ts'
import { usePageData } from '../hooks/usePageData.ts'
import { MEDIA_TYPE_ORDER, mediaTypeLabel } from '../utils/anime.ts'
import './SeasonPage.css'

interface SeasonReadState {
  items: AnimeBrowseItemDto[]
  totalCount: number
  lastFetchedAt: string | null
}

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
  const typeParam = searchParams.get('type')

  const year = Number.isInteger(yearParam) && yearParam > 0 ? yearParam : fallback.year
  const season = isSeasonName(seasonParam) ? seasonParam : fallback.season
  const sort = isSortKey(sortParam) ? sortParam : 'popularity'
  const inMyList = inMyListParam !== '0'
  const typeFilter = typeParam ? typeParam.split(',').filter(Boolean) : []
  const { hideHentai } = useContentFilter()

  // Keyed on season/year alone: a different season is a different history
  // snapshot, so restoring one restores its data regardless of which sort or
  // filter was active when it was left (D5). Sort/filter changes within the
  // same season are handled below via `reload`, not a second key.
  const seasonKey = `season:${year}/${season}`
  const {
    data: seasonData,
    setData: setSeasonData,
    loading,
    reload,
  } = usePageData<SeasonReadState>(seasonKey, () =>
    getSeasonPage(year, season, {
      sort,
      includeMyList: inMyList,
      hideHentai,
      types: typeFilter,
      offset: 0,
      limit: PAGE_SIZE,
    }).then((page) => ({ items: page.items, totalCount: page.totalCount, lastFetchedAt: page.lastFetchedAt })),
  )
  const items = seasonData?.items ?? []
  const totalCount = seasonData?.totalCount ?? 0
  const lastFetchedAt = seasonData?.lastFetchedAt ?? null

  const [loadingMore, setLoadingMore] = useState(false)
  const [refreshing, setRefreshing] = useState(false)
  const sentinelRef = useRef<HTMLDivElement>(null)
  const { start, current, isLatest } = useLatestRequest()
  const isLoading = loading || loadingMore

  // Read by the debounced refresh effect and the infinite-scroll effect so
  // they re-read with whatever sort, filter, and loaded-page-count are
  // current when they actually run, not whatever was current when they were
  // scheduled.
  const sortRef = useRef(sort)
  sortRef.current = sort
  const inMyListRef = useRef(inMyList)
  inMyListRef.current = inMyList
  const hideHentaiRef = useRef(hideHentai)
  hideHentaiRef.current = hideHentai
  const typeFilterRef = useRef(typeFilter)
  typeFilterRef.current = typeFilter
  const seenTypesRef = useRef<{ key: string; types: Set<string>; hasUnknown: boolean }>({
    key: seasonKey,
    types: new Set<string>(),
    hasUnknown: false,
  })
  const itemsLengthRef = useRef(items.length)
  itemsLengthRef.current = items.length
  const reloadRef = useRef(reload)
  reloadRef.current = reload

  const hasMore = items.length < totalCount
  const neverCached = lastFetchedAt === null && items.length === 0
  const firstUnwatchedIndex = sort === 'myScore' ? items.findIndex((item) => item.myScore === null) : -1
  const yearOptions = useMemo(() => {
    const latest = Math.max(year, fallback.year) + 1
    return Array.from({ length: latest - EARLIEST_YEAR + 1 }, (_, i) => latest - i)
  }, [year, fallback.year])

  // Type filter options: every media type seen across this season's loaded
  // pages, mirroring MyListPage's presentTypes/hasUnknownType (D6) but
  // accumulated rather than a one-shot read of `items` — the filter runs
  // server-side, so once a type is selected `items` only ever contains that
  // type again, and a naive re-derivation from `items` alone would make every
  // other type disappear from the picker the moment one is chosen. Reset when
  // the season itself changes; mutated directly during render (not an effect)
  // so newly discovered types are reflected in the same pass that loaded them.
  if (seenTypesRef.current.key !== seasonKey) {
    seenTypesRef.current = { key: seasonKey, types: new Set<string>(), hasUnknown: false }
  }
  for (const item of items) {
    if (item.mediaType) seenTypesRef.current.types.add(item.mediaType)
    else seenTypesRef.current.hasUnknown = true
  }

  const typeOptions = useMemo(() => {
    const { types: presentTypes, hasUnknown: hasUnknownType } = seenTypesRef.current
    const options: FilterMultiSelectOption[] = MEDIA_TYPE_ORDER.filter((value) => presentTypes.has(value)).map(
      (value) => ({ value, label: mediaTypeLabel(value) }),
    )
    if (hasUnknownType) options.push({ value: 'unknown', label: 'Unknown' })
    return options
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [items, seasonKey])

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

  function setTypeFilter(next: string[]) {
    setSearchParams((prev) => {
      const params = new URLSearchParams(prev)
      if (next.length > 0) params.set('type', next.join(','))
      else params.delete('type')
      return params
    })
  }

  // Any parameter the cache-first read below depends on invalidates
  // in-flight background-refresh/load-more requests started before the
  // change (mirroring the generation guard usePageData applies to its own
  // load internally, for the two fetches that sit outside it).
  useEffect(() => {
    start()
  }, [seasonKey, sort, inMyList, hideHentai, typeParam])

  // Sort/filter changed within the same season: the season's initial or
  // restored load is already covered by usePageData itself (above), so this
  // only fires on a later change, reusing `reload`'s own generation guard
  // rather than opening a second, independently-raced fetch. Read through a
  // ref so a season change (which gives `reload` a new identity) doesn't
  // also retrigger this effect.
  const isFirstFilterRun = useRef(true)
  useEffect(() => {
    if (isFirstFilterRun.current) {
      isFirstFilterRun.current = false
      return
    }
    reloadRef.current()
  }, [sort, inMyList, hideHentai, typeParam])

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
    // Captured now, at the moment this season's refresh starts (the effect
    // above already bumped it for this same season) — not after
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
          types: typeFilterRef.current,
          offset: 0,
          limit: Math.max(itemsLengthRef.current, PAGE_SIZE),
        }).then((page) => {
          if (cancelled || !isLatest(requestId)) return
          setSeasonData({ items: page.items, totalCount: page.totalCount, lastFetchedAt: page.lastFetchedAt })
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
      if (isLoading || !hasMore) return
      const requestId = current()
      setLoadingMore(true)
      getSeasonPage(year, season, {
        sort,
        includeMyList: inMyList,
        hideHentai,
        types: typeFilter,
        offset: items.length,
        limit: PAGE_SIZE,
      })
        .then((page) => {
          if (!isLatest(requestId)) return
          setSeasonData((prev) =>
            prev ? { ...prev, items: [...prev.items, ...page.items], totalCount: page.totalCount } : prev,
          )
        })
        .catch(() => {})
        .finally(() => {
          // Unconditional: a superseded request must still clear the
          // in-flight flag or a param change (sort/filter) that invalidates
          // it mid-request leaves `loadingMore` stuck true forever, silently
          // blocking every future scroll-triggered load. Only *applying* a
          // stale result to state is gated by isLatest, in the .then above.
          setLoadingMore(false)
        })
    }
  }, [items, isLoading, hasMore, year, season, sort, inMyList, hideHentai, typeParam])

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
          {typeOptions.length > 0 && (
            <FilterMultiSelect label="Type" options={typeOptions} selected={typeFilter} onChange={setTypeFilter} />
          )}
          <label className="season-page__checkbox">
            <input type="checkbox" checked={inMyList} onChange={(event) => setInMyList(event.target.checked)} />
            In my list
          </label>
        </div>
      </div>

      {items.length === 0 && !isLoading && !neverCached ? (
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
      {(isLoading || neverCached) && <p className="season-page__loading">Loading…</p>}
    </div>
  )
}
