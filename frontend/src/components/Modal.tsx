import { useEffect, type MouseEvent, type ReactNode } from 'react'
import './Modal.css'

type ModalProps = {
  onClose: () => void
  children: ReactNode
  labelledBy?: string
}

// Generic "opens on top of the page, closes on Esc/click-outside" overlay —
// shared by the entry editor and any future overlay (edit-history, top-anime
// selection) that needs the same open/close behavior.
export function Modal({ onClose, children, labelledBy }: ModalProps) {
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
      <div className="modal" role="dialog" aria-modal="true" aria-labelledby={labelledBy}>
        {children}
      </div>
    </div>
  )
}
