import { Fragment, useEffect, useMemo, useRef, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { getSeasonBounds, getYearPage, refreshYear } from '../api/client.ts'
import type { AnimeBrowseItemDto } from '../api/types.ts'
import { RECAP_SEASONS, type RecapSeasonName } from '../api/types.ts'
import { AnimeCard, AnimeCardMeta } from '../components/AnimeCard.tsx'
import { FilterMultiSelect, type FilterMultiSelectOption } from '../components/FilterMultiSelect.tsx'
import { useContentFilter } from '../context/ContentFilterContext.tsx'
import { useDebouncedValue } from '../hooks/useDebouncedValue.ts'
import { usePageData } from '../hooks/usePageData.ts'
import { useRestorableState } from '../hooks/useRestorableState.ts'
import { MEDIA_TYPE_ORDER, mediaTypeLabel, shiftSeason } from '../utils/anime.ts'
import { EARLIEST_YEAR, FUTURE_SEASON_WINDOW } from './SeasonPage.tsx'
import './YearPage.css'

// Year page: every anime MAL classifies under any of the selected year's four
// seasons, combined into one grid (add-year-browser design D1 — the union is
// resolved server-side, not merged here). Its effect structure deliberately
// mirrors SeasonPage's: the two-effect split (cache-first whole-listing read
// vs. debounced MAL refresh) and the four-way terminal state were each a bug
// at some point on the season page and are reproduced here rather than
// reinvented, sharing EARLIEST_YEAR/FUTURE_SEASON_WINDOW with it so the two
// pages' horizons can never drift apart. Sort, the type filter, and the
// in-my-list filter never trigger a read — they act on the already-loaded
// listing in place, sorting by the server-computed sortOrder key rather than
// reimplementing any ordering rule (design D2, D3).

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

  // Keyed on year/hideHentai: those are the only things a read depends on
  // now that sort and the two page filters act on the already-loaded listing
  // (design D1-D3). A different key is a different history snapshot,
  // restored with its own whole listing regardless of which sort or filter
  // was active when it was left.
  const yearKey = `year:${year}:${hideHentai}`
  const { data: yearData, setData: setYearData } = usePageData<YearReadState>(yearKey, () =>
    getYearPage(year, { hideHentai }).then((page) => ({
      items: page.items,
      totalCount: page.totalCount,
      lastFetchedAt: page.lastFetchedAt,
      hasListing: page.hasListing,
    })),
  )
  const items = yearData?.items ?? []
  const lastFetchedAt = yearData?.lastFetchedAt ?? null
  const hasListing = yearData?.hasListing ?? false

  const [refreshing, setRefreshing] = useState(false)
  // The refresh outcome for the year currently being viewed, reset the
  // moment the year changes (below) so one year's outcome can never decide
  // another year's terminal-state render. Null until a refresh attempt for
  // this year has settled.
  const [refreshOutcome, setRefreshOutcome] = useState<'fetched' | 'notListed' | 'skipped' | 'failed' | null>(null)
  const sentinelRef = useRef<HTMLDivElement>(null)

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

  // Read by the debounced refresh effect so it re-reads with whatever
  // hideHentai is current when it actually runs, not whatever was current
  // when it was scheduled.
  const hideHentaiRef = useRef(hideHentai)
  hideHentaiRef.current = hideHentai
  const lastFetchedAtRef = useRef(lastFetchedAt)
  lastFetchedAtRef.current = lastFetchedAt

  // Filtered by the in-my-list and type controls, then sorted by the
  // server-computed sortOrder for the active sort — an integer compare on a
  // precomputed key, so no ordering rule (the my-score banding, the
  // alphabetical collation) is ever reimplemented here (design D3).
  const displayed = useMemo(() => {
    const filtered = items.filter((item) => {
      if (!inMyList && item.inMyList) return false
      if (typeFilter.length > 0 && !typeFilter.includes(item.mediaType ?? 'unknown')) return false
      return true
    })
    return [...filtered].sort((a, b) => (a.sortOrder?.[sort] ?? 0) - (b.sortOrder?.[sort] ?? 0))
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [items, inMyList, sort, typeParam])

  // A view control restored like every other (useRestorableState), not plain
  // useState: it resets to PAGE_SIZE on a fresh visit, and a sort or filter
  // change already is one — each goes through setSearchParams, which mints a
  // new history entry, so this reseeds under its new location key with no
  // code resetting it explicitly (task 2.8).
  const [visibleCount, setVisibleCount] = useRestorableState('visibleCount', PAGE_SIZE)
  const visibleItems = displayed.slice(0, visibleCount)

  const firstUnwatchedIndex = sort === 'myScore' ? displayed.findIndex((item) => item.myScore === null) : -1

  // The page's terminal states, in the season page's own order (tasks.md
  // 5.5): the grid takes priority whenever there's anything to show;
  // otherwise a cached-but-empty year reports why (no listing vs. filtered
  // out); otherwise the year has never been cached, so it's either still
  // loading or its first fetch has settled and produced nothing.
  const terminalState: 'grid' | 'filtersEmpty' | 'notListed' | 'loading' | 'loadFailed' =
    displayed.length > 0
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

  // Every media type present in the whole loaded listing — filtering is
  // client-side now, so selecting one type can no longer hide the others
  // from this picker; no accumulation workaround is needed.
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
        // it is. Since the whole listing is read at once, this re-read can
        // never leave the grid showing fewer anime than it was showing
        // (design D1).
        const shouldReread =
          result.outcome === 'fetched' ||
          result.outcome === 'notListed' ||
          (result.outcome === 'skipped' && lastFetchedAtRef.current === null)
        if (!shouldReread) return undefined

        return getYearPage(targetYear, { hideHentai: hideHentaiRef.current }).then((page) => {
          if (cancelled) return
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

  // Reveal more of the already-loaded, already-filtered-and-sorted listing
  // once the sentinel enters view — no network call. The reveal is
  // load-bearing rather than decorative: the whole year is already loaded,
  // and slicing to visibleCount is what keeps a 1,200-card year from
  // rendering (and painting every poster) in one go.
  useEffect(() => {
    const node = sentinelRef.current
    if (!node) return

    const observer = new IntersectionObserver((entries) => {
      if (entries[0]?.isIntersecting) {
        setVisibleCount((prev) => Math.min(prev + PAGE_SIZE, displayed.length))
      }
    })
    observer.observe(node)
    return () => observer.disconnect()
  }, [displayed, setVisibleCount])

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
          {visibleItems.map((item, index) => (
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
      {terminalState === 'loading' && <p className="year-page__loading">Loading…</p>}
    </div>
  )
}
