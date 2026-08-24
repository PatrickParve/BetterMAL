import { Fragment, useEffect, useMemo, useRef, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { getSeasonBounds, getYearPage, refreshYear } from '../api/client.ts'
import type { AnimeBrowseItemDto } from '../api/types.ts'
import { RECAP_SEASONS, type RecapSeasonName } from '../api/types.ts'
import { AnimeCard, AnimeCardMeta } from '../components/AnimeCard.tsx'
import { FilterMultiSelect, type FilterMultiSelectOption } from '../components/FilterMultiSelect.tsx'
import { useContentFilter } from '../context/ContentFilterContext.tsx'
import { useDebouncedValue } from '../hooks/useDebouncedValue.ts'
import { useLatestRequest } from '../hooks/useLatestRequest.ts'
import { usePageData } from '../hooks/usePageData.ts'
import { MEDIA_TYPE_ORDER, mediaTypeLabel, shiftSeason } from '../utils/anime.ts'
import { EARLIEST_YEAR, FUTURE_SEASON_WINDOW } from './SeasonPage.tsx'
import './YearPage.css'

// Year page: every anime MAL classifies under any of the selected year's four
// seasons, combined into one grid (add-year-browser design D1 — the union is
// resolved server-side, not merged here). Its effect structure deliberately
// mirrors SeasonPage's: the two-effect split (cache-first read vs. debounced
// MAL refresh), the useLatestRequest generation guard, the accumulated
// type-option set, and the four-way terminal state were each a bug at some
// point on the season page (design D4) and are reproduced here rather than
// reinvented, sharing EARLIEST_YEAR/FUTURE_SEASON_WINDOW with it so the two
// pages' horizons can never drift apart.

interface YearReadState {
  items: AnimeBrowseItemDto[]
  totalCount: number
  lastFetchedAt: string | null
  hasListing: boolean
}

type SortKey = 'popularity' | 'malScore' | 'alphabetical' | 'myScore'

const SORT_OPTIONS: { value: SortKey; label: string }[] = [
  { value: 'popularity', label: 'Popularity' },
  { value: 'malScore', label: 'MAL score' },
  { value: 'alphabetical', label: 'Alphabetical' },
  { value: 'myScore', label: 'My score' },
]

const PAGE_SIZE = 24

// Mirrors SeasonPage's own debounce (tasks.md 5.3) — worth four times as
// much here, since each skipped year is four season refreshes not started.
const REFRESH_DEBOUNCE_MS = 400

function currentYearAndSeason(): { year: number; season: RecapSeasonName } {
  const now = new Date()
  return { year: now.getFullYear(), season: RECAP_SEASONS[Math.floor(now.getMonth() / 3)] }
}

function isSortKey(value: string | null): value is SortKey {
  return value !== null && SORT_OPTIONS.some((option) => option.value === value)
}

export function YearPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const fallback = useMemo(currentYearAndSeason, [])

  const yearParam = Number(searchParams.get('year'))
  const sortParam = searchParams.get('sort')
  const inMyListParam = searchParams.get('inMyList')
  const typeParam = searchParams.get('type')

  const year = Number.isInteger(yearParam) && yearParam > 0 ? yearParam : fallback.year
  const sort = isSortKey(sortParam) ? sortParam : 'popularity'
  const inMyList = inMyListParam !== '0'
  const typeFilter = typeParam ? typeParam.split(',').filter(Boolean) : []
  const { hideHentai } = useContentFilter()

  // Keyed on the year alone (design D5): a different year is a different
  // history snapshot, restored with whatever it held regardless of which
  // sort or filter was active when it was left. Sort/filter changes within
  // one year go through `reload`, not a second key.
  const yearKey = `year:${year}`
  const {
    data: yearData,
    setData: setYearData,
    loading,
    reload,
  } = usePageData<YearReadState>(yearKey, () =>
    getYearPage(year, {
      sort,
      includeMyList: inMyList,
      hideHentai,
      types: typeFilter,
      offset: 0,
      limit: PAGE_SIZE,
    }).then((page) => ({
      items: page.items,
      totalCount: page.totalCount,
      lastFetchedAt: page.lastFetchedAt,
      hasListing: page.hasListing,
    })),
  )
  const items = yearData?.items ?? []
  const totalCount = yearData?.totalCount ?? 0
  const lastFetchedAt = yearData?.lastFetchedAt ?? null
  const hasListing = yearData?.hasListing ?? false

  const [loadingMore, setLoadingMore] = useState(false)
  const [refreshing, setRefreshing] = useState(false)
  // The refresh outcome for the year currently being viewed, reset the
  // moment the year changes (below) so one year's outcome can never decide
  // another year's terminal-state render. Null until a refresh attempt for
  // this year has settled.
  const [refreshOutcome, setRefreshOutcome] = useState<'fetched' | 'notListed' | 'skipped' | 'failed' | null>(null)
  const sentinelRef = useRef<HTMLDivElement>(null)
  const { start, current, isLatest } = useLatestRequest()
  const isLoading = loading || loadingMore

  // The navigable ceiling — the furthest year selectable. Seeded with the
  // year of the same current+2 default the season page seeds (design D3),
  // not a flat currentYear + 1 — winter + 2 is summer of the *same* year, so
  // a flat +1 would be wrong for half the calendar. Replaced once
  // GET /api/season/bounds resolves, ignored on failure.
  const [ceiling, setCeiling] = useState(() => shiftSeason(fallback.year, fallback.season, FUTURE_SEASON_WINDOW).year)

  useEffect(() => {
    let cancelled = false
    getSeasonBounds()
      .then((bounds) => {
        if (cancelled) return
        setCeiling(bounds.latestYear)
      })
      .catch(() => {
        // Keep the client-computed default ceiling — an honest fallback
        // rather than blocking navigation on this request.
      })
    return () => {
      cancelled = true
    }
  }, [])

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
    key: yearKey,
    types: new Set<string>(),
    hasUnknown: false,
  })
  const itemsLengthRef = useRef(items.length)
  itemsLengthRef.current = items.length
  const lastFetchedAtRef = useRef(lastFetchedAt)
  lastFetchedAtRef.current = lastFetchedAt
  const reloadRef = useRef(reload)
  reloadRef.current = reload

  const hasMore = items.length < totalCount
  const firstUnwatchedIndex = sort === 'myScore' ? items.findIndex((item) => item.myScore === null) : -1

  // The page's terminal states, in the season page's own order (tasks.md
  // 5.5): the grid takes priority whenever there's anything to show;
  // otherwise a cached-but-empty year reports why (no listing vs. filtered
  // out); otherwise the year has never been cached, so it's either still
  // loading or its first fetch has settled and produced nothing.
  const terminalState: 'grid' | 'filtersEmpty' | 'notListed' | 'loading' | 'loadFailed' =
    items.length > 0
      ? 'grid'
      : lastFetchedAt !== null && hasListing
        ? 'filtersEmpty'
        : lastFetchedAt !== null
          ? 'notListed'
          : refreshOutcome === null
            ? 'loading'
            : 'loadFailed'

  // At or past the ceiling, keeping the viewed year in the list so a
  // URL-addressed year past the horizon still shows its own year rather than
  // a <select> with a value it doesn't offer.
  const yearOptions = useMemo(() => {
    const earliest = Math.min(EARLIEST_YEAR, year)
    const latest = Math.max(ceiling, year)
    return Array.from({ length: latest - earliest + 1 }, (_, i) => latest - i)
  }, [year, ceiling])

  // Type filter options: every media type seen across this year's loaded
  // pages, accumulated rather than a one-shot read of `items` — the filter
  // runs server-side, so once a type is selected `items` only ever contains
  // that type again, and a naive re-derivation from `items` alone would make
  // every other type disappear from the picker the moment one is chosen.
  // Reset when the year itself changes; mutated directly during render (not
  // an effect) so newly discovered types are reflected in the same pass that
  // loaded them.
  if (seenTypesRef.current.key !== yearKey) {
    seenTypesRef.current = { key: yearKey, types: new Set<string>(), hasUnknown: false }
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
  }, [items, yearKey])

  function setYear(next: number) {
    setSearchParams((prev) => {
      const params = new URLSearchParams(prev)
      params.set('year', String(next))
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
  }, [yearKey, sort, inMyList, hideHentai, typeParam])

  // Sort/filter changed within the same year: the year's initial or restored
  // load is already covered by usePageData itself (above), so this only
  // fires on a later change, reusing `reload`'s own generation guard rather
  // than opening a second, independently-raced fetch. Read through a ref so
  // a year change (which gives `reload` a new identity) doesn't also
  // retrigger this effect.
  const isFirstFilterRun = useRef(true)
  useEffect(() => {
    if (isFirstFilterRun.current) {
      isFirstFilterRun.current = false
      return
    }
    reloadRef.current()
  }, [sort, inMyList, hideHentai, typeParam])

  // The outcome from a stale year must never decide this year's render —
  // reset the instant the year changes, ahead of the debounced refresh
  // effect below settling for whichever year is landed on.
  useEffect(() => {
    setRefreshOutcome(null)
  }, [yearKey])

  // Visit-triggered background refresh: keyed on the year alone (via the
  // debounced value below) so sort/filter changes never cause a MAL fetch.
  // Debounced so arrow-stepping through years only refreshes the one settled
  // on; the very first render's value is applied immediately since
  // useDebouncedValue seeds its state with the initial value.
  const debouncedYear = useDebouncedValue(year, REFRESH_DEBOUNCE_MS)

  useEffect(() => {
    const targetYear = debouncedYear
    // Captured now, at the moment this year's refresh starts (the effect
    // above already bumped it for this same year) — not after refreshYear
    // resolves, so a slow refresh whose year has since been navigated away
    // from is still correctly recognized as stale.
    const requestId = current()

    let cancelled = false
    setRefreshing(true)

    refreshYear(targetYear)
      .then((result) => {
        if (cancelled) return undefined
        setRefreshOutcome(result.outcome)

        // Re-read on fetched/notListed (new data, or the fact settled). On
        // skipped (already fetched today by someone else) only when this tab
        // has nothing cached itself — the cross-tab race where another tab's
        // fetch landed between this tab's own cache read and its refresh
        // call. Never on failed: the cached page, if any, is left exactly as
        // it is.
        const shouldReread =
          result.outcome === 'fetched' ||
          result.outcome === 'notListed' ||
          (result.outcome === 'skipped' && lastFetchedAtRef.current === null)
        if (!shouldReread) return undefined

        return getYearPage(targetYear, {
          sort: sortRef.current,
          includeMyList: inMyListRef.current,
          hideHentai: hideHentaiRef.current,
          types: typeFilterRef.current,
          offset: 0,
          limit: Math.max(itemsLengthRef.current, PAGE_SIZE),
        }).then((page) => {
          if (cancelled || !isLatest(requestId)) return
          setYearData({
            items: page.items,
            totalCount: page.totalCount,
            lastFetchedAt: page.lastFetchedAt,
            hasListing: page.hasListing,
          })
        })
      })
      .catch(() => {
        // A failed refresh leaves the cached page exactly as it is — no
        // error is surfaced, the indicator just clears below.
        if (!cancelled) setRefreshOutcome('failed')
      })
      .finally(() => {
        if (!cancelled) setRefreshing(false)
      })

    return () => {
      cancelled = true
    }
  }, [debouncedYear])

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
      getYearPage(year, {
        sort,
        includeMyList: inMyList,
        hideHentai,
        types: typeFilter,
        offset: items.length,
        limit: PAGE_SIZE,
      })
        .then((page) => {
          if (!isLatest(requestId)) return
          setYearData((prev) =>
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
  }, [items, isLoading, hasMore, year, sort, inMyList, hideHentai, typeParam])

  return (
    <div className="year-page">
      <div className="year-page__header">
        <h1 className="year-page__title">Yearly anime</h1>

        <div className="year-page__center">
          <div className="year-page__nav">
            <button
              type="button"
              onClick={() => setYear(year - 1)}
              aria-label="Previous year"
              disabled={year <= EARLIEST_YEAR}
            >
              &lsaquo;
            </button>
            <span className="year-page__label-wrap">
              <span className="year-page__label">{year}</span>
              {refreshing && <span className="year-page__updating">Updating…</span>}
            </span>
            <button type="button" onClick={() => setYear(year + 1)} aria-label="Next year" disabled={year >= ceiling}>
              &rsaquo;
            </button>
          </div>
          <div className="year-page__jump">
            <select
              className="year-page__sort"
              value={year}
              onChange={(event) => setYear(Number(event.target.value))}
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

        <div className="year-page__controls">
          <select
            className="year-page__sort"
            value={sort}
            onChange={(event) => setSort(event.target.value as SortKey)}
            aria-label="Sort year anime"
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
          <label className="year-page__checkbox">
            <input type="checkbox" checked={inMyList} onChange={(event) => setInMyList(event.target.checked)} />
            In my list
          </label>
        </div>
      </div>

      {terminalState === 'grid' && (
        <div className="year-page__grid">
          {items.map((item, index) => (
            <Fragment key={item.animeId}>
              {index === firstUnwatchedIndex && index > 0 && <div className="year-page__divider">Unwatched</div>}
              <AnimeCard
                animeId={item.animeId}
                title={item.title}
                englishTitle={item.englishTitle}
                pictureUrl={item.pictureUrl}
                className="anime-card--fluid"
              >
                <AnimeCardMeta mediaType={item.mediaType} totalEpisodes={item.totalEpisodes} malScore={item.malScore} />
              </AnimeCard>
            </Fragment>
          ))}
        </div>
      )}
      {terminalState === 'filtersEmpty' && <p className="year-page__empty">No anime match the current filters.</p>}
      {terminalState === 'notListed' && <p className="year-page__empty">MyAnimeList hasn't listed this year yet.</p>}
      {terminalState === 'loadFailed' && (
        <p className="year-page__empty">This year couldn't be loaded — it'll be retried next time you open it.</p>
      )}

      <div ref={sentinelRef} className="year-page__sentinel" />
      {(terminalState === 'loading' || loadingMore) && <p className="year-page__loading">Loading…</p>}
    </div>
  )
}
