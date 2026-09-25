import { useMemo } from 'react'
import { Link, Navigate, useSearchParams } from 'react-router-dom'
import { getAiringWeek } from '../api/client.ts'
import type { AiringSlotDto, AiringWeekDto } from '../api/types.ts'
import { LoadFailedNotice } from '../components/LoadFailedNotice.tsx'
import { LoadingNotice } from '../components/LoadingNotice.tsx'
import { PosterPicture } from '../components/PosterPicture.tsx'
import { useDelayedFlag } from '../hooks/useDelayedFlag.ts'
import { useHeldData } from '../hooks/useHeldData.ts'
import { usePageData } from '../hooks/usePageData.ts'
import { pickDisplayTitle } from '../utils/anime.ts'
import { EARLIEST_YEAR, yearsInRange } from '../utils/browseRange.ts'
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

// Mirrors the backend's AiringWeekRange.YearsAhead: the schedule reaches
// this many years past the current one — at least a full year beyond
// wherever airing rows are stored, and where the year selector already
// stopped (design D7 of bound-recap-and-airing-range).
const AIRING_YEARS_AHEAD = 1

// The schedule's floor — the same earliest year the Season, Year and Recap
// pages use. 1 January 1917 is a Monday, so the first week of the range is
// exactly 1-7 January and the previous arrow needs no special case.
const floorIso = `${EARLIEST_YEAR}-01-01`

function ceilingIso(currentYear: number): string {
  return `${currentYear + AIRING_YEARS_AHEAD}-12-31`
}

// A week is addressable when its ?week= names a real calendar date between
// floorIso and ceilingIso(currentYear), inclusive. Never goes through
// Date.parse (design D7): the range check is a plain string comparison,
// which zero-padded four-digit ISO dates sort correctly under, and which
// rejects a year like 0000 before any Date is constructed — new Date(y, ...)
// would otherwise map a year below 100 onto 1900-something.
function isAddressableWeekDate(value: string | null, today: string): value is string {
  if (value === null || !/^\d{4}-\d{2}-\d{2}$/.test(value)) return false
  if (value < floorIso || value > ceilingIso(Number(today.slice(0, 4)))) return false
  const month = Number(value.slice(5, 7))
  if (month < 1 || month > 12) return false
  const day = Number(value.slice(8, 10))
  return day >= 1 && day <= new Date(Number(value.slice(0, 4)), month, 0).getDate()
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

// A slot's episode number, or an `Ep {start}-{end}` range when it represents
// more than one merged episode of the same anime.
function formatEpisodeLabel(slot: AiringSlotDto): string {
  if (slot.episodeNumber === null) return 'Ep —'
  if (slot.episodeNumberEnd !== null && slot.episodeNumberEnd > slot.episodeNumber) {
    return `Ep ${slot.episodeNumber}-${slot.episodeNumberEnd}`
  }
  return `Ep ${slot.episodeNumber}`
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

// Route guard (design D1/D7 of bound-recap-and-airing-range): validates the
// URL's ?week= before anything mounts, and renders either a
// history-replacing redirect to today's week or the page body with the
// validated reference date as a prop. AiringPageView is rendered unkeyed, at
// this fixed position, so stepping a week changes only its prop rather than
// remounting the view.
export function AiringPage() {
  const [searchParams] = useSearchParams()
  const today = useMemo(todayIso, [])
  const weekParam = searchParams.get('week')

  if (weekParam !== null && !isAddressableWeekDate(weekParam, today)) {
    const params = new URLSearchParams(searchParams)
    params.set('week', today)
    return <Navigate to={`/airing?${params.toString()}`} replace />
  }

  return <AiringPageView referenceDate={weekParam ?? today} />
}

// Weekly schedule of my-list anime, laid out as seven local day-columns.
// Navigation moves whole weeks at a time; the date picker jumps straight to the
// week containing any chosen date; "current" jumps back to today's week. The
// selected week lives in the URL (not component state) so it survives
// back-navigation from an anime detail page, and defaults to today when absent.
// The schedule covers 1 January 1917 through 31 December of next year
// (AiringWeekRange on the backend); `referenceDate` has already been
// validated by the AiringPage guard above.
//
// Stepping to another week keeps the previous week's schedule on screen
// (useHeldData) until the new one lands, muted once the load outlasts the
// loading-indicator delay, so a step never collapses the page to a header.
// The range label is part of what is held; the controls always name the week
// stepped to.
function AiringPageView({ referenceDate }: { referenceDate: string }) {
  const [, setSearchParams] = useSearchParams()

  const {
    data: weekData,
    failed,
    retry,
  } = usePageData<AiringWeekDto>(`airing:${referenceDate}`, () => getAiringWeek(referenceDate))
  const { shown: week, stale } = useHeldData(weekData, failed)
  const holdingMuted = useDelayedFlag(stale)

  // Replaces rather than pushes (polish-rewatch-more-and-filters design.md
  // D7): stepping through weeks must never grow the history stack, so one
  // Back leaves the Airing page for wherever the user came from regardless
  // of how many weeks were stepped through. The week stays in `?week=`, so
  // the URL is still shareable and still survives back-navigation from an
  // anime's detail page (that navigation is a push away from here and a pop
  // back to it, untouched by this).
  //
  // `keepScroll` is required, not incidental: a replace mints a fresh
  // `location.key`, which `useScrollRestoration` would otherwise read as a
  // fresh visit and answer with `scrollTo(0, 0)` — jerking the page to the
  // top on every week change. The flag also seeds the new entry's snapshot
  // with the current scroll position, so navigating away and back returns
  // here rather than to the top.
  function goToWeek(date: string) {
    setSearchParams(
      (prev) => {
        const params = new URLSearchParams(prev)
        params.set('week', date)
        return params
      },
      { replace: true, state: { keepScroll: true } },
    )
  }

  const isCurrentWeek = weekStartIso(referenceDate) === weekStartIso(todayIso())
  const isEmptyWeek = week !== null && week.days.every((day) => day.slots.length === 0)
  const today = todayIso()

  // Sliced, not `new Date(referenceDate)` — see toLocalIso above.
  const selectedYear = Number(referenceDate.slice(0, 4))
  const selectedMonth = Number(referenceDate.slice(5, 7))
  const currentYear = new Date().getFullYear()
  const ceiling = ceilingIso(currentYear)
  const jumpYears = yearsInRange(EARLIEST_YEAR, currentYear + AIRING_YEARS_AHEAD)

  // Arrow bounds (design D7): the previous arrow stops at the range's floor
  // and the next arrow stops once no later week starts by the ceiling, so a
  // Sunday reference late in December lands on the final week rather than
  // past it. Compared as plain ISO strings — safe because every value here
  // is a zero-padded four-digit-year date.
  const previousWeekTarget = addDaysIso(referenceDate, -7)
  const isPreviousDisabled = weekStartIso(referenceDate) <= floorIso
  const nextWeekTarget = addDaysIso(referenceDate, 7)
  const isNextDisabled = addDaysIso(weekStartIso(referenceDate), 7) > ceiling

  return (
    <div className="airing-page">
      <div className="airing-page__header">
        <h1>Schedule</h1>
        <div className="airing-page__nav">
          <button
            type="button"
            onClick={() => goToWeek(previousWeekTarget < floorIso ? floorIso : previousWeekTarget)}
            aria-label="Previous week"
            disabled={isPreviousDisabled}
          >
            &lsaquo;
          </button>
          <button type="button" onClick={() => goToWeek(todayIso())} disabled={isCurrentWeek}>
            current
          </button>
          <button
            type="button"
            onClick={() => goToWeek(nextWeekTarget > ceiling ? ceiling : nextWeekTarget)}
            aria-label="Next week"
            disabled={isNextDisabled}
          >
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
        {week && (
          <span className={holdingMuted ? 'airing-page__range held-content--muted' : 'airing-page__range'}>
            {formatWeekRange(week.weekStart, week.weekEnd)}
          </span>
        )}
      </div>

      {/* "Nothing airing this week." only for a week that loaded: a read that
          failed is a failure, and one still in flight shows the (delayed)
          loading notice, never an empty schedule. */}
      {!week && failed && <LoadFailedNotice what="this week's schedule" onRetry={retry} />}
      {!week && !failed && <LoadingNotice className="airing-page__loading" />}
      {week &&
        (isEmptyWeek ? (
          <p className={holdingMuted ? 'airing-page__empty held-content--muted' : 'airing-page__empty'}>
            Nothing airing this week.
          </p>
        ) : (
          <div className={holdingMuted ? 'airing-page__grid held-content--muted' : 'airing-page__grid'}>
            {week.days.map((day) => {
              const isToday = day.localDate === today
              return (
                <div key={day.localDate} className="airing-day">
                  {/* Sliced, not `new Date(day.localDate)` — parsing a bare ISO date
                      with the Date constructor reads it as UTC and can land on the
                      wrong local day (see toLocalIso above). */}
                  <div
                    className={isToday ? 'airing-day__header airing-day__header--today' : 'airing-day__header'}
                    aria-current={isToday ? 'date' : undefined}
                  >
                    {day.dayOfWeek} {Number(day.localDate.slice(8, 10))}.{Number(day.localDate.slice(5, 7))}
                  </div>
                  <ul className="airing-day__slots">
                    {day.slots.map((slot) => (
                      <li key={`${slot.animeId}-${slot.localTime}-${slot.episodeNumber ?? 'x'}`}>
                        {/* A card, not a row: the picture band sits above the title
                            because a seventh-of-a-page column can't share its width
                            between a picture and its text. Time and episode share a
                            header strip above the band, never over the picture, and
                            grid placement sets that layout, so the DOM order stays
                            time, title, episode for the link's accessible name. */}
                        <Link to={`/anime/${slot.animeId}`} className="airing-slot">
                          <span className="airing-slot__time">{slot.localTime}</span>
                          <PosterPicture src={slot.pictureUrl} className="airing-slot__art" whole loading="lazy" />
                          <span className="airing-slot__title">{pickDisplayTitle(slot.title, slot.englishTitle)}</span>
                          <span className="airing-slot__episode">{formatEpisodeLabel(slot)}</span>
                        </Link>
                      </li>
                    ))}
                  </ul>
                </div>
              )
            })}
          </div>
        ))}
    </div>
  )
}
