import { useEffect, useMemo, useState } from 'react'
import { getRecapAvailability } from '../api/client.ts'
import { RECAP_SEASONS, type RecapAvailabilityDto, type RecapMode, type RecapSeasonName, type RecapTimeFilter, type RecapYearAvailabilityDto } from '../api/types.ts'
import { seasonLabel } from '../utils/anime.ts'
import { Modal } from './Modal.tsx'
import './RecapPickerOverlay.css'

type RecapPickerOverlayProps = {
  onClose: () => void
  // Called with the query string for `/recap?...` once a confirmable
  // selection is submitted — the caller navigates.
  onConfirm: (search: string) => void
}

const MODE_OPTIONS: { value: RecapMode; label: string }[] = [
  { value: 'multiYear', label: 'Multi-year' },
  { value: 'yearly', label: 'Yearly' },
  { value: 'season', label: 'Season' },
]

function currentSeasonName(): RecapSeasonName {
  return RECAP_SEASONS[Math.floor(new Date().getMonth() / 3)]
}

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
// calls /api/recap directly; it hands the caller a query string for
// `/recap?...` to navigate to.
export function RecapPickerOverlay({ onClose, onConfirm }: RecapPickerOverlayProps) {
  const currentYear = useMemo(() => new Date().getFullYear(), [])
  const [availability, setAvailability] = useState<RecapAvailabilityDto | null>(null)

  const [mode, setMode] = useState<RecapMode>('yearly')
  const [from, setFrom] = useState(currentYear - 1)
  const [to, setTo] = useState(currentYear)
  const [year, setYear] = useState(currentYear)
  const [season, setSeason] = useState<RecapSeasonName>(currentSeasonName)
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

  const knownYears = availability?.years.map((y) => y.year) ?? []
  const minYear = knownYears.length > 0 ? Math.min(...knownYears, currentYear) : currentYear
  const maxYear = knownYears.length > 0 ? Math.max(...knownYears, currentYear) : currentYear
  const yearOptions = Array.from({ length: maxYear - minYear + 1 }, (_, i) => maxYear - i)

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
          Recap a period
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
                  {RECAP_SEASONS.map((s) => (
                    <option key={s} value={s}>
                      {seasonLabel(s)}
                    </option>
                  ))}
                </select>
              </label>
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
            Show recap
          </button>
        </div>
      </div>
    </Modal>
  )
}
