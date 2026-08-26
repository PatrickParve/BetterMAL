import { useState } from 'react'
import { Link } from 'react-router-dom'
import type { AnimeUpdateDto, AnimeUpdateKind } from '../api/types.ts'
import { pickDisplayTitle } from '../utils/anime.ts'
import { TruncatedTitle } from './TruncatedTitle.tsx'
import { UpdatesHistoryOverlay } from './UpdatesHistoryOverlay.tsx'
import './UpdatesSection.css'

type UpdatesSectionProps = {
  items: AnimeUpdateDto[]
}

// Exported so UpdatesHistoryOverlay renders the same headline text — the
// two surfaces must never describe the same update differently.
export function formatShortDate(value: string): string {
  return new Date(value).toLocaleDateString(undefined, { day: 'numeric', month: 'short' })
}

function formatTime(value: string): string {
  return value.slice(0, 5)
}

function formatSlot(day: string, time: string): string {
  return `${day}s ${formatTime(time)}`
}

// A schedule-change card's headline carries the movement itself rather than
// repeating the current-value line beneath it (design.md D13); a
// becoming-known kind gets a short label instead, since the value it
// revealed is exactly what that line beneath already shows.
function describeKind(kind: AnimeUpdateKind, item: AnimeUpdateDto): string | null {
  switch (kind) {
    case 'Announced':
      return 'Announced'
    case 'EpisodeCountReleased':
      return 'Episode count revealed'
    case 'StartDateReleased':
      return 'Premiere date revealed'
    case 'StartDateChanged': {
      if (!item.airedFrom || !item.previousStartDate) return null
      const delayed = new Date(item.airedFrom) > new Date(item.previousStartDate)
      const verb = delayed ? 'Delayed' : 'Moved up'
      return `${verb} to ${formatShortDate(item.airedFrom)}, was ${formatShortDate(item.previousStartDate)}`
    }
    case 'BroadcastSlotChanged': {
      if (!item.currentBroadcastDayOfWeek || !item.currentBroadcastTime || !item.previousBroadcastDayOfWeek || !item.previousBroadcastTime)
        return null
      return `Moved to ${formatSlot(item.currentBroadcastDayOfWeek, item.currentBroadcastTime)}, was ${formatSlot(item.previousBroadcastDayOfWeek, item.previousBroadcastTime)}`
    }
    case 'EpisodesMoved':
      return item.movedEpisode !== null && item.newEpisodeDate
        ? `Ep ${item.movedEpisode} moved to ${formatShortDate(item.newEpisodeDate)}`
        : null
    default:
      return null
  }
}

export function buildHeadline(item: AnimeUpdateDto): string {
  return item.kinds
    .map((kind) => describeKind(kind, item))
    .filter((fragment): fragment is string => fragment !== null)
    .join(' · ')
}

// Home page "Updates" section (anime-updates spec): a full-width row of
// horizontal cards, newest leftmost, scrolling rather than wrapping when it
// overflows. A History button opens the full log and stays available even
// when the row itself has nothing to show.
export function UpdatesSection({ items }: UpdatesSectionProps) {
  const [showHistory, setShowHistory] = useState(false)

  return (
    <section className="dashboard-section updates-section">
      <div className="updates-section__header">
        <h2>Updates</h2>
        <button type="button" className="updates-section__history-button" onClick={() => setShowHistory(true)}>
          History
        </button>
      </div>

      {items.length === 0 ? (
        <p className="updates-section__empty">No updates in the last 30 days.</p>
      ) : (
        <ul className="updates-section__row">
          {items.map((item) => (
            <li key={item.id}>
              <Link to={`/anime/${item.animeId}`} className="updates-section__card">
                {item.pictureUrl ? (
                  <img src={item.pictureUrl} alt="" className="updates-section__thumb" />
                ) : (
                  <div className="updates-section__thumb updates-section__thumb--placeholder" aria-hidden="true" />
                )}
                <span className="updates-section__text">
                  <TruncatedTitle
                    title={pickDisplayTitle(item.title, item.englishTitle)}
                    lines={1}
                    className="updates-section__title"
                  />
                  <span className="updates-section__headline">{buildHeadline(item)}</span>
                  {(item.totalEpisodes !== null || item.airedFrom !== null) && (
                    <span className="updates-section__meta">
                      {item.totalEpisodes !== null && <span>{item.totalEpisodes} episodes</span>}
                      {item.airedFrom !== null && <span>{formatShortDate(item.airedFrom)}</span>}
                    </span>
                  )}
                  <span className="updates-section__reason">{item.reason}</span>
                </span>
              </Link>
            </li>
          ))}
        </ul>
      )}

      {showHistory && <UpdatesHistoryOverlay onClose={() => setShowHistory(false)} />}
    </section>
  )
}
