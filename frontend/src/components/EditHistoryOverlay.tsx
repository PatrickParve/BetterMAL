import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { LoadingNotice } from './LoadingNotice.tsx'
import { Modal } from './Modal.tsx'
import { RowPicture } from './RowPicture.tsx'
import { TruncatedTitle } from './TruncatedTitle.tsx'
import { getActivityHistory } from '../api/client.ts'
import type { ActivityFeedItemDto } from '../api/types.ts'
import { formatTimestamp, pickDisplayTitle } from '../utils/anime.ts'
import './EditHistoryOverlay.css'

type EditHistoryOverlayProps = {
  onClose: () => void
}

// A row's local calendar day in the same YYYY-MM-DD shape an
// `<input type="date">` value uses, so a date bound compares lexically
// against it as an inclusive whole day in the viewer's own timezone.
function toLocalDateString(timestamp: string): string {
  return new Date(timestamp).toLocaleDateString('en-CA')
}

// Full edit history — everything the "Latest updates" box trims down to its
// most-recent handful. Fetched fresh each time the overlay opens; the title
// search and date range then filter that one fetch locally, so neither
// triggers a refetch.
export function EditHistoryOverlay({ onClose }: EditHistoryOverlayProps) {
  const [history, setHistory] = useState<ActivityFeedItemDto[]>([])
  const [loading, setLoading] = useState(true)
  const [search, setSearch] = useState('')
  const [fromDate, setFromDate] = useState('')
  const [toDate, setToDate] = useState('')

  useEffect(() => {
    getActivityHistory()
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
        const matchesTitle = item.animeTitle.toLowerCase().includes(query)
        const matchesEnglishTitle = item.animeEnglishTitle?.toLowerCase().includes(query) ?? false
        if (!matchesTitle && !matchesEnglishTitle) return false
      }

      const day = toLocalDateString(item.timestamp)
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
    <Modal onClose={onClose} labelledBy="edit-history-title" className="modal--wide">
      <div className="edit-history">
        <div className="edit-history__header">
          <h2 id="edit-history-title" className="edit-history__title">
            Full edit history
          </h2>
          <button type="button" className="edit-history__close" aria-label="Close history" onClick={onClose}>
            <CloseIcon />
          </button>
        </div>

        <div className="edit-history__filters">
          <input
            type="text"
            className="edit-history__search"
            placeholder="Search by title"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            aria-label="Search by anime title"
          />
          <div className="edit-history__date-range">
            <input
              type="date"
              className="edit-history__date"
              value={fromDate}
              onChange={(event) => setFromDate(event.target.value)}
              aria-label="From date"
            />
            <span className="edit-history__date-separator" aria-hidden="true">
              –
            </span>
            <input
              type="date"
              className="edit-history__date"
              value={toDate}
              onChange={(event) => setToDate(event.target.value)}
              aria-label="To date"
            />
          </div>
          {hasActiveFilter && (
            <button type="button" className="edit-history__clear" onClick={clearFilters}>
              Clear
            </button>
          )}
        </div>

        {loading ? (
          <LoadingNotice className="edit-history__empty" />
        ) : history.length === 0 ? (
          <p className="edit-history__empty">No activity yet.</p>
        ) : filteredHistory.length === 0 ? (
          <p className="edit-history__empty">No history matches these filters.</p>
        ) : (
          <div className="edit-history__list-frame">
            <ul className="edit-history__list scroll-hidden">
              {filteredHistory.map((item) => (
                <li key={item.id} className="edit-history__row">
                  <Link to={`/anime/${item.animeId}`} className="edit-history__link" onClick={onClose}>
                    <RowPicture src={item.pictureUrl} className="edit-history__picture" />
                    <span className="edit-history__info">
                      <TruncatedTitle
                        title={pickDisplayTitle(item.animeTitle, item.animeEnglishTitle)}
                        lines={2}
                        className="edit-history__row-title"
                      />
                      <span className="edit-history__detail">{item.summary}</span>
                    </span>
                  </Link>
                  <span className="edit-history__timestamp">{formatTimestamp(item.timestamp)}</span>
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
