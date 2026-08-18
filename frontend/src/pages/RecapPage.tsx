import { useEffect, useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { getRecap } from '../api/client.ts'
import {
  RECAP_SEASONS,
  type RecapDto,
  type RecapHotTakeDto,
  type RecapMode,
  type RecapRowDto,
  type RecapSeasonName,
  type RecapSeasonRankingDto,
  type RecapStatsDto,
  type RecapTimeFilter,
  type RecapYearRankingDto,
} from '../api/types.ts'
import { ScoreValue } from '../components/ScoreValue.tsx'
import { YearRankingOverlay } from '../components/YearRankingOverlay.tsx'
import { usePageData } from '../hooks/usePageData.ts'
import { formatRuntime, MEDIA_TYPE_ORDER, mediaTypeLabel, pickDisplayTitle, seasonLabel } from '../utils/anime.ts'
import './RecapPage.css'

type RankingBasis = 'mine' | 'mal'

const MODE_OPTIONS: { value: RecapMode; label: string }[] = [
  { value: 'multiYear', label: 'Multi-year' },
  { value: 'yearly', label: 'Yearly' },
  { value: 'season', label: 'Season' },
]

// A generous floor for the year selects — recaps look at the past, so
// there's no MAL-catalog-style ceiling to mirror like SeasonPage's.
const EARLIEST_YEAR = 1960
const TOP_TEN_SIZE = 10
const VISIBLE_YEAR_RANKS = 5

function isRecapMode(value: string | null): value is RecapMode {
  return value === 'multiYear' || value === 'yearly' || value === 'season'
}

function isSeasonName(value: string | null): value is RecapSeasonName {
  return value !== null && (RECAP_SEASONS as readonly string[]).includes(value)
}

function currentSeasonName(): RecapSeasonName {
  return RECAP_SEASONS[Math.floor(new Date().getMonth() / 3)]
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
// back out (tasks.md 7.3).
function myListScopeSearch(
  mode: RecapMode,
  startYear: number,
  endYear: number,
  season: RecapSeasonName,
  filter: RecapTimeFilter,
  typeFilter: string,
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
  const [yearOverlayOpen, setYearOverlayOpen] = useState(false)

  const modeParam = searchParams.get('mode')
  const mode: RecapMode = isRecapMode(modeParam) ? modeParam : 'yearly'

  const fromParam = Number(searchParams.get('from'))
  const toParam = Number(searchParams.get('to'))
  const yearParam = Number(searchParams.get('year'))
  const seasonParam = searchParams.get('season')
  const filterParam = searchParams.get('filter')
  const basisParam = searchParams.get('basis')
  const typeParam = searchParams.get('type')

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

  function updateParams(updates: Record<string, string | null>) {
    setSearchParams((prev) => {
      const params = new URLSearchParams(prev)
      for (const [key, value] of Object.entries(updates)) {
        if (value === null) params.delete(key)
        else params.set(key, value)
      }
      return params
    })
  }

  // Falls back off a selected filter that turned out empty for this period,
  // rather than showing an impossible-selection empty state (spec
  // scenario "Falling back when the selection becomes unavailable"). Only
  // acts once the response actually matches the current selection, so a
  // rapid sequence of changes can't flip the filter based on a stale reply.
  useEffect(() => {
    if (!recap || mode === 'season') return
    if (recap.mode !== mode || recap.startYear !== startYear || recap.endYear !== endYear) return
    const currentCount = filter === 'watched' ? recap.watchedCount : recap.airedCount
    const otherCount = filter === 'watched' ? recap.airedCount : recap.watchedCount
    if (currentCount === 0 && otherCount > 0) updateParams({ filter: filter === 'watched' ? 'aired' : 'watched' })
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [recap, mode, startYear, endYear, filter])

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
        {MODE_OPTIONS.map((option) => (
          <button
            key={option.value}
            type="button"
            role="tab"
            aria-selected={mode === option.value}
            className={mode === option.value ? 'recap-page__tab recap-page__tab--active' : 'recap-page__tab'}
            onClick={() => switchMode(option.value)}
          >
            {option.label}
          </button>
        ))}
      </div>
    )
  }

  function renderPeriodControls() {
    const years = yearOptions(Math.min(EARLIEST_YEAR, startYear, endYear), Math.max(currentYear, startYear, endYear))
    return (
      <div className="recap-page__period-controls">
        {mode === 'multiYear' && (
          <>
            <select value={startYear} onChange={(e) => updateParams({ from: e.target.value })} aria-label="From year">
              {years.map((y) => (
                <option key={y} value={y}>
                  {y}
                </option>
              ))}
            </select>
            <span className="recap-page__period-sep">&ndash;</span>
            <select value={endYear} onChange={(e) => updateParams({ to: e.target.value })} aria-label="To year">
              {years.map((y) => (
                <option key={y} value={y}>
                  {y}
                </option>
              ))}
            </select>
          </>
        )}
        {mode === 'yearly' && (
          <select value={startYear} onChange={(e) => updateParams({ year: e.target.value })} aria-label="Year">
            {years.map((y) => (
              <option key={y} value={y}>
                {y}
              </option>
            ))}
          </select>
        )}
        {mode === 'season' && (
          <>
            <select value={season} onChange={(e) => updateParams({ season: e.target.value })} aria-label="Season">
              {RECAP_SEASONS.map((s) => (
                <option key={s} value={s}>
                  {seasonLabel(s)}
                </option>
              ))}
            </select>
            <select value={startYear} onChange={(e) => updateParams({ year: e.target.value })} aria-label="Year">
              {years.map((y) => (
                <option key={y} value={y}>
                  {y}
                </option>
              ))}
            </select>
          </>
        )}
      </div>
    )
  }

  function renderFilterToggle() {
    if (mode === 'season') return null
    const watchedDisabled = recap ? recap.watchedCount === 0 : false
    const airedDisabled = recap ? recap.airedCount === 0 : false
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

  function renderStats(stats: RecapStatsDto) {
    const tiles: { label: string; value: string }[] = [
      { label: 'Mean score', value: stats.meanScore !== null ? stats.meanScore.toFixed(2) : '—' },
      { label: 'Anime', value: String(stats.animeCounted) },
      { label: 'Completed', value: String(stats.completed) },
      { label: 'Episodes watched', value: String(stats.episodesWatched) },
      { label: 'Movies watched', value: String(stats.moviesWatched) },
      { label: 'Time spent', value: formatRuntime(stats.timeSpentSeconds) },
    ]
    return (
      <div className="recap-page__stats">
        {tiles.map((tile) => (
          <div key={tile.label} className="recap-page__stat">
            <span className="recap-page__stat-value">{tile.value}</span>
            <span className="recap-page__stat-label">{tile.label}</span>
          </div>
        ))}
      </div>
    )
  }

  function renderHotTake(take: RecapHotTakeDto) {
    const malLikedMore = take.divergence > 0
    return (
      <li key={take.animeId} className="recap-hot-take">
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
      <section className="recap-page__section">
        <h2>Hot takes</h2>
        {hotTakes.length === 0 ? (
          <p className="recap-page__empty-note">No hot takes for this period.</p>
        ) : (
          <ul className="recap-hot-take-list">{hotTakes.map(renderHotTake)}</ul>
        )}
      </section>
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
      return a.title.localeCompare(b.title)
    })
    const topTen = ranked.slice(0, TOP_TEN_SIZE)

    return (
      <section className="recap-page__section">
        <div className="recap-page__section-header">
          <h2>Top {Math.min(TOP_TEN_SIZE, narrowed.length)}</h2>
          <div className="recap-page__top-ten-controls">
            {basisControlVisible && (
              <div className="recap-page__basis-toggle" role="group" aria-label="Rank top 10 by">
                <button
                  type="button"
                  className={effectiveBasis === 'mine' ? 'recap-page__tab recap-page__tab--active' : 'recap-page__tab'}
                  aria-pressed={effectiveBasis === 'mine'}
                  onClick={() => updateParams({ basis: null })}
                >
                  My score
                </button>
                <button
                  type="button"
                  className={effectiveBasis === 'mal' ? 'recap-page__tab recap-page__tab--active' : 'recap-page__tab'}
                  aria-pressed={effectiveBasis === 'mal'}
                  onClick={() => updateParams({ basis: 'mal' })}
                >
                  MAL score
                </button>
              </div>
            )}
            {(typeOptions.length > 0 || hasUnknownType) && (
              <select
                className="recap-page__type-select"
                value={typeFilter}
                onChange={(e) => updateParams({ type: e.target.value === 'all' ? null : e.target.value })}
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
          <ol className="recap-top-ten-list">
            {topTen.map((item, index) => (
              <li key={item.animeId} className="recap-top-ten-row">
                <span className="recap-top-ten-row__rank">#{index + 1}</span>
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
                <span className="recap-top-ten-row__score">
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

  function renderPosters(posters: { animeId: number; title: string; pictureUrl: string | null }[]) {
    if (posters.length === 0) return null
    return (
      <span className="recap-ranking-row__posters">
        {posters.map((p) =>
          p.pictureUrl ? (
            <img key={p.animeId} src={p.pictureUrl} alt="" title={p.title} className="recap-ranking-row__poster" />
          ) : (
            <span key={p.animeId} className="recap-ranking-row__poster recap-ranking-row__poster--placeholder" title={p.title} />
          ),
        )}
      </span>
    )
  }

  function renderSeasonRanking(ranking: RecapSeasonRankingDto[]) {
    return (
      <section className="recap-page__section">
        <h2>Season ranking</h2>
        <ol className="recap-ranking-list">
          {ranking.map((row, index) => (
            <li key={`${row.year}-${row.season}`} className="recap-ranking-row">
              <Link to={`/recap?mode=season&year=${row.year}&season=${row.season}`} className="recap-ranking-row__link">
                <span className="recap-ranking-row__rank">#{index + 1}</span>
                <span className="recap-ranking-row__label">
                  {seasonLabel(row.season)} {row.year}
                </span>
                <span className="recap-ranking-row__meta">
                  {row.scoredCount} scored &middot; {row.weightedScore.toFixed(2)}
                </span>
                {renderPosters(row.topPosters)}
              </Link>
            </li>
          ))}
        </ol>
      </section>
    )
  }

  function renderYearRanking(ranking: RecapYearRankingDto[]) {
    const visible = ranking.slice(0, VISIBLE_YEAR_RANKS)
    return (
      <section className="recap-page__section">
        <h2>Year ranking</h2>
        <ol className="recap-ranking-list">
          {visible.map((row, index) => (
            <li key={row.year} className="recap-ranking-row">
              <Link to={`/recap?mode=yearly&year=${row.year}&filter=aired`} className="recap-ranking-row__link">
                <span className="recap-ranking-row__rank">#{index + 1}</span>
                <span className="recap-ranking-row__label">{row.year}</span>
                <span className="recap-ranking-row__meta">
                  {row.scoredCount} scored &middot; {row.weightedScore.toFixed(2)}
                </span>
                {renderPosters(row.topPosters)}
              </Link>
            </li>
          ))}
        </ol>
        {ranking.length > VISIBLE_YEAR_RANKS && (
          <button type="button" className="recap-page__see-all-years" onClick={() => setYearOverlayOpen(true)}>
            See all {ranking.length} years
          </button>
        )}
      </section>
    )
  }

  return (
    <div className="recap-page">
      <div className="recap-page__header">
        <h1>Recap</h1>
        {renderModeTabs()}
      </div>

      <div className="recap-page__controls">
        {renderPeriodControls()}
        {renderFilterToggle()}
      </div>

      {loading && !recap && <p className="recap-page__loading">Loading&hellip;</p>}

      {recap && recap.items.length === 0 && (
        <div className="recap-page__empty">
          <p>Nothing to recap for {periodLabel}.</p>
        </div>
      )}

      {recap && recap.items.length > 0 && (
        <>
          <h2 className="recap-page__period-label">{periodLabel}</h2>
          {renderStats(recap.stats)}
          {renderHotTakes(recap.stats.hotTakes)}
          {renderTopTen(recap)}
          {recap.seasonRanking.length > 0 && renderSeasonRanking(recap.seasonRanking)}
          {recap.yearRanking.length > 0 && renderYearRanking(recap.yearRanking)}
        </>
      )}

      {yearOverlayOpen && recap && (
        <YearRankingOverlay rankings={recap.yearRanking} onClose={() => setYearOverlayOpen(false)} />
      )}
    </div>
  )
}
