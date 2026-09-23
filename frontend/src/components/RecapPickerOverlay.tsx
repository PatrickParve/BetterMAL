import { useEffect, useMemo, useState } from 'react'
import { getRecapAvailability } from '../api/client.ts'
import { RECAP_SEASONS, type RecapAvailabilityDto, type RecapMode, type RecapSeasonName, type RecapTimeFilter, type RecapYearAvailabilityDto } from '../api/types.ts'
import { seasonLabel } from '../utils/anime.ts'
import { currentSeasonTarget, EARLIEST_YEAR, yearsInRange } from '../utils/browseRange.ts'
import { Modal } from './Modal.tsx'
import './RecapPickerOverlay.css'

type RecapPickerOverlayProps = {
  onClose: () => void
  // Called with the query string for `/recap?...` once a confirmable
  // selection is submitted — the caller decides what to do with it
  // (navigate, or apply as a scope elsewhere).
  onConfirm: (search: string) => void
  // Both default to today's period-picker copy; My list overrides them to
  // read as an apply-to-list action rather than a navigation (design.md
  // decision 2).
  title?: string
  confirmLabel?: string
}

const MODE_OPTIONS: { value: RecapMode; label: string }[] = [
  { value: 'multiYear', label: 'Multi-year' },
  { value: 'yearly', label: 'Yearly' },
  { value: 'season', label: 'Season' },
]

function sumCounts(years: RecapYearAvailabilityDto[], startYear: number, endYear: number): { watched: number; aired: number } {
  let watched = 0
  let aired = 0
  for (const y of years) {
    if (y.year >= startYear && y.year <= endYear) {
      watched += y.watchedCount
      aired += y.airedCount
    }
  }
  return { watched, aired }
}

// Recap type + period picker (tasks.md 7.1), opened from My list's "Recap a
// period" control. Loads /api/recap/availability once on open and gates the
// time filter locally from it (design.md decision 2) — the same
// unavailable-option rule the recap page itself applies. Confirming never
// calls /api/recap directly; it hands the caller a query string in the
// picker's own mode/from/to/year/season/filter vocabulary and leaves the
// caller to decide what that means — navigate to `/recap?...`, or (My
// list's caller, design.md decision 2) translate it into a list scope.
export function RecapPickerOverlay({
  onClose,
  onConfirm,
  title = 'Recap a period',
  confirmLabel = 'Show recap',
}: RecapPickerOverlayProps) {
  const current = useMemo(currentSeasonTarget, [])
  const [availability, setAvailability] = useState<RecapAvailabilityDto | null>(null)

  const [mode, setMode] = useState<RecapMode>('yearly')
  const [from, setFrom] = useState(current.year - 1)
  const [to, setTo] = useState(current.year)
  const [year, setYear] = useState(current.year)
  const [season, setSeason] = useState<RecapSeasonName>(current.season)
  const [filter, setFilter] = useState<RecapTimeFilter>('watched')

  useEffect(() => {
    let cancelled = false
    getRecapAvailability()
      .then((result) => {
        if (!cancelled) setAvailability(result)
      })
      .catch(() => {
        // Leave availability null — every option renders as available
        // rather than the picker becoming unusable over one failed request.
      })
    return () => {
      cancelled = true
    }
  }, [])

  // The picker's range matches the recap page's own (design D5 of
  // bound-recap-and-airing-range): years reach back to whatever availability
  // suggests, never before EARLIEST_YEAR, but availability can never raise
  // the top past the current year — an anime starting next season still
  // gives that year an aired count, and the recap page would refuse any
  // period past the current one anyway.
  const knownYears = availability?.years.map((y) => y.year) ?? []
  const earliestKnown = knownYears.length > 0 ? Math.min(...knownYears) : current.year
  const yearOptions = yearsInRange(Math.max(EARLIEST_YEAR, earliestKnown), current.year)
  // Within the current year, cut the season list to the current season —
  // mirrors the recap page's own renderPeriodControls (design D4).
  const seasonOptions =
    year === current.year
      ? RECAP_SEASONS.filter((s) => RECAP_SEASONS.indexOf(s) <= RECAP_SEASONS.indexOf(current.season))
      : RECAP_SEASONS

  const counts =
    availability && mode !== 'season'
      ? sumCounts(availability.years, mode === 'multiYear' ? Math.min(from, to) : year, mode === 'multiYear' ? Math.max(from, to) : year)
      : null
  const watchedDisabled = counts !== null && counts.watched === 0
  const airedDisabled = counts !== null && counts.aired === 0

  // Auto-corrects off a filter that just became unavailable (a period
  // change within the picker), same fallback the recap page itself applies.
  useEffect(() => {
    if (!availability || mode === 'season') return
    if (filter === 'watched' && watchedDisabled && !airedDisabled) setFilter('aired')
    else if (filter === 'aired' && airedDisabled && !watchedDisabled) setFilter('watched')
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [availability, watchedDisabled, airedDisabled, mode])

  const seasonCount = availability?.seasons.find((s) => s.year === year && s.season === season)?.count ?? 0
  const seasonUnavailable = availability !== null && seasonCount === 0

  // "An option covering no entries ... cannot be confirmed" (spec): for
  // multi-year/yearly that's the filter toggle (handled above); for season,
  // with no toggle to fall back to, it's the confirm action itself.
  const confirmDisabled =
    mode === 'season' ? seasonUnavailable : availability !== null && watchedDisabled && airedDisabled

  function handleConfirm() {
    if (confirmDisabled) return
    const params = new URLSearchParams({ mode })
    if (mode === 'multiYear') {
      params.set('from', String(Math.min(from, to)))
      params.set('to', String(Math.max(from, to)))
      params.set('filter', filter)
    } else if (mode === 'yearly') {
      params.set('year', String(year))
      params.set('filter', filter)
    } else {
      params.set('year', String(year))
      params.set('season', season)
    }
    onConfirm(params.toString())
  }

  return (
    <Modal onClose={onClose} labelledBy="recap-picker-overlay-title" className="recap-picker-overlay-modal">
      <div className="recap-picker-overlay">
        <h2 id="recap-picker-overlay-title" className="recap-picker-overlay__title">
          {title}
        </h2>

        <div className="recap-picker-overlay__mode-tabs" role="tablist" aria-label="Recap type">
          {MODE_OPTIONS.map((option) => (
            <button
              key={option.value}
              type="button"
              role="tab"
              aria-selected={mode === option.value}
              className={mode === option.value ? 'recap-picker-overlay__tab recap-picker-overlay__tab--active' : 'recap-picker-overlay__tab'}
              onClick={() => setMode(option.value)}
            >
              {option.label}
            </button>
          ))}
        </div>

        <div className="recap-picker-overlay__settings">
          {mode === 'multiYear' && (
            <div className="recap-picker-overlay__row">
              <label>
                From
                <select value={from} onChange={(e) => setFrom(Number(e.target.value))}>
                  {yearOptions.map((y) => (
                    <option key={y} value={y}>
                      {y}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                To
                <select value={to} onChange={(e) => setTo(Number(e.target.value))}>
                  {yearOptions.map((y) => (
                    <option key={y} value={y}>
                      {y}
                    </option>
                  ))}
                </select>
              </label>
            </div>
          )}

          {mode === 'yearly' && (
            <div className="recap-picker-overlay__row">
              <label>
                Year
                <select value={year} onChange={(e) => setYear(Number(e.target.value))}>
                  {yearOptions.map((y) => (
                    <option key={y} value={y}>
                      {y}
                    </option>
                  ))}
                </select>
              </label>
            </div>
          )}

          {mode === 'season' && (
            <div className="recap-picker-overlay__row">
              <label>
                Season
                <select value={season} onChange={(e) => setSeason(e.target.value as RecapSeasonName)}>
                  {seasonOptions.map((s) => (
                    <option key={s} value={s}>
                      {seasonLabel(s)}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                Year
                <select
                  value={year}
                  onChange={(e) => {
                    const nextYear = Number(e.target.value)
                    // Moving to the current year while a later season is
                    // selected moves the season back to the current one,
                    // since this select only changes the year half of the
                    // target (design D4/D5).
                    if (nextYear === current.year && RECAP_SEASONS.indexOf(season) > RECAP_SEASONS.indexOf(current.season)) {
                      setSeason(current.season)
                    }
                    setYear(nextYear)
                  }}
                >
                  {yearOptions.map((y) => (
                    <option key={y} value={y}>
                      {y}
                    </option>
                  ))}
                </select>
              </label>
            </div>
          )}

          {mode !== 'season' && (
            <div className="recap-picker-overlay__row">
              <span className="recap-picker-overlay__filter-label">Include</span>
              <div className="recap-picker-overlay__filter-toggle" role="group" aria-label="Time filter">
                <button
                  type="button"
                  className={filter === 'watched' ? 'recap-picker-overlay__tab recap-picker-overlay__tab--active' : 'recap-picker-overlay__tab'}
                  aria-pressed={filter === 'watched'}
                  disabled={watchedDisabled}
                  title={watchedDisabled ? 'Nothing completed or dropped in this period' : undefined}
                  onClick={() => setFilter('watched')}
                >
                  What I watched
                </button>
                <button
                  type="button"
                  className={filter === 'aired' ? 'recap-picker-overlay__tab recap-picker-overlay__tab--active' : 'recap-picker-overlay__tab'}
                  aria-pressed={filter === 'aired'}
                  disabled={airedDisabled}
                  title={airedDisabled ? 'Nothing aired in this period' : undefined}
                  onClick={() => setFilter('aired')}
                >
                  What aired
                </button>
              </div>
            </div>
          )}

          {confirmDisabled && <p className="recap-picker-overlay__empty-note">Nothing to recap for this selection.</p>}
        </div>

        <div className="recap-picker-overlay__buttons">
          <button type="button" className="recap-picker-overlay__cancel" onClick={onClose}>
            Cancel
          </button>
          <button type="button" className="recap-picker-overlay__confirm" disabled={confirmDisabled} onClick={handleConfirm}>
            {confirmLabel}
          </button>
        </div>
      </div>
    </Modal>
  )
}
