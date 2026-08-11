import { Link, useSearchParams } from 'react-router-dom'
import { getAiringWeek } from '../api/client.ts'
import type { AiringWeekDto } from '../api/types.ts'
import { usePageData } from '../hooks/usePageData.ts'
import { pickDisplayTitle } from '../utils/anime.ts'
import './AiringPage.css'

// Local calendar date, formatted without ever going through toISOString
// (which converts to UTC and can land on the wrong day for any non-UTC zone).
function toLocalIso(date: Date): string {
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

function todayIso(): string {
  return toLocalIso(new Date())
}

function isIsoDate(value: string | null): value is string {
  return value !== null && /^\d{4}-\d{2}-\d{2}$/.test(value) && !Number.isNaN(Date.parse(value))
}

// Twelve month names in the viewer's locale, built from a fixed reference
// year so leap-year length never affects the label.
const MONTH_LABELS = Array.from({ length: 12 }, (_, index) =>
  new Date(2000, index, 1).toLocaleDateString(undefined, { month: 'long' }),
)

// The target ISO date for a chosen month/year, keeping the current
// day-of-month and clamping to the target month's last day (31 Jan -> Feb
// lands on the 28th/29th rather than overflowing into March).
function dateForMonthYear(referenceDate: string, year: number, month: number): string {
  const day = Number(referenceDate.slice(8, 10))
  const lastDayOfMonth = new Date(year, month, 0).getDate()
  const clampedDay = Math.min(day, lastDayOfMonth)
  return `${year}-${String(month).padStart(2, '0')}-${String(clampedDay).padStart(2, '0')}`
}

function addDaysIso(iso: string, days: number): string {
  const date = new Date(`${iso}T00:00:00`)
  date.setDate(date.getDate() + days)
  return toLocalIso(date)
}

// Monday of the local week containing the given date — used to tell whether the
// displayed week is the current one (so "current" can be disabled).
function weekStartIso(iso: string): string {
  const date = new Date(`${iso}T00:00:00`)
  const daysSinceMonday = (date.getDay() + 6) % 7
  return addDaysIso(iso, -daysSinceMonday)
}

// "Jul 6 – Jul 12, 2026": the year is shown once, at the end of the range.
function formatWeekRange(weekStart: string, weekEnd: string): string {
  const start = new Date(weekStart).toLocaleDateString(undefined, { month: 'short', day: 'numeric' })
  const end = new Date(weekEnd).toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' })
  return `${start} – ${end}`
}

// Weekly schedule of my-list anime, laid out as seven local day-columns.
// Navigation moves whole weeks at a time; the date picker jumps straight to the
// week containing any chosen date; "current" jumps back to today's week. The
// selected week lives in the URL (not component state) so it survives
// back-navigation from an anime detail page, and defaults to today when absent.
export function AiringPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const weekParam = searchParams.get('week')
  const referenceDate = isIsoDate(weekParam) ? weekParam : todayIso()

  const { data: week } = usePageData<AiringWeekDto>(`airing:${referenceDate}`, () => getAiringWeek(referenceDate))

  function goToWeek(date: string) {
    setSearchParams((prev) => {
      const params = new URLSearchParams(prev)
      params.set('week', date)
      return params
    })
  }

  const isCurrentWeek = weekStartIso(referenceDate) === weekStartIso(todayIso())
  const isEmptyWeek = week !== null && week.days.every((day) => day.slots.length === 0)

  // Sliced, not `new Date(referenceDate)` — see toLocalIso above.
  const selectedYear = Number(referenceDate.slice(0, 4))
  const selectedMonth = Number(referenceDate.slice(5, 7))
  const currentYear = new Date().getFullYear()
  const jumpYears: number[] = []
  for (let year = currentYear + 1; year >= 1960; year--) jumpYears.push(year)

  return (
    <div className="airing-page">
      <div className="airing-page__header">
        <h1>Schedule</h1>
        <div className="airing-page__nav">
          <button type="button" onClick={() => goToWeek(addDaysIso(referenceDate, -7))} aria-label="Previous week">
            &lsaquo;
          </button>
          <button type="button" onClick={() => goToWeek(todayIso())} disabled={isCurrentWeek}>
            current
          </button>
          <button type="button" onClick={() => goToWeek(addDaysIso(referenceDate, 7))} aria-label="Next week">
            &rsaquo;
          </button>
        </div>
        <div className="airing-page__jump">
          <span>Jump to</span>
          <select
            aria-label="Month"
            value={selectedMonth}
            onChange={(event) => goToWeek(dateForMonthYear(referenceDate, selectedYear, Number(event.target.value)))}
          >
            {MONTH_LABELS.map((label, index) => (
              <option key={label} value={index + 1}>
                {label}
              </option>
            ))}
          </select>
          <select
            aria-label="Year"
            value={selectedYear}
            onChange={(event) => goToWeek(dateForMonthYear(referenceDate, Number(event.target.value), selectedMonth))}
          >
            {jumpYears.map((year) => (
              <option key={year} value={year}>
                {year}
              </option>
            ))}
          </select>
        </div>
        {week && <span className="airing-page__range">{formatWeekRange(week.weekStart, week.weekEnd)}</span>}
      </div>

      {week &&
        (isEmptyWeek ? (
          <p className="airing-page__empty">Nothing airing this week.</p>
        ) : (
          <div className="airing-page__grid">
            {week.days.map((day) => (
              <div key={day.localDate} className="airing-day">
                {/* Sliced, not `new Date(day.localDate)` — parsing a bare ISO date
                    with the Date constructor reads it as UTC and can land on the
                    wrong local day (see toLocalIso above). */}
                <div className="airing-day__header">
                  {day.dayOfWeek} {Number(day.localDate.slice(8, 10))}.{Number(day.localDate.slice(5, 7))}
                </div>
                <ul className="airing-day__slots">
                  {day.slots.map((slot) => (
                    <li key={slot.animeId}>
                      <Link to={`/anime/${slot.animeId}`} className="airing-slot">
                        <span className="airing-slot__time">{slot.localTime}</span>
                        <span className="airing-slot__body">
                          {slot.pictureUrl ? (
                            <img src={slot.pictureUrl} alt="" className="airing-slot__thumb" />
                          ) : (
                            <div className="airing-slot__thumb airing-slot__thumb--placeholder" aria-hidden="true" />
                          )}
                          <span className="airing-slot__info">
                            <span className="airing-slot__title">{pickDisplayTitle(slot.title, slot.englishTitle)}</span>
                            <span className="airing-slot__episode">Ep {slot.episodeNumber ?? '—'}</span>
                          </span>
                        </span>
                      </Link>
                    </li>
                  ))}
                </ul>
              </div>
            ))}
          </div>
        ))}
    </div>
  )
}
