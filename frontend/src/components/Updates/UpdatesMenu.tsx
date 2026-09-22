import { useEffect, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import { useClickOutside } from '../../hooks/useClickOutside.ts'
import { UpdatesHistoryOverlay } from './UpdatesHistoryOverlay.tsx'
import { UpdatesDropdown } from './UpdatesDropdown.tsx'
import { useRecentUpdates } from './useRecentUpdates.ts'
import { useUpdatesSeen } from './updatesSeenStore.ts'
import './UpdatesMenu.css'

// The navbar's Updates control (navigation-and-search, anime-updates): a
// bell button opening a dropdown over the last 30 days of updates, with an
// unseen indicator derived from the server-held seen flags and the History
// overlay reachable from its header. Opening the dropdown does not itself
// mark anything seen — an update becomes seen only once its card has
// actually been looked at, inside UpdatesDropdown (store-seen-updates-on-server
// design.md D6/D10).
export function UpdatesMenu() {
  const [open, setOpen] = useState(false)
  const [historyOpen, setHistoryOpen] = useState(false)
  const { items, refresh } = useRecentUpdates()
  const { isSeen } = useUpdatesSeen()

  const containerRef = useRef<HTMLDivElement>(null)
  const buttonRef = useRef<HTMLButtonElement>(null)

  const unseen = items.some((item) => !isSeen(item))

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
    refresh()
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
        className={'updates-menu__button' + (open || historyOpen ? ' updates-menu__button--open' : '')}
        aria-expanded={open}
        aria-label={accessibleLabel}
        onClick={handleToggle}
      >
        <BellIcon />
        {unseen && <span className="navbar__status-dot" aria-hidden="true" />}
      </button>

      {open && <UpdatesDropdown items={items} onNavigate={closeDropdown} onOpenHistory={openHistory} />}

      {/* Portaled to the document body (design D2): the navbar becomes
          sticky (Navbar.css), which makes it a stacking context, and its
          hidden state is applied with a transform, which would make it the
          containing block for this overlay's fixed-position backdrop —
          trapping it inside the navbar's own bounds and z-index instead of
          covering the whole window. React events still bubble through the
          component tree from a portaled node, so nothing here loses them. */}
      {historyOpen && createPortal(<UpdatesHistoryOverlay onClose={() => setHistoryOpen(false)} />, document.body)}
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
