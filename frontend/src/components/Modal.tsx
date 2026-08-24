import { useEffect, type MouseEvent, type ReactNode } from 'react'
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

  useEffect(() => {
    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') onClose()
    }
    document.addEventListener('keydown', handleKeyDown)
    return () => document.removeEventListener('keydown', handleKeyDown)
  }, [onClose])

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
