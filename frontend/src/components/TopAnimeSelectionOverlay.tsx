import { useState } from 'react'
import { Modal } from './Modal.tsx'
import { putTopAnimeSelection } from '../api/client.ts'
import type { TopAnimeSectionDto } from '../api/types.ts'
import './TopAnimeSelectionOverlay.css'

type TopAnimeSelectionOverlayProps = {
  section: TopAnimeSectionDto
  onClose: () => void
  onSaved: () => void
}

// Lets the user pick which tied next-highest-scored anime fill the remaining
// "My top anime" slots, overriding the deterministic (alphabetical) default.
// Selecting fewer than the available slots is fine — the backend fills any
// unselected slots from the same default, so the list still always reaches
// the minimum of 10 when possible.
export function TopAnimeSelectionOverlay({ section, onClose, onSaved }: TopAnimeSelectionOverlayProps) {
  const defaultChecked = section.candidates.slice(0, section.tieBreakSlots).map((c) => c.animeId)
  const [selected, setSelected] = useState<number[]>(
    section.selectedAnimeIds.length > 0 ? section.selectedAnimeIds : defaultChecked,
  )
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const atLimit = selected.length >= section.tieBreakSlots

  function toggle(animeId: number) {
    setSelected((prev) => {
      if (prev.includes(animeId)) return prev.filter((id) => id !== animeId)
      if (prev.length >= section.tieBreakSlots) return prev
      return [...prev, animeId]
    })
  }

  async function handleSave() {
    setSaving(true)
    setError(null)
    try {
      await putTopAnimeSelection(selected)
      onSaved()
    } catch {
      setError('Could not save the selection. Please try again.')
      setSaving(false)
    }
  }

  return (
    <Modal onClose={onClose} labelledBy="top-anime-selection-title">
      <div className="top-anime-selection">
        <h2 id="top-anime-selection-title" className="top-anime-selection__title">
          Choose your top anime
        </h2>
        <p className="top-anime-selection__hint">
          Pick up to {section.tieBreakSlots} of these tied anime to fill the remaining slots ({selected.length}/
          {section.tieBreakSlots} selected).
        </p>

        <ul className="top-anime-selection__list">
          {section.candidates.map((candidate) => {
            const checked = selected.includes(candidate.animeId)
            return (
              <li key={candidate.animeId} className="top-anime-selection__row">
                <label>
                  <input
                    type="checkbox"
                    checked={checked}
                    disabled={!checked && atLimit}
                    onChange={() => toggle(candidate.animeId)}
                  />
                  {candidate.pictureUrl ? (
                    <img src={candidate.pictureUrl} alt="" className="top-anime-selection__picture" />
                  ) : (
                    <div
                      className="top-anime-selection__picture top-anime-selection__picture--placeholder"
                      aria-hidden="true"
                    />
                  )}
                  <span className="top-anime-selection__row-title">{candidate.title}</span>
                </label>
              </li>
            )
          })}
        </ul>

        {error && <p className="top-anime-selection__error">{error}</p>}

        <div className="top-anime-selection__buttons">
          <button type="button" onClick={onClose} disabled={saving}>
            Cancel
          </button>
          <button type="button" className="top-anime-selection__save" onClick={handleSave} disabled={saving}>
            {saving ? 'Saving…' : 'Save'}
          </button>
        </div>
      </div>
    </Modal>
  )
}
