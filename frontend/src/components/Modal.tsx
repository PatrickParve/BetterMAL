import { useEffect, useRef, type MouseEvent, type ReactNode } from 'react'
import { useLocation } from 'react-router-dom'
import { useScrollLock } from '../hooks/useScrollLock.ts'
import './Modal.css'

type ModalProps = {
  onClose: () => void
  children: ReactNode
  labelledBy?: string
  className?: string
}

// Generic "opens on top of the page, closes on Esc/click-outside" overlay —
// shared by the entry editor and any future overlay (edit-history, top-anime
// selection) that needs the same open/close behavior.
export function Modal({ onClose, children, labelledBy, className }: ModalProps) {
  useScrollLock()

  const onCloseRef = useRef(onClose)
  onCloseRef.current = onClose

  useEffect(() => {
    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') onCloseRef.current()
    }
    document.addEventListener('keydown', handleKeyDown)
    return () => document.removeEventListener('keydown', handleKeyDown)
  }, [])

  // Closing on navigation is a property of this one wrapper rather than of
  // any one overlay (D4): every overlay in the app — the three app-root
  // ones (entry editor, ranking editor, completion prompt) and every
  // page-local one — passes through here, so this one effect covers all of
  // them, including a page-local overlay that survives a same-route id
  // change (`/anime/1` → `/anime/2`) and any overlay added later. It closes
  // through `onClose()` rather than by unmounting so dismissal work still
  // runs — `AnimeRankOverlay` flushes a pending arrangement in its `onClose`,
  // and `CompletionScoreOverlay` reports its dismissal via `onClose(null)`.
  //
  // Keyed on `pathname` alone, not the whole location (D5): filters, sort,
  // scope, page number and the search query all live in the query string as
  // a page's own view state, not a different page, and shouldn't close an
  // open overlay.
  const { pathname } = useLocation()
  const mountedPathnameRef = useRef(pathname)

  useEffect(() => {
    if (pathname !== mountedPathnameRef.current) onCloseRef.current()
  }, [pathname])

  function handleBackdropClick(event: MouseEvent<HTMLDivElement>) {
    if (event.target === event.currentTarget) onClose()
  }

  return (
    <div className="modal-backdrop" onMouseDown={handleBackdropClick}>
      <div className={className ? `modal ${className}` : 'modal'} role="dialog" aria-modal="true" aria-labelledby={labelledBy}>
        {children}
      </div>
    </div>
  )
}
