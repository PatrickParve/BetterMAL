import { useState } from 'react'
import { Modal } from './Modal.tsx'
import { isSeriesTitleAcceptable } from '../utils/anime.ts'
import './SeriesTitlePickerOverlay.css'

type SeriesTitlePickerOverlayProps = {
  offeredTitles: string[]
  current: string
  onPick: (title: string) => void
  onClose: () => void
}

// series-identity: every main-line member's title/English title as a
// clickable list, plus a free-text field for a trimmed title. The D8
// contiguous-trim rule is mirrored here only to disable the confirm button
// and explain a refusal before it reaches the server, which remains the
// actual authority (design D8).
export function SeriesTitlePickerOverlay({ offeredTitles, current, onPick, onClose }: SeriesTitlePickerOverlayProps) {
  const [value, setValue] = useState(current)
  const trimmed = value.trim()
  const acceptable = trimmed.length > 0 && isSeriesTitleAcceptable(trimmed, offeredTitles)

  function handleConfirm() {
    if (!acceptable) return
    onPick(trimmed)
    onClose()
  }

  return (
    <Modal onClose={onClose} labelledBy="series-title-picker-overlay-title">
      <div className="series-title-picker-overlay">
        <h2 id="series-title-picker-overlay-title" className="series-title-picker-overlay__title">
          Choose title
        </h2>

        <ul className="series-title-picker-overlay__list">
          {offeredTitles.map((title) => (
            <li key={title}>
              <button
                type="button"
                className={
                  title === value
                    ? 'series-title-picker-overlay__option series-title-picker-overlay__option--selected'
                    : 'series-title-picker-overlay__option'
                }
                onClick={() => setValue(title)}
              >
                {title}
              </button>
            </li>
          ))}
        </ul>

        <label className="series-title-picker-overlay__field">
          Custom (must be a contiguous piece of one of the titles above)
          <input type="text" value={value} onChange={(e) => setValue(e.target.value)} />
        </label>

        {!acceptable && trimmed.length > 0 && (
          <p className="series-title-picker-overlay__error">
            That title isn't an unbroken piece of any offered title.
          </p>
        )}

        <div className="series-title-picker-overlay__buttons">
          <button type="button" onClick={onClose}>
            Cancel
          </button>
          <button
            type="button"
            className="series-title-picker-overlay__confirm"
            onClick={handleConfirm}
            disabled={!acceptable}
          >
            Save
          </button>
        </div>
      </div>
    </Modal>
  )
}
