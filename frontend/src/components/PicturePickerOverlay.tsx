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
}

// Shared by the anime detail page and the series page (design D14): a grid
// of every picture option inside the existing Modal, click-to-choose with no
// separate confirm step, following TopAnimeSelectionOverlay's interaction
// and RelatedAnimeOverlay's grid/list structure. Picking closes the overlay
// immediately (spec anime-detail "A chosen picture applies immediately");
// the caller updates its own state and fires the save, optimistically or
// otherwise — this component does not wait on it.
export function PicturePickerOverlay({ title, options, current, onPick, onClose, note }: PicturePickerOverlayProps) {
  function handlePick(url: string) {
    onPick(url)
    onClose()
  }

  return (
    <Modal onClose={onClose} labelledBy="picture-picker-overlay-title" className="modal--wide">
      <div className="picture-picker-overlay">
        <h2 id="picture-picker-overlay-title" className="picture-picker-overlay__title">
          {title}
        </h2>

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

        <div className="picture-picker-overlay__buttons">
          <button type="button" onClick={onClose}>
            Close
          </button>
        </div>
      </div>
    </Modal>
  )
}
