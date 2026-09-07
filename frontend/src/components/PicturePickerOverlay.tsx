import { Modal } from './Modal.tsx'
import './PicturePickerOverlay.css'

type PicturePickerOverlayProps = {
  title: string
  options: string[]
  // The current selection is always shown, marked, even when a stored
  // choice has fallen out of the fetched option set — a choice MAL has
  // dropped stays visible and replaceable rather than disappearing
  // (design D9 / artwork-selection "A stored choice is never re-validated
  // away").
  current: string | null
  onPick: (url: string) => void
  onClose: () => void
  // A quiet note shown below the grid rather than an error — the series
  // picker's "N more members not yet fetched" (design D6).
  note?: string
  // Supplied only when a choice is stored — its presence is the client's
  // only signal that there is anything to clear (spec artwork-selection
  // "The picture picker").
  onClear?: () => void
}

// Shared by the anime detail page and the series page (design D14): a grid
// of every picture option inside the existing Modal, click-to-choose with no
// separate confirm step, following TopAnimeSelectionOverlay's interaction
// and RelatedAnimeOverlay's grid/list structure. Picking closes the overlay
// immediately (spec anime-detail "A chosen picture applies immediately");
// the caller updates its own state and fires the save, optimistically or
// otherwise — this component does not wait on it.
export function PicturePickerOverlay({
  title,
  options,
  current,
  onPick,
  onClose,
  note,
  onClear,
}: PicturePickerOverlayProps) {
  function handlePick(url: string) {
    onPick(url)
    onClose()
  }

  function handleClear() {
    onClear?.()
    onClose()
  }

  return (
    <Modal onClose={onClose} labelledBy="picture-picker-overlay-title" className="modal--wide">
      <div className="picture-picker-overlay">
        <div className="picture-picker-overlay__header">
          <h2 id="picture-picker-overlay-title" className="picture-picker-overlay__title">
            {title}
          </h2>
          <div className="picture-picker-overlay__actions">
            {onClear && (
              <button type="button" className="picture-picker-overlay__clear" onClick={handleClear}>
                Default
              </button>
            )}
            <button type="button" className="picture-picker-overlay__close" aria-label="Close" onClick={onClose}>
              <CloseIcon />
            </button>
          </div>
        </div>

        <div className="picture-picker-overlay__grid">
          {options.map((url) => (
            <button
              key={url}
              type="button"
              className={
                url === current
                  ? 'picture-picker-overlay__option picture-picker-overlay__option--selected'
                  : 'picture-picker-overlay__option'
              }
              onClick={() => handlePick(url)}
              aria-pressed={url === current}
            >
              <img src={url} alt="" className="picture-picker-overlay__image" />
              {url === current && <span className="picture-picker-overlay__badge">Current</span>}
            </button>
          ))}
        </div>

        {note && <p className="picture-picker-overlay__note">{note}</p>}
      </div>
    </Modal>
  )
}

function CloseIcon() {
  return (
    <svg viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden="true">
      <line x1="6" y1="6" x2="18" y2="18" />
      <line x1="18" y1="6" x2="6" y2="18" />
    </svg>
  )
}
