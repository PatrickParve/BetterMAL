import { useEffect, useRef, useState } from 'react'
import { useClickOutside } from '../../hooks/useClickOutside.ts'
import { UpdatesHistoryOverlay } from './UpdatesHistoryOverlay.tsx'
import { UpdateCard } from './UpdateCard.tsx'
import { useCappedCardHeight } from './useCappedCardHeight.ts'
import { useRecentUpdates } from './useRecentUpdates.ts'
import { hasUnseen, markSeen, readSeenMarker } from './updatesSeen.ts'
import './UpdatesMenu.css'

// The navbar's Updates control (navigation-and-search, anime-updates): a
// bell button opening a dropdown over the last 30 days of updates, capped to
// its newest cards on open (VISIBLE_CARD_CAP), with an unseen indicator and
// the History overlay reachable from its header.
export function UpdatesMenu() {
  const [open, setOpen] = useState(false)
  const [historyOpen, setHistoryOpen] = useState(false)
  const [seenMarker, setSeenMarker] = useState<string | null>(readSeenMarker)
  const { items, refresh } = useRecentUpdates()

  const containerRef = useRef<HTMLDivElement>(null)
  const buttonRef = useRef<HTMLButtonElement>(null)

  const { listRef, maxHeight: listMaxHeight } = useCappedCardHeight(items.length)
  const unseen = hasUnseen(items, seenMarker)

  useClickOutside(containerRef, () => setOpen(false))

  // Escape returns focus to the button, the same as the search type-ahead
  // this dropdown otherwise follows (design.md D8).
  useEffect(() => {
    if (!open) return
    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        setOpen(false)
        buttonRef.current?.focus()
      }
    }
    document.addEventListener('keydown', handleKeyDown)
    return () => document.removeEventListener('keydown', handleKeyDown)
  }, [open])

  function handleToggle() {
    if (open) {
      setOpen(false)
      return
    }
    setOpen(true)
    // Marked seen only once the in-flight fetch resolves, so opening
    // mid-load can't mark a set seen that hasn't arrived yet (design.md D2).
    refresh().then((data) => {
      if (!data) return
      setSeenMarker(markSeen(data))
    })
  }

  function closeDropdown() {
    setOpen(false)
  }

  // Rendered from this component rather than from inside the dropdown
  // element, so closing the dropdown to open the history doesn't unmount it
  // (design.md D8).
  function openHistory() {
    setOpen(false)
    setHistoryOpen(true)
  }

  const accessibleLabel = unseen ? 'Updates, new' : 'Updates'

  return (
    <div className="updates-menu" ref={containerRef}>
      <button
        type="button"
        ref={buttonRef}
        className="updates-menu__button"
        aria-expanded={open}
        aria-label={accessibleLabel}
        onClick={handleToggle}
      >
        <BellIcon />
        {unseen && <span className="updates-menu__dot" aria-hidden="true" />}
      </button>

      {open && (
        <div className="updates-menu__dropdown">
          <div className="updates-menu__header">
            <span className="updates-menu__title">Updates</span>
            <button type="button" className="updates-menu__history-button" onClick={openHistory}>
              History
            </button>
          </div>

          {items.length === 0 ? (
            <p className="updates-menu__empty">No recent updates.</p>
          ) : (
            <ul
              className="updates-menu__list"
              ref={listRef}
              style={listMaxHeight !== undefined ? { maxHeight: listMaxHeight } : undefined}
            >
              {items.map((item) => (
                <li key={item.id}>
                  <UpdateCard item={item} variant="menu" onNavigate={closeDropdown} />
                </li>
              ))}
            </ul>
          )}
        </div>
      )}

      {historyOpen && <UpdatesHistoryOverlay onClose={() => setHistoryOpen(false)} />}
    </div>
  )
}

function BellIcon() {
  return (
    <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d="M6 10a6 6 0 1 1 12 0c0 3.3 0.9 5 2 6H4c1.1-1 2-2.7 2-6Z" />
      <path d="M10 20a2 2 0 0 0 4 0" />
    </svg>
  )
}
