import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { Modal } from './Modal.tsx'
import { TruncatedTitle } from './TruncatedTitle.tsx'
import { buildHeadline, formatShortDate } from './UpdatesSection.tsx'
import { getUpdatesHistory } from '../api/client.ts'
import type { AnimeUpdateDto } from '../api/types.ts'
import { formatTimestamp, pickDisplayTitle } from '../utils/anime.ts'
import './UpdatesHistoryOverlay.css'

type UpdatesHistoryOverlayProps = {
  onClose: () => void
}

// A row's local calendar day in the same YYYY-MM-DD shape an
// `<input type="date">` value uses, so a date bound compares lexically
// against it as an inclusive whole day in the viewer's own timezone —
// mirrors EditHistoryOverlay's own toLocalDateString.
function toLocalDateString(timestamp: string): string {
  return new Date(timestamp).toLocaleDateString('en-CA')
}

// The whole updates log (anime-updates spec, "The updates history is
// searchable and date-filterable") — modelled on EditHistoryOverlay: fetched
// fresh on open, then a title search and a from/to date range filter that
// one fetch locally.
export function UpdatesHistoryOverlay({ onClose }: UpdatesHistoryOverlayProps) {
  const [history, setHistory] = useState<AnimeUpdateDto[]>([])
  const [loading, setLoading] = useState(true)
  const [search, setSearch] = useState('')
  const [fromDate, setFromDate] = useState('')
  const [toDate, setToDate] = useState('')

  useEffect(() => {
    getUpdatesHistory()
      .then(setHistory)
      .catch(() => {
        // Overlay just stays empty; nothing else to react to here.
      })
      .finally(() => setLoading(false))
  }, [])

  const hasActiveFilter = search.trim().length > 0 || fromDate.length > 0 || toDate.length > 0

  const filteredHistory = useMemo(() => {
    const query = search.trim().toLowerCase()

    return history.filter((item) => {
      if (query) {
        const matchesTitle = item.title.toLowerCase().includes(query)
        const matchesEnglishTitle = item.englishTitle?.toLowerCase().includes(query) ?? false
        if (!matchesTitle && !matchesEnglishTitle) return false
      }

      const day = toLocalDateString(item.detectedAt)
      if (fromDate && day < fromDate) return false
      if (toDate && day > toDate) return false

      return true
    })
  }, [history, search, fromDate, toDate])

  function clearFilters() {
    setSearch('')
    setFromDate('')
    setToDate('')
  }

  return (
    <Modal onClose={onClose} labelledBy="updates-history-title" className="modal--wide">
      <div className="updates-history">
        <div className="updates-history__header">
          <h2 id="updates-history-title" className="updates-history__title">
            Updates history
          </h2>
          <button type="button" className="updates-history__close" aria-label="Close history" onClick={onClose}>
            <CloseIcon />
          </button>
        </div>

        <div className="updates-history__filters">
          <input
            type="text"
            className="updates-history__search"
            placeholder="Search by title"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            aria-label="Search by anime title"
          />
          <div className="updates-history__date-range">
            <input
              type="date"
              className="updates-history__date"
              value={fromDate}
              onChange={(event) => setFromDate(event.target.value)}
              aria-label="From date"
            />
            <span className="updates-history__date-separator" aria-hidden="true">
              –
            </span>
            <input
              type="date"
              className="updates-history__date"
              value={toDate}
              onChange={(event) => setToDate(event.target.value)}
              aria-label="To date"
            />
          </div>
          {hasActiveFilter && (
            <button type="button" className="updates-history__clear" onClick={clearFilters}>
              Clear
            </button>
          )}
        </div>

        {loading ? (
          <p className="updates-history__empty">Loading…</p>
        ) : history.length === 0 ? (
          <p className="updates-history__empty">No updates recorded yet.</p>
        ) : filteredHistory.length === 0 ? (
          <p className="updates-history__empty">No updates match these filters.</p>
        ) : (
          <div className="updates-history__list-frame">
            <ul className="updates-history__list scroll-y">
              {filteredHistory.map((item) => (
                <li key={item.id} className="updates-history__row">
                  <Link to={`/anime/${item.animeId}`} className="updates-history__link" onClick={onClose}>
                    {item.pictureUrl ? (
                      <img src={item.pictureUrl} alt="" className="updates-history__picture" />
                    ) : (
                      <div className="updates-history__picture updates-history__picture--placeholder" aria-hidden="true" />
                    )}
                    <span className="updates-history__info">
                      <TruncatedTitle
                        title={pickDisplayTitle(item.title, item.englishTitle)}
                        lines={2}
                        className="updates-history__row-title"
                      />
                      <span className="updates-history__detail">
                        <span className="updates-history__detail-text">{buildHeadline(item)}</span>
                        {item.totalEpisodes !== null && <span>{item.totalEpisodes} episodes</span>}
                        {item.airedFrom !== null && <span>{formatShortDate(item.airedFrom)}</span>}
                      </span>
                      <span className="updates-history__reason">{item.reason}</span>
                    </span>
                  </Link>
                  <span className="updates-history__timestamp">{formatTimestamp(item.detectedAt)}</span>
                </li>
              ))}
            </ul>
          </div>
        )}
      </div>
    </Modal>
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
