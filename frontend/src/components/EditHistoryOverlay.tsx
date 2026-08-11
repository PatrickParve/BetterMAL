import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { Modal } from './Modal.tsx'
import { TruncatedTitle } from './TruncatedTitle.tsx'
import { getActivityHistory } from '../api/client.ts'
import type { ActivityFeedItemDto } from '../api/types.ts'
import { CHANGE_TYPE_LABELS, formatTimestamp, pickDisplayTitle } from '../utils/anime.ts'
import './EditHistoryOverlay.css'

type EditHistoryOverlayProps = {
  onClose: () => void
}

// Full edit history — everything the "Latest updates" box trims down to its
// most-recent handful. Fetched fresh each time the overlay opens.
export function EditHistoryOverlay({ onClose }: EditHistoryOverlayProps) {
  const [history, setHistory] = useState<ActivityFeedItemDto[]>([])
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    getActivityHistory()
      .then(setHistory)
      .catch(() => {
        // Overlay just stays empty; nothing else to react to here.
      })
      .finally(() => setLoading(false))
  }, [])

  return (
    <Modal onClose={onClose} labelledBy="edit-history-title" className="modal--wide">
      <div className="edit-history">
        <h2 id="edit-history-title" className="edit-history__title">
          Full edit history
        </h2>

        {loading ? (
          <p className="edit-history__empty">Loading…</p>
        ) : history.length === 0 ? (
          <p className="edit-history__empty">No activity yet.</p>
        ) : (
          <ul className="edit-history__list scroll-y">
            {history.map((item) => (
              <li key={item.id} className="edit-history__row">
                <Link to={`/anime/${item.animeId}`} className="edit-history__link" onClick={onClose}>
                  {item.pictureUrl ? (
                    <img src={item.pictureUrl} alt="" className="edit-history__picture" />
                  ) : (
                    <div className="edit-history__picture edit-history__picture--placeholder" aria-hidden="true" />
                  )}
                  <span className="edit-history__info">
                    <TruncatedTitle
                      title={pickDisplayTitle(item.animeTitle, item.animeEnglishTitle)}
                      lines={2}
                      className="edit-history__row-title"
                    />
                    <span className="edit-history__detail">
                      {CHANGE_TYPE_LABELS[item.changeType] ?? item.changeType}
                      {item.changeDetail ? ` — ${item.changeDetail}` : ''}
                    </span>
                  </span>
                </Link>
                <span className="edit-history__timestamp">{formatTimestamp(item.timestamp)}</span>
              </li>
            ))}
          </ul>
        )}

        <div className="edit-history__buttons">
          <button type="button" onClick={onClose}>
            Close
          </button>
        </div>
      </div>
    </Modal>
  )
}
