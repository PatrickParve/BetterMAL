import { Fragment, useEffect, useMemo, useRef, useState } from 'react'
import { Navigate, useSearchParams } from 'react-router-dom'
import { getSeasonPage, refreshSeason } from '../api/client.ts'
import type { AnimeBrowseItemDto } from '../api/types.ts'
import { RECAP_SEASONS, type RecapSeasonName } from '../api/types.ts'
import { AnimeCard, AnimeCardMeta } from '../components/AnimeCard.tsx'
import { FilterMultiSelect, type FilterMultiSelectOption } from '../components/FilterMultiSelect.tsx'
import { useContentFilter } from '../context/ContentFilterContext.tsx'
import { useDebouncedValue } from '../hooks/useDebouncedValue.ts'
import { usePageData } from '../hooks/usePageData.ts'
import { useRestorableState } from '../hooks/useRestorableState.ts'
import { useOnDemandProbe, useSeasonBounds } from '../hooks/useSeasonBounds.ts'
import { MEDIA_TYPE_ORDER, mediaTypeLabel, seasonPointIndex, shiftSeason } from '../utils/anime.ts'
import {
  EARLIEST_YEAR,
  currentSeasonTarget,
  isAddressableSeason,
  isSeasonName,
  probeTarget,
  yearsInRange,
  type SeasonTarget,
} from '../utils/browseRange.ts'
import './SeasonPage.css'

interface SeasonReadState {
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

// How long the season selection must sit still before a background refresh
// fires — so stepping quickly through seasons with the arrows only fetches
// the season actually landed on.
const REFRESH_DEBOUNCE_MS = 400

function seasonLabel(season: RecapSeasonName): string {
  return season.charAt(0).toUpperCase() + season.slice(1)
}

function isSortKey(value: string | null): value is SortKey {
  return value !== null && SORT_OPTIONS.some((option) => option.value === value)
}

// Carries every other search parameter over, setting only year/season — used
// by every replacement below so a rejected link that also names a sort or a
// type filter lands on the current season with those still applied (design
// D5).
function replacementUrl(searchParams: URLSearchParams, target: SeasonTarget): string {
  const params = new URLSearchParams(searchParams)
  params.set('year', String(target.year))
  params.set('season', target.season)
  return `/season?${params.toString()}`
}

// The route guard (design D2): decides, before anything mounts, whether the
// URL's year/season addresses a season a client may reach at all — winter
// 1917 through the navigable ceiling, and nothing further (design D3). A
// season between the archive's floor and the current season is admitted
// immediately, since the ceiling can never fall below it; a later season
// waits on GET /api/season/bounds (rendering nothing meanwhile) and is then
// admitted or replaced with the current season via <Navigate replace> — never
// pushed, so Back returns to wherever the reader came from rather than
// bouncing off the rejected URL again. The one exception is the horizon
// probe's own target: addressed directly and still past the ceiling, it earns
// a single on-demand MAL round trip (design D10a) instead of an outright
// refusal. Only once a target is settled does SeasonPageView — the page body,
// unchanged from before this split — ever mount, so no read, refresh or
// bounds-driven fetch can run for a season this guard rejects.
export function SeasonPage() {
  const [searchParams] = useSearchParams()
  const current = useMemo(currentSeasonTarget, [])
  const { ceiling, isPending } = useSeasonBounds()

  const yearParam = searchParams.get('year')
  const seasonParam = searchParams.get('season')
  const parsedYear = yearParam === null ? NaN : Number(yearParam)
  const hasValidParams = Number.isInteger(parsedYear) && isSeasonName(seasonParam)
  const requested: SeasonTarget = hasValidParams
    ? { year: parsedYear, season: seasonParam as RecapSeasonName }
    : current

  const currentIndex = seasonPointIndex(current.year, current.season)
  const requestedIndex = seasonPointIndex(requested.year, requested.season)
  const floorIndex = seasonPointIndex(EARLIEST_YEAR, 'winter')
  const target = probeTarget(current)
  const isProbeTarget = requestedIndex === seasonPointIndex(target.year, target.season)
  const needsOnDemandProbe =
    hasValidParams &&
    requestedIndex > currentIndex &&
    isProbeTarget &&
    !isPending &&
    requestedIndex > seasonPointIndex(ceiling.year, ceiling.season)

  // Hooks must run unconditionally on every render, so this is always called
  // — it only actually fires a MAL round trip while needsOnDemandProbe is
  // true (task 4.7).
  const onDemandCeiling = useOnDemandProbe(needsOnDemandProbe)

  if (!hasValidParams || requestedIndex < floorIndex) {
    return <Navigate to={replacementUrl(searchParams, current)} replace />
  }

  // Winter 1917 through the current season: the ceiling can never fall below
  // it, so this is admitted with no wait — the common case, and the navbar's
  // default landing.
  if (requestedIndex <= currentIndex) {
    return <SeasonPageView year={requested.year} season={requested.season} />
  }

  // A future target: wait for the ceiling before deciding anything about it.
  if (isPending) return null

  if (isAddressableSeason(requested, ceiling)) {
    return <SeasonPageView year={requested.year} season={requested.season} />
  }

  // The one season past the ceiling a URL may still ask about (design D10a).
  if (isProbeTarget) {
    if (onDemandCeiling === null) return null
    if (seasonPointIndex(onDemandCeiling.year, onDemandCeiling.season) >= requestedIndex) {
      return <SeasonPageView year={requested.year} season={requested.season} />
    }
  }

  return <Navigate to={replacementUrl(searchParams, current)} replace />
}

// Season page body: all anime airing in the selected season (not just my
// list), with a sort/filter control and a client-side reveal via an
// IntersectionObserver sentinel below the grid. `year`/`season` arrive
// already validated by the SeasonPage guard above (design D2); sort/type/
// inMyList still live in the URL directly, read here since they carry no
// addressability rule of their own.
//
// Reads and refreshes are two separate effects: reading from cache is
// instant and runs once per season/hideHentai combination (usePageData's own
// key, which loads the season's whole listing in one read — design D1),
// while the MAL refresh is debounced and keyed on season alone. Sort, the
// type filter, and the in-my-list filter never trigger a read at all — they
// act on the already-loaded listing in place, and the ordering itself is a
// per-item key the server computed rather than a rule reimplemented here
// (design D2, D3 of add-season-browser).
function SeasonPageView({ year, season }: { year: number; season: RecapSeasonName }) {
  const [searchParams, setSearchParams] = useSearchParams()
  const { ceiling } = useSeasonBounds()

  const sortParam = searchParams.get('sort')
  const inMyListParam = searchParams.get('inMyList')
  const typeParam = searchParams.get('type')

  const sort = isSortKey(sortParam) ? sortParam : 'popularity'
  const inMyList = inMyListParam !== '0'
  // URLSearchParams.get already distinguishes absent (null) from
  // present-and-empty (''), which is what makes a third state — None — free:
  // no parameter is All, `?type=` is None, `?type=tv,movie` is a selection.
  const typeFilter = typeParam === null ? null : typeParam.split(',').filter(Boolean)
  const { hideHentai } = useContentFilter()

  // Keyed on season/year/hideHentai: those are the only things a read
  // depends on now that sort and the two page filters act on the already-
  // loaded listing (design D1-D3). A different key is a different history
  // snapshot, so restoring one restores its own whole listing regardless of
  // which sort or filter was active when it was left.
  const seasonKey = `season:${year}/${season}:${hideHentai}`
  const { data: seasonData, setData: setSeasonData } = usePageData<SeasonReadState>(seasonKey, () =>
    getSeasonPage(year, season, { hideHentai }).then((page) => ({
      items: page.items,
      totalCount: page.totalCount,
      lastFetchedAt: page.lastFetchedAt,
      hasListing: page.hasListing,
    })),
  )
  const items = seasonData?.items ?? []
  const lastFetchedAt = seasonData?.lastFetchedAt ?? null
  const hasListing = seasonData?.hasListing ?? false

  const [refreshing, setRefreshing] = useState(false)
  // The refresh outcome for the season currently being viewed, reset the
  // moment the season changes (below) so one season's outcome can never
  // decide another season's terminal-state render (task 8.1). Null until a
  // refresh attempt for this season has settled.
  const [refreshOutcome, setRefreshOutcome] = useState<'fetched' | 'notListed' | 'skipped' | 'failed' | null>(null)
  const sentinelRef = useRef<HTMLDivElement>(null)

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
      if (typeFilter !== null && !typeFilter.includes(item.mediaType ?? 'unknown')) return false
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

  // The page's terminal states, in the order design.md decision 5 specifies
  // (task 8.3): the grid takes priority whenever there's anything to show;
  // otherwise a cached-but-empty season reports why (no listing vs. filtered
  // out); otherwise the season has never been cached, so it's either still
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

  // Built from the addressable range's own two ends alone (design D1) — the
  // guard above has already confirmed `year`/`season` sit inside it, so no
  // widening against the viewed year is needed here anymore.
  const yearOptions = useMemo(() => yearsInRange(EARLIEST_YEAR, ceiling.year), [ceiling.year])

  // Within the ceiling's own year, cut the season list to the ceiling's
  // season — but always keep the currently-selected season so a
  // URL-addressed season past the horizon still has a valid <select> value
  // (task 7.3 of an earlier change).
  const seasonOptions = useMemo(() => {
    if (year !== ceiling.year) return RECAP_SEASONS
    const ceilingIndex = RECAP_SEASONS.indexOf(ceiling.season)
    return RECAP_SEASONS.filter((option) => RECAP_SEASONS.indexOf(option) <= ceilingIndex || option === season)
  }, [year, season, ceiling.year, ceiling.season])

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

  function setTarget(next: { year: number; season: RecapSeasonName }) {
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

  function setTypeFilter(next: string[] | null) {
    setSearchParams((prev) => {
      const params = new URLSearchParams(prev)
      if (next === null) params.delete('type')
      else params.set('type', next.join(','))
      return params
    })
  }

  // The outcome from a stale season must never decide this season's render
  // (task 8.1) — reset the instant the season changes, ahead of the debounced
  // refresh effect below settling for whichever season is landed on.
  useEffect(() => {
    setRefreshOutcome(null)
  }, [seasonKey])

  // Visit-triggered background refresh: keyed on season alone (via the
  // debounced key below) so sort/filter changes never cause a MAL fetch.
  // Debounced so arrow-stepping through seasons only refreshes the one
  // settled on; the very first render's value is applied immediately since
  // useDebouncedValue seeds its state with the initial value.
  const debouncedSeasonKey = useDebouncedValue(`${year}/${season}`, REFRESH_DEBOUNCE_MS)

  useEffect(() => {
    const [yearPart, seasonPart] = debouncedSeasonKey.split('/')
    const targetYear = Number(yearPart)
    const targetSeason = seasonPart as RecapSeasonName

    let cancelled = false
    setRefreshing(true)

    refreshSeason(targetYear, targetSeason)
      .then((result) => {
        if (cancelled) return undefined
        setRefreshOutcome(result.outcome)

        // Re-read on fetched/notListed (new data, or the fact settled).
        // On skipped (already fetched today by someone else) only when this
        // tab has nothing cached itself — the cross-tab race where another
        // tab's fetch landed between this tab's own cache read and its
        // refresh call. Never on failed: the cached page, if any, is left
        // exactly as it is (design.md decision 5). Since the whole listing
        // is read at once, this re-read can never leave the grid showing
        // fewer anime than it was showing (design D1).
        const shouldReread =
          result.outcome === 'fetched' ||
          result.outcome === 'notListed' ||
          (result.outcome === 'skipped' && lastFetchedAtRef.current === null)
        if (!shouldReread) return undefined

        return getSeasonPage(targetYear, targetSeason, { hideHentai: hideHentaiRef.current }).then((page) => {
          if (cancelled) return
          setSeasonData({
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
  }, [debouncedSeasonKey])

  // Reveal more of the already-loaded, already-filtered-and-sorted listing
  // once the sentinel enters view — no network call. The reveal is
  // load-bearing rather than decorative: the whole season is already loaded,
  // and slicing to visibleCount is what keeps a several-hundred-card season
  // from rendering (and painting every poster) in one go.
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
    <div className="season-page">
      <div className="season-page__header">
        <h1 className="season-page__title">Seasonal anime</h1>

        <div className="season-page__center">
          <div className="season-page__nav">
            <button
              type="button"
              onClick={() => setTarget(shiftSeason(year, season, -1))}
              aria-label="Previous season"
              disabled={seasonPointIndex(year, season) <= seasonPointIndex(EARLIEST_YEAR, 'winter')}
            >
              &lsaquo;
            </button>
            <span className="season-page__label-wrap">
              <span className="season-page__label">
                {seasonLabel(season)} {year}
              </span>
              {refreshing && <span className="season-page__updating">Updating…</span>}
            </span>
            <button
              type="button"
              onClick={() => setTarget(shiftSeason(year, season, 1))}
              aria-label="Next season"
              disabled={seasonPointIndex(year, season) >= seasonPointIndex(ceiling.year, ceiling.season)}
            >
              &rsaquo;
            </button>
          </div>
          <div className="season-page__jump">
            <select
              className="season-page__sort"
              value={season}
              onChange={(event) => setTarget({ year, season: event.target.value as RecapSeasonName })}
              aria-label="Jump to season"
            >
              {seasonOptions.map((option) => (
                <option key={option} value={option}>
                  {seasonLabel(option)}
                </option>
              ))}
            </select>
            <select
              className="season-page__sort"
              value={year}
              onChange={(event) => {
                const nextYear = Number(event.target.value)
                // Selecting the ceiling year while a later season is
                // selected clamps the season back to the ceiling's, since
                // this dropdown only changes the year half of the target
                // (design.md decision 4 / task 7.4 of an earlier change).
                const nextSeason =
                  nextYear === ceiling.year && RECAP_SEASONS.indexOf(season) > RECAP_SEASONS.indexOf(ceiling.season)
                    ? ceiling.season
                    : season
                setTarget({ year: nextYear, season: nextSeason })
              }}
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

      {terminalState === 'grid' && (
        <div className="season-page__grid">
          {visibleItems.map((item, index) => (
            <Fragment key={item.animeId}>
              {index === firstUnwatchedIndex && index > 0 && <div className="season-page__divider">Unwatched</div>}
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
      {terminalState === 'filtersEmpty' && <p className="season-page__empty">No anime match the current filters.</p>}
      {terminalState === 'notListed' && (
        <p className="season-page__empty">MyAnimeList hasn't listed this season yet.</p>
      )}
      {terminalState === 'loadFailed' && (
        <p className="season-page__empty">This season couldn't be loaded — it'll be retried next time you open it.</p>
      )}

      <div ref={sentinelRef} className="season-page__sentinel" />
      {terminalState === 'loading' && <p className="season-page__loading">Loading…</p>}
    </div>
  )
}
