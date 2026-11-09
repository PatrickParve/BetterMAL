import { useEffect, useMemo, useRef, type KeyboardEvent } from 'react'
import { Link } from 'react-router-dom'
import type { RecapRowDto, RecapWatchingDayDto, RecapWatchingDaysDto } from '../api/types.ts'
import { useRestorableScroll } from '../hooks/useRestorableScroll.ts'
import {
  ISO_WEEKDAY_NAMES,
  formatDayRange,
  formatIsoDay,
  formatIsoDayLong,
  formatIsoDayShortWeekday,
  isoWeekdayOf,
  pickDisplayTitle,
} from '../utils/anime.ts'
import { Modal } from './Modal.tsx'
import { WatchingDayBlock } from './WatchingDayBlock.tsx'
import './WatchingDaysOverlay.css'

export type WatchingDaysTab = 'top' | 'calendar'

type WatchingDaysOverlayProps = {
  title: string
  watchingDays: RecapWatchingDaysDto
  items: RecapRowDto[]
  tab: WatchingDaysTab
  onTabChange: (tab: WatchingDaysTab) => void
  // The calendar's chosen day as an ISO day; null (or a day that is no longer a
  // watching day) means the biggest day.
  selectedDay: string | null
  onSelectDay: (day: string) => void
  onClose: () => void
}

function plural(count: number, one: string, many: string): string {
  return `${count} ${count === 1 ? one : many}`
}

// A watching day's step among the period's, 1 (lightest) to 4 (strongest): where its
// figure falls among every watching day's figures, sorted ascending, by the quarter
// points of the sorted list. Equal figures share a step and the largest figure is
// always in the strongest one (watching days calendar; design D7).
function stepThresholds(days: RecapWatchingDayDto[]) {
  const figures = days.map((day) => day.episodes).sort((a, b) => a - b)
  const n = figures.length
  return {
    t4: figures[Math.floor((3 * n) / 4)],
    t3: figures[Math.floor(n / 2)],
    t2: figures[Math.floor(n / 4)],
  }
}

function stepOf(figure: number, thresholds: ReturnType<typeof stepThresholds>): 1 | 2 | 3 | 4 {
  if (figure >= thresholds.t4) return 4
  if (figure >= thresholds.t3) return 3
  if (figure >= thresholds.t2) return 2
  return 1
}

function pad(value: number): string {
  return String(value).padStart(2, '0')
}

type CalendarMonth = { year: number; month: number; label: string; days: number; leading: number }

const SHORT_MONTH_NAMES = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec']

// Every month from the first watching day's through the last's, including a month
// between them that holds none.
function monthsOf(days: RecapWatchingDayDto[]): CalendarMonth[] {
  const [firstYear, firstMonth] = days[0].date.split('-').map(Number)
  const [lastYear, lastMonth] = days[days.length - 1].date.split('-').map(Number)
  const months: CalendarMonth[] = []
  for (let year = firstYear, month = firstMonth; year < lastYear || (year === lastYear && month <= lastMonth); ) {
    months.push({
      year,
      month,
      label: `${SHORT_MONTH_NAMES[month - 1]} ${year}`,
      days: new Date(Date.UTC(year, month, 0)).getUTCDate(),
      leading: isoWeekdayOf(`${year}-${pad(month)}-01`) - 1,
    })
    month += 1
    if (month > 12) {
      month = 1
      year += 1
    }
  }
  return months
}

// The recap's Watching days overlay (list-recaps "The watching days overlay" and
// "The watching days calendar"): four figures, then Top days or a Calendar. It is
// controlled: the recap page holds the tab and the chosen day as restorable state
// beside the open flag, so a round trip to an anime brings it back as it was.
export function WatchingDaysOverlay({
  title,
  watchingDays,
  items,
  tab,
  onTabChange,
  selectedDay,
  onSelectDay,
  onClose,
}: WatchingDaysOverlayProps) {
  const scrollRef = useRestorableScroll('watchingDays', 'vertical')
  const itemsById = useMemo(() => new Map(items.map((item) => [item.animeId, item])), [items])
  const dayByDate = useMemo(() => new Map(watchingDays.days.map((day) => [day.date, day])), [watchingDays])

  const { daysWatched, episodesPerDay, longestStreak, busiestWeekday, biggestBinge } = watchingDays
  const bingeItem = itemsById.get(biggestBinge.animeId)
  const bingeTitle = bingeItem ? pickDisplayTitle(bingeItem.title, bingeItem.englishTitle) : `Anime ${biggestBinge.animeId}`

  const tiles = [
    {
      label: 'Days watched',
      value: plural(daysWatched, 'day', 'days'),
      line: `${episodesPerDay} ${episodesPerDay === 1 ? 'episode' : 'episodes'} a day`,
    },
    {
      label: 'Longest streak',
      value: plural(longestStreak.days, 'day', 'days'),
      line: formatDayRange(longestStreak.start, longestStreak.end),
    },
    {
      label: 'Busiest weekday',
      value: ISO_WEEKDAY_NAMES[busiestWeekday.isoWeekday - 1],
      line: `${busiestWeekday.sharePercent}% of episodes`,
    },
    {
      label: 'Biggest binge',
      value: bingeTitle,
      line: `${plural(biggestBinge.episodes, 'episode', 'episodes')} · ${formatIsoDay(biggestBinge.date)}`,
      // The one tile that leads somewhere: the anime it names.
      to: `/anime/${biggestBinge.animeId}`,
    },
  ]

  return (
    <Modal onClose={onClose} labelledBy="watching-days-title" className="modal--watching-days" contentRef={scrollRef}>
      <div className="watching-days">
        <div className="watching-days__header">
          <h2 id="watching-days-title" className="watching-days__title">
            {title}
          </h2>
          <button type="button" className="watching-days__close" onClick={onClose} aria-label="Close watching days">
            <CloseIcon />
          </button>
        </div>

        <div className="watching-days__tiles">
          {tiles.map((tile) => {
            const content = (
              <>
                <span className="watching-days__tile-label">{tile.label}</span>
                <span className="watching-days__tile-value" title={tile.value}>
                  {tile.value}
                </span>
                <span className="watching-days__tile-line">{tile.line}</span>
              </>
            )
            return tile.to ? (
              <Link key={tile.label} to={tile.to} className="watching-days__tile watching-days__tile--link">
                {content}
              </Link>
            ) : (
              <div key={tile.label} className="watching-days__tile">
                {content}
              </div>
            )
          })}
        </div>

        <div className="watching-days__tabs" role="group" aria-label="Watching days view">
          <button
            type="button"
            className={tab === 'top' ? 'recap-page__tab recap-page__tab--active' : 'recap-page__tab'}
            aria-pressed={tab === 'top'}
            onClick={() => onTabChange('top')}
          >
            Top days
          </button>
          <button
            type="button"
            className={tab === 'calendar' ? 'recap-page__tab recap-page__tab--active' : 'recap-page__tab'}
            aria-pressed={tab === 'calendar'}
            onClick={() => onTabChange('calendar')}
          >
            Calendar
          </button>
        </div>

        {tab === 'top' ? (
          <div className="watching-days__top">
            {watchingDays.topDays.map((date, index) => {
              const day = dayByDate.get(date)
              return day ? <WatchingDayBlock key={date} day={day} place={index + 1} itemsById={itemsById} /> : null
            })}
          </div>
        ) : (
          <WatchingDaysCalendar
            watchingDays={watchingDays}
            dayByDate={dayByDate}
            itemsById={itemsById}
            selectedDay={selectedDay}
            onSelectDay={onSelectDay}
          />
        )}
      </div>
    </Modal>
  )
}

function WatchingDaysCalendar({
  watchingDays,
  dayByDate,
  itemsById,
  selectedDay,
  onSelectDay,
}: {
  watchingDays: RecapWatchingDaysDto
  dayByDate: Map<string, RecapWatchingDayDto>
  itemsById: Map<number, RecapRowDto>
  selectedDay: string | null
  onSelectDay: (day: string) => void
}) {
  const { days, topDays } = watchingDays
  const months = useMemo(() => monthsOf(days), [days])
  const thresholds = useMemo(() => stepThresholds(days), [days])
  const containerRef = useRef<HTMLDivElement>(null)
  const focusAfterSelectRef = useRef(false)

  // A restored day that is no longer a watching day falls back to the biggest day.
  const effectiveDay = selectedDay !== null && dayByDate.has(selectedDay) ? selectedDay : topDays[0]
  const selected = dayByDate.get(effectiveDay)

  // An arrow key selects the neighbour and then moves focus to it, once the roving
  // tab stop has moved there.
  useEffect(() => {
    if (!focusAfterSelectRef.current) return
    focusAfterSelectRef.current = false
    containerRef.current?.querySelector<HTMLElement>(`[data-day="${effectiveDay}"]`)?.focus()
  }, [effectiveDay])

  function handleKeyDown(event: KeyboardEvent<HTMLButtonElement>, date: string) {
    const index = days.findIndex((day) => day.date === date)
    let target = -1
    if (event.key === 'ArrowLeft') target = Math.max(0, index - 1)
    else if (event.key === 'ArrowRight') target = Math.min(days.length - 1, index + 1)
    else if (event.key === 'Home') target = 0
    else if (event.key === 'End') target = days.length - 1
    if (target < 0) return
    event.preventDefault()
    focusAfterSelectRef.current = true
    if (days[target].date === effectiveDay) {
      focusAfterSelectRef.current = false
      return
    }
    onSelectDay(days[target].date)
  }

  return (
    <div className="watching-calendar" ref={containerRef}>
      <div className="watching-calendar__months">
        {months.map((month) => (
          <section key={month.label} className="watching-calendar__month" aria-label={month.label}>
            <h3 className="watching-calendar__month-label">{month.label}</h3>
            <div className="watching-calendar__grid">
              {['M', 'T', 'W', 'T', 'F', 'S', 'S'].map((letter, i) => (
                <span key={i} className="watching-calendar__weekday" aria-hidden="true">
                  {letter}
                </span>
              ))}
              {Array.from({ length: month.leading }, (_, i) => (
                <span key={`blank-${i}`} className="watching-calendar__blank" aria-hidden="true" />
              ))}
              {Array.from({ length: month.days }, (_, i) => {
                const date = `${month.year}-${pad(month.month)}-${pad(i + 1)}`
                const day = dayByDate.get(date)
                if (!day) return <span key={date} className="watching-calendar__day watching-calendar__day--empty" />
                const isSelected = date === effectiveDay
                return (
                  <button
                    key={date}
                    type="button"
                    className={`watching-calendar__day watching-calendar__day--step-${stepOf(day.episodes, thresholds)}${
                      isSelected ? ' watching-calendar__day--selected' : ''
                    }`}
                    data-day={date}
                    tabIndex={isSelected ? 0 : -1}
                    aria-pressed={isSelected}
                    aria-label={`${formatIsoDayLong(date)}, ${plural(day.episodes, 'episode', 'episodes')}`}
                    title={`${formatIsoDayShortWeekday(date)} · ${plural(day.episodes, 'episode', 'episodes')}`}
                    onClick={() => onSelectDay(date)}
                    onKeyDown={(event) => handleKeyDown(event, date)}
                  />
                )
              })}
            </div>
          </section>
        ))}
      </div>
      {selected && <WatchingDayBlock day={selected} itemsById={itemsById} />}
    </div>
  )
}

function CloseIcon() {
  return (
    <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden="true">
      <line x1="6" y1="6" x2="18" y2="18" />
      <line x1="18" y1="6" x2="6" y2="18" />
    </svg>
  )
}
