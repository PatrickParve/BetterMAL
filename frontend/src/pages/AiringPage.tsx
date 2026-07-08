import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { getAiringWeek } from '../api/client.ts'
import type { AiringWeekDto } from '../api/types.ts'
import { pickDisplayTitle } from '../utils/anime.ts'
import './AiringPage.css'

function isoDateWeeksFromToday(offsetWeeks: number): string {
  const date = new Date()
  date.setDate(date.getDate() + offsetWeeks * 7)
  return date.toISOString().slice(0, 10)
}

function formatWeekRange(weekStart: string, weekEnd: string): string {
  const fmt = (iso: string) => new Date(iso).toLocaleDateString(undefined, { month: 'short', day: 'numeric' })
  return `${fmt(weekStart)} – ${fmt(weekEnd)}`
}

// Weekly schedule of my-list anime, laid out as seven local day-columns.
// Navigation moves whole weeks at a time; "current" jumps back to today's week.
export function AiringPage() {
  const [offsetWeeks, setOffsetWeeks] = useState(0)
  const [week, setWeek] = useState<AiringWeekDto | null>(null)

  useEffect(() => {
    let cancelled = false
    getAiringWeek(isoDateWeeksFromToday(offsetWeeks))
      .then((data) => {
        if (!cancelled) setWeek(data)
      })
      .catch(() => {
        // Airing page just stays on the previous week; the user can retry via the nav controls.
      })
    return () => {
      cancelled = true
    }
  }, [offsetWeeks])

  const isEmptyWeek = week !== null && week.days.every((day) => day.slots.length === 0)

  return (
    <div className="airing-page">
      <div className="airing-page__header">
        <h1>Schedule</h1>
        <div className="airing-page__nav">
          <button type="button" onClick={() => setOffsetWeeks((value) => value - 1)} aria-label="Previous week">
            &lsaquo;
          </button>
          <button type="button" onClick={() => setOffsetWeeks(0)} disabled={offsetWeeks === 0}>
            current
          </button>
          <button type="button" onClick={() => setOffsetWeeks((value) => value + 1)} aria-label="Next week">
            &rsaquo;
          </button>
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
                <div className="airing-day__header">{day.dayOfWeek}</div>
                <ul className="airing-day__slots">
                  {day.slots.map((slot) => (
                    <li key={slot.animeId}>
                      <Link to={`/anime/${slot.animeId}`} className="airing-slot">
                        {slot.pictureUrl ? (
                          <img src={slot.pictureUrl} alt="" className="airing-slot__thumb" />
                        ) : (
                          <div className="airing-slot__thumb airing-slot__thumb--placeholder" aria-hidden="true" />
                        )}
                        <span className="airing-slot__info">
                          <span className="airing-slot__time">{slot.localTime}</span>
                          <span className="airing-slot__title">
                            {pickDisplayTitle(slot.title, slot.englishTitle)}
                            {slot.episodeNumber !== null && ` #${slot.episodeNumber}`}
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
