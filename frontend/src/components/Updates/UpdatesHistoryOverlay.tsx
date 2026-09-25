import { useCallback, useEffect, useMemo, useState } from 'react'
import { LoadingNotice } from '../LoadingNotice.tsx'
import { Modal } from '../Modal.tsx'
import { UpdateCard } from './UpdateCard.tsx'
import { useUpdateLook } from './useUpdateLook.ts'
import { useSeenTracking } from './useSeenTracking.ts'
import { useUpdatesSeen, flushSeenReports } from './updatesSeenStore.ts'
import { getUpdatesHistory } from '../../api/client.ts'
import type { AnimeUpdateDto } from '../../api/types.ts'
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
// one fetch locally. Each row renders through the shared UpdateCard (design.md
// D6) rather than its own markup, so the history reads identically to the
// navbar dropdown.
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

  // The list's own node, tracked as state via a callback ref rather than a
  // plain useRef read in an effect (the same reason useCappedCardHeight
  // uses one, and the reason this overlay keeps its own copy rather than
  // reusing that hook now that it no longer caps the list's height): the
  // <ul> mounts after the items load, so a ref read in an effect keyed on
  // the item count would see nothing. useSeenTracking needs this node to
  // observe the list.
  const [listNode, setListNode] = useState<HTMLUListElement | null>(null)
  const listRef = useCallback((node: HTMLUListElement | null) => setListNode(node), [])

  // Looking at History's cards counts on the same terms as the dropdown's,
  // through the same shared store (store-seen-updates-on-server design.md
  // D6/D7). A search or date filter change re-narrows filteredHistory,
  // which the tracker and the look both re-evaluate against.
  const { isSeen, reportSeen } = useUpdatesSeen()
  const newIds = useUpdateLook(filteredHistory, isSeen)
  useSeenTracking(listNode, filteredHistory, isSeen, reportSeen)

  useEffect(() => {
    return () => flushSeenReports()
  }, [])

  return (
    <Modal onClose={onClose} labelledBy="updates-history-title" className="modal--wide modal--column">
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
          <LoadingNotice className="updates-history__empty" />
        ) : history.length === 0 ? (
          <p className="updates-history__empty">No updates recorded yet.</p>
        ) : filteredHistory.length === 0 ? (
          <p className="updates-history__empty">No updates match these filters.</p>
        ) : (
          <div className="updates-history__list-frame">
            <ul className="updates-history__list" ref={listRef}>
              {filteredHistory.map((item) => (
                <li key={item.id} data-update-id={item.id}>
                  <UpdateCard item={item} variant="history" onNavigate={onClose} isNew={newIds.has(item.id)} />
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
