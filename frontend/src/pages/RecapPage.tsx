import { useEffect, useMemo, useRef, useState, type CSSProperties } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { getRecap } from '../api/client.ts'
import {
  RECAP_SEASONS,
  type RecapDto,
  type RecapHotTakeDto,
  type RecapMode,
  type RecapRowDto,
  type RecapSeasonName,
  type RecapStatsDto,
  type RecapTimeFilter,
  type RecapTimeRankingDto,
  type ScoreDistributionBucketDto,
} from '../api/types.ts'
import { RankingOverlay, type RankingOverlayRow } from '../components/RankingOverlay.tsx'
import {
  describeSeasonRanking,
  describeYearRanking,
  offeredScores,
  rankByScoreCount,
  RankingSection,
} from '../components/RankingSection.tsx'
import { ScoreBoardOverlay, type ScoreBoardGroup } from '../components/ScoreBoardOverlay.tsx'
import { ScoreChip } from '../components/ScoreChip.tsx'
import { ScoreDistribution } from '../components/ScoreDistribution.tsx'
import { ScoreValue } from '../components/ScoreValue.tsx'
import { usePageData } from '../hooks/usePageData.ts'
import { useRestorableState } from '../hooks/useRestorableState.ts'
import {
  formatRuntime,
  MEDIA_TYPE_ORDER,
  mediaTypeLabel,
  pickDisplayTitle,
  seasonLabel,
  seasonPointIndex,
  shiftSeason,
} from '../utils/anime.ts'
import './RecapPage.css'

type RankingBasis = 'mine' | 'mal'

const MODE_OPTIONS: { value: RecapMode; label: string; family?: 'year' | 'season' }[] = [
  { value: 'multiYear', label: 'Multi-year' },
  { value: 'yearly', label: 'Yearly', family: 'year' },
  { value: 'season', label: 'Season', family: 'season' },
]

// A generous floor for the year selects — recaps look at the past, so
// there's no MAL-catalog-style ceiling to mirror like SeasonPage's.
const EARLIEST_YEAR = 1960
const TOP_TEN_SIZE = 10

function describeTimeRanking(row: RecapTimeRankingDto): RankingOverlayRow {
  const label = row.season ? `${seasonLabel(row.season)} ${row.year}` : String(row.year)
  const episodeWord = row.episodesWatched === 1 ? 'episode' : 'episodes'
  return {
    key: row.season ? `${row.year}-${row.season}` : String(row.year),
    label,
    meta: `${formatRuntime(row.timeSpentSeconds)} · ${row.episodesWatched} ${episodeWord}`,
    to: row.season
      ? `/recap?mode=season&year=${row.year}&season=${row.season}`
      : `/recap?mode=yearly&year=${row.year}&filter=aired`,
    posters: row.topPosters,
  }
}

// Ten ascending slots for scores 1-10, matching
// ProfileService.BuildScoreDistribution's shape (design.md decision 3) —
// computed over every included entry of the period, not the top 10's
// media-type-narrowed set (design.md decision 4). Carries the entries
// themselves, not only a count, so the rating distribution's buckets and the
// score board's slots (add-recap-score-board-and-hold-scroll's design.md
// decision 1) are both derived from this one selection rule and can never
// disagree about how many anime carry a score.
// anime-ranking: within a slot, anime are ordered by rank — best-ranked
// first, unranked last (ordered by title among other unranked members) —
// per list-recaps spec "The score board lays a period out by score".
function compareByRankThenTitle(a: RecapRowDto, b: RecapRowDto): number {
  if (a.myRank != null && b.myRank != null) return a.myRank - b.myRank
  if (a.myRank != null) return -1
  if (b.myRank != null) return 1
  return a.title.localeCompare(b.title)
}

function scoreGroupsOf(items: RecapRowDto[]): ScoreBoardGroup[] {
  const byScore = new Map<number, RecapRowDto[]>()
  for (const item of items) {
    if (item.myScore == null) continue
    const list = byScore.get(item.myScore)
    if (list) list.push(item)
    else byScore.set(item.myScore, [item])
  }
  return Array.from({ length: 10 }, (_, i) => i + 1).map((score) => ({
    score,
    items: (byScore.get(score) ?? []).sort(compareByRankThenTitle),
  }))
}

function isRecapMode(value: string | null): value is RecapMode {
  return value === 'multiYear' || value === 'yearly' || value === 'season'
}

function isSeasonName(value: string | null): value is RecapSeasonName {
  return value !== null && (RECAP_SEASONS as readonly string[]).includes(value)
}

function currentSeasonName(): RecapSeasonName {
  return RECAP_SEASONS[Math.floor(new Date().getMonth() / 3)]
}

// The recap's two score-ranking filter selections (design D5): URL
// parameters, like every other recap control, so a filtered ranking is
// linkable and survives a reload. Anything outside 1-10 — missing,
// non-numeric, out of range — reads as null (All) rather than erroring.
function parseScoreParam(value: string | null): number | null {
  if (value === null) return null
  const n = Number(value)
  return Number.isInteger(n) && n >= 1 && n <= 10 ? n : null
}

// Bounds always widen to include whatever's actually selected (mirrors
// SeasonPage's yearOptions) — a URL-addressed year outside the normal
// 1960-current window (or an as-yet-unreached future one) still has a
// valid <select> value instead of silently snapping to whichever option
// happens to be first in the list.
function yearOptions(low: number, high: number): number[] {
  return Array.from({ length: high - low + 1 }, (_, i) => high - i)
}

// The my-list handoff (design.md decision 9): recap params carried under a
// `recap`-prefixed vocabulary so they can never collide with MyListPage's
// own filter/sort search params. MyListPage reads these same literal names
// back out (tasks.md 7.3). `focus` (design.md decision 1) is a single opaque
// token that seeds my list's own controls to the narrowing a stat tile or
// distribution row describes, on top of the plain scope: `completed`,
// `dropped`, `watching`, `movies`, or `score-1`…`score-10`. Omitted, the
// scope alone is the unfocused "in this period" set — the existing "See all
// N in my list" link never passes one, so it stays byte-identical.
function myListScopeSearch(
  mode: RecapMode,
  startYear: number,
  endYear: number,
  season: RecapSeasonName,
  filter: RecapTimeFilter,
  typeFilter: string,
  focus?: string,
): string {
  const params = new URLSearchParams()
  params.set('recapMode', mode)
  if (mode === 'multiYear') {
    params.set('recapFrom', String(startYear))
    params.set('recapTo', String(endYear))
  } else {
    params.set('recapYear', String(startYear))
  }
  if (mode === 'season') params.set('recapSeason', season)
  else params.set('recapFilter', filter)
  if (typeFilter !== 'all') params.set('recapType', typeFilter)
  if (focus) params.set('focus', focus)
  return params.toString()
}

// Recap page: three period modes switched in place via URL search params
// (design.md decision 8), matching SeasonPage/TopAnimePage's
// parameterised-in-the-URL convention. The server returns the whole included
// set; ranking basis and media-type narrowing are applied locally (design.md
// decision 1).
export function RecapPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const currentYear = useMemo(() => new Date().getFullYear(), [])
  // Only one ranking overlay can be open at a time, so a single slot (title +
  // rows already described) serves the season, year, and both time-watched
  // rankings alike.
  const [overlay, setOverlay] = useState<{
    title: string
    rows: RankingOverlayRow[]
    family?: 'year' | 'season'
  } | null>(null)
  // The score board (design.md decision 6/D7): opened from the
  // distribution's section header, matching `overlay` above — still no URL
  // parameter and no history entry, so back still leaves the recap exactly
  // as it does with the board closed. What's new is that it's recorded in
  // the history entry's snapshot, so a restore rebuilds the page with the
  // board open again, as a new overlay over it, rather than the board
  // surviving the navigation.
  const [boardOpen, setBoardOpen] = useRestorableState<boolean>('scoreBoard', false)

  const modeParam = searchParams.get('mode')
  const mode: RecapMode = isRecapMode(modeParam) ? modeParam : 'yearly'

  const fromParam = Number(searchParams.get('from'))
  const toParam = Number(searchParams.get('to'))
  const yearParam = Number(searchParams.get('year'))
  const seasonParam = searchParams.get('season')
  const filterParam = searchParams.get('filter')
  const basisParam = searchParams.get('basis')
  const typeParam = searchParams.get('type')
  const seasonScoreParam = searchParams.get('seasonScore')
  const yearScoreParam = searchParams.get('yearScore')

  const startYear =
    mode === 'multiYear'
      ? Number.isInteger(fromParam) && fromParam > 0
        ? fromParam
        : currentYear - 1
      : Number.isInteger(yearParam) && yearParam > 0
        ? yearParam
        : currentYear
  const endYear = mode === 'multiYear' ? (Number.isInteger(toParam) && toParam > 0 ? toParam : currentYear) : startYear
  const season: RecapSeasonName = isSeasonName(seasonParam) ? seasonParam : currentSeasonName()
  const filter: RecapTimeFilter = filterParam === 'aired' ? 'aired' : 'watched'
  const basis: RankingBasis = basisParam === 'mal' ? 'mal' : 'mine'
  const typeFilter = typeParam ?? 'all'

  const recapKey =
    mode === 'multiYear'
      ? `recap:multiYear:${startYear}-${endYear}:${filter}`
      : mode === 'yearly'
        ? `recap:yearly:${startYear}:${filter}`
        : `recap:season:${startYear}:${season}`

  const { data: recap, loading } = usePageData<RecapDto>(recapKey, () =>
    getRecap({ mode, startYear, endYear, season, filter }),
  )

  // recapKey embeds the period (and filter), so stepping it mints a new
  // resource key and usePageData clears `recap` to null until that key's
  // fetch resolves (same mechanism ProfilePage's topAnime/rewatched strips
  // hit). The whole body below is gated on recap being non-null, so without
  // holding the last-loaded period on screen, every period *step* collapsed
  // the page to just its header for a moment — and a collapsed page has
  // nowhere for the held scroll position to be, so the browser clamped it
  // back toward the top regardless of keepScroll.
  //
  // `recap` can also lag one render behind the URL: React's "adjust state
  // during render" pattern inside usePageData re-invokes this component
  // synchronously when recapKey has changed but its own state hasn't caught
  // up yet, and during that throwaway pass `recap` is still the *previous*
  // key's object even though `mode`/`startYear`/`filter` here already read
  // the new URL. That pass's JSX is discarded, but a plain ref mutation in
  // it is not — so recapMatchesSelection (mirroring the fallback effect's
  // own staleness check below) gates every use of `recap` for display,
  // including the ref write, on it actually describing the current
  // mode/period/filter, not just being non-null.
  const recapMatchesSelection =
    recap !== null &&
    recap.mode === mode &&
    recap.startYear === startYear &&
    recap.endYear === endYear &&
    (mode === 'season' ? recap.season === season : recap.filter === filter)

  // A fetch that lands with the *selected* filter's count at zero and the
  // *other* filter's above zero is a real but transient result: the effect
  // below is about to correct it with a second navigation. Held back from
  // becoming the displayed value too, the same way a null (still-loading)
  // result is — otherwise the page would genuinely collapse to "Nothing to
  // recap" for the one tick between this response landing and the
  // correction's own response landing, and that collapse is exactly the kind
  // of height change that clamps the scroll position it's holding.
  const pendingFilterFallback =
    recapMatchesSelection &&
    mode !== 'season' &&
    ((filter === 'watched' && recap.watchedCount === 0 && recap.airedCount > 0) ||
      (filter === 'aired' && recap.airedCount === 0 && recap.watchedCount > 0))

  const recapDisplayRef = useRef<RecapDto | null>(null)
  if (recapMatchesSelection && !pendingFilterFallback) recapDisplayRef.current = recap
  const displayedRecap = recapMatchesSelection && !pendingFilterFallback ? recap : recapDisplayRef.current

  const scoreGroups = useMemo(() => (displayedRecap ? scoreGroupsOf(displayedRecap.items) : []), [displayedRecap])
  const scoreBuckets: ScoreDistributionBucketDto[] = scoreGroups.map((g) => ({ score: g.score, count: g.items.length }))

  // The score filter's offered buttons and effective selections (design D5):
  // a selection naming a score the displayed ranking does not offer reads as
  // All without being cleared from the URL, so stepping to another period
  // that offers it again restores the selection rather than silently
  // dropping it.
  const offeredSeasonScores = displayedRecap ? offeredScores(displayedRecap.seasonRanking) : []
  const offeredYearScores = displayedRecap ? offeredScores(displayedRecap.yearRanking) : []
  const parsedSeasonScore = parseScoreParam(seasonScoreParam)
  const seasonScore = parsedSeasonScore !== null && offeredSeasonScores.includes(parsedSeasonScore) ? parsedSeasonScore : null
  const parsedYearScore = parseScoreParam(yearScoreParam)
  const yearScore = parsedYearScore !== null && offeredYearScores.includes(parsedYearScore) ? parsedYearScore : null

  // `options.keepScroll` (design.md decision 5/7) is opt-in per call site:
  // the ranking-basis toggle, the media-type select, every period control
  // (year/season selects and stepper arrows in all three modes), and the
  // dynamic filter's own empty-selection fallback pass it. The recap-type
  // tabs (switchMode) and the manual time-filter buttons don't — they change
  // the shape of the page rather than its period, so they keep today's
  // scroll-to-top.
  function updateParams(updates: Record<string, string | null>, options?: { keepScroll?: boolean }) {
    setSearchParams(
      (prev) => {
        const params = new URLSearchParams(prev)
        for (const [key, value] of Object.entries(updates)) {
          if (value === null) params.delete(key)
          else params.set(key, value)
        }
        return params
      },
      options?.keepScroll ? { state: { keepScroll: true } } : undefined,
    )
  }

  // Falls back off a selected filter that turned out empty for this period,
  // rather than showing an impossible-selection empty state (spec
  // scenario "Falling back when the selection becomes unavailable"). Only
  // acts once the response actually matches the current selection, so a
  // rapid sequence of changes can't flip the filter based on a stale reply.
  // Passes keepScroll (design.md decision 5): this fires only as a
  // correction to a period or mode change the user just made, never in
  // response to touching the filter itself (its buttons are disabled when
  // their count is zero, so a manual choice can never be the empty one).
  useEffect(() => {
    if (!recapMatchesSelection || mode === 'season') return
    const currentCount = filter === 'watched' ? recap!.watchedCount : recap!.airedCount
    const otherCount = filter === 'watched' ? recap!.airedCount : recap!.watchedCount
    if (currentCount === 0 && otherCount > 0)
      updateParams({ filter: filter === 'watched' ? 'aired' : 'watched' }, { keepScroll: true })
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [recap, recapMatchesSelection, mode, filter])

  function switchMode(next: RecapMode) {
    if (next === mode) return
    if (next === 'multiYear') updateParams({ mode: next, from: String(startYear), to: String(startYear), year: null })
    else if (next === 'yearly') updateParams({ mode: next, year: String(startYear), from: null, to: null })
    else updateParams({ mode: next, year: String(startYear), season, from: null, to: null })
  }

  const periodLabel =
    mode === 'multiYear'
      ? startYear === endYear
        ? String(startYear)
        : `${startYear}–${endYear}`
      : mode === 'yearly'
        ? String(startYear)
        : `${seasonLabel(season)} ${startYear}`

  function renderModeTabs() {
    return (
      <div className="recap-page__mode-tabs" role="tablist" aria-label="Recap type">
        {MODE_OPTIONS.map((option) => {
          const familyClass = option.family ? ` family--${option.family}` : ''
          return (
            <button
              key={option.value}
              type="button"
              role="tab"
              aria-selected={mode === option.value}
              className={
                (mode === option.value ? 'recap-page__tab recap-page__tab--active' : 'recap-page__tab') + familyClass
              }
              onClick={() => switchMode(option.value)}
            >
              {option.label}
            </button>
          )
        })}
      </div>
    )
  }

  // Period stepper arrows (design.md decision 7): bounded by the recap's own
  // widened year range — the same [min(EARLIEST_YEAR, selected),
  // max(currentYear, selected)] range yearOptions() computes — so an arrow
  // disables exactly when the select it sits beside has nothing further to
  // offer. Season and yearly modes only; a multi-year range has no single
  // step.
  function renderPeriodControls() {
    const years = yearOptions(Math.min(EARLIEST_YEAR, startYear, endYear), Math.max(currentYear, startYear, endYear))
    const lowYear = Math.min(EARLIEST_YEAR, startYear)
    const highYear = Math.max(currentYear, startYear)

    return (
      <div className="recap-page__period-controls">
        {mode === 'multiYear' && (
          <>
            <select
              value={startYear}
              onChange={(e) => updateParams({ from: e.target.value }, { keepScroll: true })}
              aria-label="From year"
            >
              {years.map((y) => (
                <option key={y} value={y}>
                  {y}
                </option>
              ))}
            </select>
            <span className="recap-page__period-sep">&ndash;</span>
            <select
              value={endYear}
              onChange={(e) => updateParams({ to: e.target.value }, { keepScroll: true })}
              aria-label="To year"
            >
              {years.map((y) => (
                <option key={y} value={y}>
                  {y}
                </option>
              ))}
            </select>
          </>
        )}
        {mode === 'yearly' && (
          <div className="recap-page__period-nav">
            <button
              type="button"
              onClick={() => updateParams({ year: String(startYear - 1) }, { keepScroll: true })}
              aria-label="Previous year"
              disabled={startYear <= lowYear}
            >
              &lsaquo;
            </button>
            <select
              value={startYear}
              onChange={(e) => updateParams({ year: e.target.value }, { keepScroll: true })}
              aria-label="Year"
            >
              {years.map((y) => (
                <option key={y} value={y}>
                  {y}
                </option>
              ))}
            </select>
            <button
              type="button"
              onClick={() => updateParams({ year: String(startYear + 1) }, { keepScroll: true })}
              aria-label="Next year"
              disabled={startYear >= highYear}
            >
              &rsaquo;
            </button>
          </div>
        )}
        {mode === 'season' && (
          <div className="recap-page__period-nav">
            <button
              type="button"
              onClick={() => {
                const next = shiftSeason(startYear, season, -1)
                updateParams({ year: String(next.year), season: next.season }, { keepScroll: true })
              }}
              aria-label="Previous season"
              disabled={seasonPointIndex(startYear, season) <= seasonPointIndex(lowYear, 'winter')}
            >
              &lsaquo;
            </button>
            <select
              value={season}
              onChange={(e) => updateParams({ season: e.target.value }, { keepScroll: true })}
              aria-label="Season"
            >
              {RECAP_SEASONS.map((s) => (
                <option key={s} value={s}>
                  {seasonLabel(s)}
                </option>
              ))}
            </select>
            <select
              value={startYear}
              onChange={(e) => updateParams({ year: e.target.value }, { keepScroll: true })}
              aria-label="Year"
            >
              {years.map((y) => (
                <option key={y} value={y}>
                  {y}
                </option>
              ))}
            </select>
            <button
              type="button"
              onClick={() => {
                const next = shiftSeason(startYear, season, 1)
                updateParams({ year: String(next.year), season: next.season }, { keepScroll: true })
              }}
              aria-label="Next season"
              disabled={seasonPointIndex(startYear, season) >= seasonPointIndex(highYear, 'fall')}
            >
              &rsaquo;
            </button>
          </div>
        )}
      </div>
    )
  }

  function renderSeasonPageLink() {
    if (mode !== 'season') return null
    return (
      <Link to={`/season?year=${startYear}&season=${season}`} className="recap-page__season-button family--season">
        <CalendarIcon />
        Browse the season
        <span aria-hidden="true">&rsaquo;</span>
      </Link>
    )
  }

  // The year-level counterpart of renderSeasonPageLink above (design D6) —
  // same markup, same height, same hover/focus treatment, just the year
  // colour family and a /year target. Mutually exclusive with it by mode, so
  // a multi-year recap offers neither and the control row keeps its shape.
  function renderYearPageLink() {
    if (mode !== 'yearly') return null
    return (
      <Link to={`/year?year=${startYear}`} className="recap-page__season-button family--year">
        <CalendarIcon />
        Browse the year
        <span aria-hidden="true">&rsaquo;</span>
      </Link>
    )
  }

  function renderFilterToggle() {
    if (mode === 'season') return null
    const watchedDisabled = displayedRecap ? displayedRecap.watchedCount === 0 : false
    const airedDisabled = displayedRecap ? displayedRecap.airedCount === 0 : false
    return (
      <div className="recap-page__filter-toggle" role="group" aria-label="Time filter">
        <button
          type="button"
          className={filter === 'watched' ? 'recap-page__tab recap-page__tab--active' : 'recap-page__tab'}
          aria-pressed={filter === 'watched'}
          disabled={watchedDisabled}
          title={watchedDisabled ? 'Nothing completed or dropped in this period' : undefined}
          onClick={() => updateParams({ filter: 'watched' })}
        >
          What I watched
        </button>
        <button
          type="button"
          className={filter === 'aired' ? 'recap-page__tab recap-page__tab--active' : 'recap-page__tab'}
          aria-pressed={filter === 'aired'}
          disabled={airedDisabled}
          title={airedDisabled ? 'Nothing aired in this period' : undefined}
          onClick={() => updateParams({ filter: 'aired' })}
        >
          What aired
        </button>
      </div>
    )
  }

  // Tile order/labels per design.md decision 5: the period total leads,
  // named for what it counts rather than read as "Anime"; Completed and
  // Dropped sit together so the gap between the two is explained. Only the
  // tiles that describe a set of anime (rather than an aggregate) carry a
  // `to` — those render as links (spec "Drilling into a recap stat"), the
  // rest stay inert `<div>`s with the same box so the grid never reflows.
  // Currently watching is counted on air date under both time filters
  // (design.md decision 4), so its link forces the aired-attributed scope
  // even when the recap itself is showing "What I watched".
  //
  // A followable tile whose own count is 0 has nowhere to lead — following
  // it would only land on an empty list — so it renders as a plain, unlinked
  // `<div>` and takes the same aggregate treatment an aggregate tile takes,
  // rather than keeping the accent rail of a tile with somewhere to go
  // (design.md decision 6): the accent marker always means "this leads
  // somewhere".
  function renderStats(stats: RecapStatsDto) {
    const scopeLink = (focus?: string, filterOverride?: RecapTimeFilter) =>
      `/my-list?${myListScopeSearch(mode, startYear, endYear, season, filterOverride ?? filter, typeFilter, focus)}`

    const tiles: { label: string; value: string; to?: string; count?: number }[] = [
      { label: 'In this period', value: String(stats.animeCounted), to: scopeLink(), count: stats.animeCounted },
      { label: 'Mean score', value: stats.meanScore !== null ? stats.meanScore.toFixed(2) : '—' },
      { label: 'Completed', value: String(stats.completed), to: scopeLink('completed'), count: stats.completed },
      { label: 'Dropped', value: String(stats.dropped), to: scopeLink('dropped'), count: stats.dropped },
      { label: 'Episodes watched', value: String(stats.episodesWatched) },
      { label: 'Movies watched', value: String(stats.moviesWatched), to: scopeLink('movies'), count: stats.moviesWatched },
      { label: 'Time spent', value: formatRuntime(stats.timeSpentSeconds) },
      {
        label: 'Currently watching',
        value: String(stats.currentlyWatching),
        to: scopeLink('watching', 'aired'),
        count: stats.currentlyWatching,
      },
    ]
    return (
      <section className="recap-page__section">
        <h2>Stats</h2>
        <div className="recap-page__stats">
          {tiles.map((tile) =>
            tile.to && (tile.count ?? 0) > 0 ? (
              <Link key={tile.label} to={tile.to} className="recap-page__stat recap-page__stat--link">
                <span className="recap-page__stat-value">{tile.value}</span>
                <span className="recap-page__stat-label">{tile.label}</span>
              </Link>
            ) : (
              <div key={tile.label} className="recap-page__stat">
                <span className="recap-page__stat-value">{tile.value}</span>
                <span className="recap-page__stat-label">{tile.label}</span>
              </div>
            ),
          )}
        </div>
      </section>
    )
  }

  // No meanScore prop (design.md decision 4): the stat block directly above
  // already reports the period's mean, so the recap's block carries no mean
  // line of its own.
  function renderDistribution(buckets: ScoreDistributionBucketDto[], groups: ScoreBoardGroup[]) {
    const totalScored = groups.reduce((sum, g) => sum + g.items.length, 0)
    return (
      <section className="recap-page__section">
        <div className="recap-page__section-header">
          <h2>Rating distribution</h2>
          <button
            type="button"
            className="recap-page__board-button"
            disabled={totalScored === 0}
            title={totalScored === 0 ? 'Nothing scored in this period' : undefined}
            onClick={() => setBoardOpen(true)}
          >
            Score board
          </button>
        </div>
        <ScoreDistribution
          buckets={buckets}
          compact
          tiered
          hrefForScore={(score) =>
            `/my-list?${myListScopeSearch(mode, startYear, endYear, season, filter, typeFilter, `score-${score}`)}`
          }
        />
      </section>
    )
  }

  function renderHotTake(take: RecapHotTakeDto) {
    const malLikedMore = take.divergence > 0
    return (
      <li key={take.animeId} className={malLikedMore ? 'recap-hot-take family--mal' : 'recap-hot-take family--mine'}>
        <Link to={`/anime/${take.animeId}`} className="recap-hot-take__link">
          {take.pictureUrl ? (
            <img src={take.pictureUrl} alt="" className="recap-hot-take__picture" />
          ) : (
            <div className="recap-hot-take__picture recap-hot-take__picture--placeholder" aria-hidden="true" />
          )}
          <span className="recap-hot-take__title" title={pickDisplayTitle(take.title, take.englishTitle)}>
            {pickDisplayTitle(take.title, take.englishTitle)}
          </span>
        </Link>
        <span className="recap-hot-take__scores">
          <span className="score--mine">{take.myScore}</span>
          <span className="score--mal">
            <ScoreValue value={take.malScore} completed={take.malRevealed} />
          </span>
        </span>
        <span
          className={
            malLikedMore
              ? 'recap-hot-take__direction recap-hot-take__direction--mal'
              : 'recap-hot-take__direction recap-hot-take__direction--mine'
          }
        >
          {malLikedMore ? 'MAL liked it more' : 'I liked it more'}
        </span>
      </li>
    )
  }

  function renderHotTakes(hotTakes: RecapHotTakeDto[]) {
    return (
      <section className="recap-page__section family--hot">
        <h2 className="section-band">Biggest Hot takes</h2>
        {hotTakes.length === 0 ? (
          <p className="recap-page__empty-note">No hot takes for this period.</p>
        ) : (
          <ul className="recap-hot-take-list">{hotTakes.map(renderHotTake)}</ul>
        )}
      </section>
    )
  }

  const MEDALS = ['gold', 'silver', 'bronze', 'plain', 'plain'] as const

  // A card for one of the top 5, rendered in place of the equivalent row
  // (design.md decision 1). Ranks 4-5 take the neutral `--plain` variant —
  // visibly subordinate to the three medals rather than a fourth/fifth
  // colour of their own. The key folds in every input that can change which
  // ten anime — and in which order — are shown, so React remounts the cards
  // (re-running the entrance animation, tasks.md 4.1/4.5) exactly when the
  // set genuinely changes, and reuses them across an unrelated re-render.
  function renderPodiumCard(item: RecapRowDto, index: number, effectiveBasis: RankingBasis) {
    const rank = index + 1
    const medal = MEDALS[index]
    const displayTitle = pickDisplayTitle(item.title, item.englishTitle)
    const key = `${mode}:${startYear}:${endYear}:${season}:${filter}:${effectiveBasis}:${typeFilter}:${item.animeId}`
    return (
      <li key={key} className={`recap-podium__card recap-podium__card--${medal}`}>
        <Link to={`/anime/${item.animeId}`} className="recap-podium__link">
          <span className="recap-podium__badge">{rank}</span>
          <div className="recap-podium__picture-frame">
            {item.pictureUrl ? (
              <img src={item.pictureUrl} alt="" className="recap-podium__picture" />
            ) : (
              <div className="recap-podium__picture recap-podium__picture--placeholder" aria-hidden="true" />
            )}
          </div>
          <span className="recap-podium__title" title={displayTitle}>
            {displayTitle}
          </span>
          {effectiveBasis === 'mine' ? (
            <ScoreChip role="mine" size="compact">
              {item.myScore ?? '—'}
            </ScoreChip>
          ) : (
            <ScoreChip role="mal" size="compact">
              <ScoreValue value={item.malScore} completed={item.malRevealed} />
            </ScoreChip>
          )}
        </Link>
      </li>
    )
  }

  function renderTopTen(recap: RecapDto) {
    const basisControlVisible = mode === 'season' || filter === 'aired'
    const effectiveBasis: RankingBasis = basisControlVisible ? basis : 'mine'

    const presentTypes = new Set(recap.items.map((item) => item.mediaType ?? 'unknown'))
    const typeOptions = MEDIA_TYPE_ORDER.filter((value) => presentTypes.has(value))
    const hasUnknownType = presentTypes.has('unknown')

    const narrowed =
      typeFilter === 'all' ? recap.items : recap.items.filter((item) => (item.mediaType ?? 'unknown') === typeFilter)

    const scoreOf = (item: RecapRowDto) => (effectiveBasis === 'mine' ? item.myScore : item.malScore)
    const ranked = [...narrowed].sort((a, b) => {
      const scoreA = scoreOf(a)
      const scoreB = scoreOf(b)
      if (scoreA == null && scoreB == null) return a.title.localeCompare(b.title)
      if (scoreA == null) return 1
      if (scoreB == null) return -1
      if (scoreB !== scoreA) return scoreB - scoreA
      // anime-ranking: on the my-score basis, a tie at the same score is
      // broken by rank rather than title; the MAL basis has no ranking
      // opinion to draw on, so it keeps its title tiebreak (list-recaps
      // spec, "Top 10 of the period").
      return effectiveBasis === 'mine' ? compareByRankThenTitle(a, b) : a.title.localeCompare(b.title)
    })
    const topTen = ranked.slice(0, TOP_TEN_SIZE)
    const podium = topTen.slice(0, 5)
    const rows = topTen.slice(5)

    return (
      <section className="recap-page__section">
        <div className="recap-page__section-header">
          <h2>Top {Math.min(TOP_TEN_SIZE, narrowed.length)}</h2>
          <div className="recap-page__top-ten-controls">
            {basisControlVisible && (
              <div className="recap-page__basis-toggle" role="group" aria-label="Rank top 10 by">
                <button
                  type="button"
                  className={
                    effectiveBasis === 'mine'
                      ? 'recap-page__tab recap-page__tab--active family--mine'
                      : 'recap-page__tab family--mine'
                  }
                  aria-pressed={effectiveBasis === 'mine'}
                  onClick={() => updateParams({ basis: null }, { keepScroll: true })}
                >
                  My score
                </button>
                <button
                  type="button"
                  className={
                    effectiveBasis === 'mal'
                      ? 'recap-page__tab recap-page__tab--active family--mal'
                      : 'recap-page__tab family--mal'
                  }
                  aria-pressed={effectiveBasis === 'mal'}
                  onClick={() => updateParams({ basis: 'mal' }, { keepScroll: true })}
                >
                  MAL score
                </button>
              </div>
            )}
            {(typeOptions.length > 0 || hasUnknownType) && (
              <select
                className="recap-page__type-select"
                value={typeFilter}
                onChange={(e) =>
                  updateParams({ type: e.target.value === 'all' ? null : e.target.value }, { keepScroll: true })
                }
                aria-label="Filter top 10 by type"
              >
                <option value="all">All types</option>
                {typeOptions.map((t) => (
                  <option key={t} value={t}>
                    {mediaTypeLabel(t)}
                  </option>
                ))}
                {hasUnknownType && <option value="unknown">Unknown</option>}
              </select>
            )}
          </div>
        </div>

        {topTen.length === 0 ? (
          <p className="recap-page__empty-note">No anime of this type in this period.</p>
        ) : (
          <>
            <ol className="recap-podium">{podium.map((item, index) => renderPodiumCard(item, index, effectiveBasis))}</ol>
            {rows.length > 0 && (
              <ol className="recap-top-ten-list" start={6}>
                {rows.map((item, index) => (
                  <li key={item.animeId} className="recap-top-ten-row">
                    <span className="recap-top-ten-row__rank">#{index + 6}</span>
                    <Link to={`/anime/${item.animeId}`} className="recap-top-ten-row__link">
                      {item.pictureUrl ? (
                        <img src={item.pictureUrl} alt="" className="recap-top-ten-row__picture" />
                      ) : (
                        <div className="recap-top-ten-row__picture recap-top-ten-row__picture--placeholder" aria-hidden="true" />
                      )}
                      <span className="recap-top-ten-row__title" title={pickDisplayTitle(item.title, item.englishTitle)}>
                        {pickDisplayTitle(item.title, item.englishTitle)}
                      </span>
                    </Link>
                    <span
                      className={
                        effectiveBasis === 'mine'
                          ? 'recap-top-ten-row__score score--mine'
                          : 'recap-top-ten-row__score score--mal'
                      }
                    >
                      {effectiveBasis === 'mine' ? (
                        (item.myScore ?? '—')
                      ) : (
                        <ScoreValue value={item.malScore} completed={item.malRevealed} />
                      )}
                    </span>
                  </li>
                ))}
              </ol>
            )}
          </>
        )}

        {narrowed.length > TOP_TEN_SIZE && (
          <Link
            to={`/my-list?${myListScopeSearch(mode, startYear, endYear, season, filter, typeFilter)}`}
            className="recap-page__see-all"
          >
            See all {narrowed.length} in my list
          </Link>
        )}
      </section>
    )
  }

  function renderSeasonRankingSection(recap: RecapDto, style?: CSSProperties) {
    if (recap.seasonRanking.length === 0) return null
    return (
      <RankingSection
        title="Season ranking"
        noun="seasons"
        rows={
          seasonScore === null
            ? recap.seasonRanking.map((row) => describeSeasonRanking(row))
            : rankByScoreCount(recap.seasonRanking, seasonScore).map((row) => describeSeasonRanking(row, seasonScore))
        }
        onSeeAll={setOverlay}
        style={style}
        family="season"
        scoreFilter={{
          scores: offeredSeasonScores,
          selected: seasonScore,
          onSelect: (score) =>
            updateParams({ seasonScore: score === null ? null : String(score) }, { keepScroll: true }),
        }}
      />
    )
  }

  function renderSeasonTimeRankingSection(recap: RecapDto, style?: CSSProperties) {
    if (recap.seasonTimeRanking.length === 0) return null
    return (
      <RankingSection
        title="Seasons by time watched"
        noun="seasons"
        rows={recap.seasonTimeRanking.map(describeTimeRanking)}
        onSeeAll={setOverlay}
        style={style}
        family="season"
      />
    )
  }

  function renderYearRankingSection(recap: RecapDto, style?: CSSProperties) {
    if (recap.yearRanking.length === 0) return null
    return (
      <RankingSection
        title="Year ranking"
        noun="years"
        rows={
          yearScore === null
            ? recap.yearRanking.map((row) => describeYearRanking(row))
            : rankByScoreCount(recap.yearRanking, yearScore).map((row) => describeYearRanking(row, yearScore))
        }
        onSeeAll={setOverlay}
        style={style}
        family="year"
        scoreFilter={{
          scores: offeredYearScores,
          selected: yearScore,
          onSelect: (score) => updateParams({ yearScore: score === null ? null : String(score) }, { keepScroll: true }),
        }}
      />
    )
  }

  function renderYearTimeRankingSection(recap: RecapDto, style?: CSSProperties) {
    if (recap.yearTimeRanking.length === 0) return null
    return (
      <RankingSection
        title="Years by time watched"
        noun="years"
        rows={recap.yearTimeRanking.map(describeTimeRanking)}
        onSeeAll={setOverlay}
        style={style}
        family="year"
      />
    )
  }

  // Rankings grouped into two columns (design.md decision 4), with one
  // exception: a yearly recap has no year-level rankings at all, so rather
  // than stacking its two season-level rankings full-width in a single
  // column, they sit side by side — the score ranking and the time-watched
  // ranking directly comparing a period's seasons. A multi-year recap keeps
  // the season-column/year-column grouping, but as a flat row-aligned grid
  // (design.md decision 8) rather than two independently-stacking flex
  // columns: each present section is placed directly with its own
  // grid-column/grid-row, so the season-level and year-level time-watched
  // rankings always start on the same line regardless of whether the score
  // ranking above either carries a "See all". Either way the grid collapses
  // to one column when only one side has anything to show.
  function renderRankings(recap: RecapDto) {
    if (mode === 'yearly') {
      const seasonRanking = renderSeasonRankingSection(recap)
      const seasonTime = renderSeasonTimeRankingSection(recap)
      if (!seasonRanking && !seasonTime) return null
      const single = !seasonRanking || !seasonTime
      return (
        <div className={single ? 'recap-page__rankings recap-page__rankings--single' : 'recap-page__rankings'}>
          {seasonRanking}
          {seasonTime}
        </div>
      )
    }

    const hasSeasonColumn = recap.seasonRanking.length > 0 || recap.seasonTimeRanking.length > 0
    const hasYearColumn = recap.yearRanking.length > 0 || recap.yearTimeRanking.length > 0
    if (!hasSeasonColumn && !hasYearColumn) return null

    const seasonColumnIndex = hasSeasonColumn ? 1 : null
    const yearColumnIndex = hasYearColumn ? (hasSeasonColumn ? 2 : 1) : null
    const single = hasSeasonColumn !== hasYearColumn

    return (
      <div className={single ? 'recap-page__rankings recap-page__rankings--single' : 'recap-page__rankings'}>
        {seasonColumnIndex && renderSeasonRankingSection(recap, { gridColumn: seasonColumnIndex, gridRow: 1 })}
        {seasonColumnIndex && renderSeasonTimeRankingSection(recap, { gridColumn: seasonColumnIndex, gridRow: 2 })}
        {yearColumnIndex && renderYearRankingSection(recap, { gridColumn: yearColumnIndex, gridRow: 1 })}
        {yearColumnIndex && renderYearTimeRankingSection(recap, { gridColumn: yearColumnIndex, gridRow: 2 })}
      </div>
    )
  }

  return (
    <div className="recap-page">
      <div className="recap-page__header">
        <h1>Recap of {periodLabel}</h1>
        {renderModeTabs()}
      </div>

      <div className="recap-page__controls">
        {renderPeriodControls()}
        <div className="recap-page__controls-trailing">
          {renderFilterToggle()}
          {renderSeasonPageLink()}
          {renderYearPageLink()}
        </div>
      </div>

      {loading && !displayedRecap && <p className="recap-page__loading">Loading&hellip;</p>}

      {displayedRecap && displayedRecap.items.length === 0 && (
        <div className="recap-page__empty">
          <p>Nothing to recap for {periodLabel}.</p>
        </div>
      )}

      {displayedRecap && displayedRecap.items.length > 0 && (
        <>
          {/* Lead section (design.md decision 5): the top anime leads, with
              the stat block and the distribution below it beside the top
              anime — top anime first in DOM order so a narrow display (where
              the grid collapses to one column) stacks the top anime above
              them. The lead grid keeps exactly two children: the top anime
              and this single right-hand column. */}
          <div className="recap-page__lead">
            {renderTopTen(displayedRecap)}
            <div className="recap-page__lead-aside">
              {renderStats(displayedRecap.stats)}
              {renderDistribution(scoreBuckets, scoreGroups)}
            </div>
          </div>

          {renderRankings(displayedRecap)}

          {renderHotTakes(displayedRecap.stats.hotTakes)}
        </>
      )}

      {overlay && (
        <RankingOverlay title={overlay.title} rows={overlay.rows} family={overlay.family} onClose={() => setOverlay(null)} />
      )}
      {boardOpen && (
        <ScoreBoardOverlay
          title={`Score board — ${periodLabel}`}
          groups={scoreGroups}
          onClose={() => setBoardOpen(false)}
        />
      )}
    </div>
  )
}

function CalendarIcon() {
  return (
    <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <rect x="3" y="5" width="18" height="16" rx="3" />
      <path d="M3 10h18" />
      <path d="M8 3v4M16 3v4" />
    </svg>
  )
}
