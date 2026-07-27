import { useRef, useState } from 'react'
import { Modal } from './Modal.tsx'
import { putTopAnimeOrder } from '../api/client.ts'
import type { TopAnimeMediaType, TopAnimeSectionDto, TopAnimeTierDto } from '../api/types.ts'
import './TopAnimeSelectionOverlay.css'

type TopAnimeSelectionOverlayProps = {
  section: TopAnimeSectionDto
  mediaType: TopAnimeMediaType
  mediaTypeLabel: string
  onClose: () => void
  onSaved: () => void
}

type EditableTier = { score: number; members: TopAnimeTierDto['members']; includedCount: number }

// Lets the user reorder every tier of the current "My top anime" list.
// Score still dominates (each tier is edited independently), but within a
// tier the order is fully up to the user, and a tier that doesn't fully fit
// shows a cut line after includedCount: moving a member above it adds that
// anime to the top list, displacing whichever member drops below.
export function TopAnimeSelectionOverlay({ section, mediaType, mediaTypeLabel, onClose, onSaved }: TopAnimeSelectionOverlayProps) {
  const [tiers, setTiers] = useState<EditableTier[]>(
    section.tiers.map((tier) => ({ score: tier.score, members: [...tier.members], includedCount: tier.includedCount })),
  )
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const dragRef = useRef<{ tierIndex: number; index: number } | null>(null)

  function moveMember(tierIndex: number, fromIndex: number, toIndex: number) {
    setTiers((prev) => {
      const tier = prev[tierIndex]
      if (toIndex < 0 || toIndex >= tier.members.length || fromIndex === toIndex) return prev
      const next = prev.map((t, i) => (i === tierIndex ? { ...t, members: [...t.members] } : t))
      const [moved] = next[tierIndex].members.splice(fromIndex, 1)
      next[tierIndex].members.splice(toIndex, 0, moved)
      return next
    })
  }

  async function handleSave() {
    setSaving(true)
    setError(null)
    try {
      await putTopAnimeOrder(
        mediaType,
        tiers.map((tier) => ({ score: tier.score, animeIds: tier.members.map((m) => m.animeId) })),
      )
      onSaved()
    } catch {
      setError('Could not save the order. Please try again.')
      setSaving(false)
    }
  }

  return (
    <Modal onClose={onClose} labelledBy="top-anime-selection-title">
      <div className="top-anime-selection">
        <h2 id="top-anime-selection-title" className="top-anime-selection__title">
          Edit top anime order
        </h2>
        <p className="top-anime-selection__hint">
          Editing order for: {mediaTypeLabel}. Drag a row or use the arrows to reorder within its score tier
          {tiers.some((t) => t.includedCount < t.members.length) ? '; the line marks the cutoff for the top list.' : '.'}
        </p>

        <div className="top-anime-selection__tiers">
          {tiers.map((tier, tierIndex) => (
            <div key={tier.score} className="top-anime-selection__tier">
              <h3 className="top-anime-selection__tier-title">Score {tier.score}</h3>
              <ul className="top-anime-selection__list">
                {tier.members.map((member, index) => (
                  <li key={member.animeId}>
                    <div
                      className="top-anime-selection__row"
                      draggable
                      onDragStart={() => {
                        dragRef.current = { tierIndex, index }
                      }}
                      onDragOver={(event) => event.preventDefault()}
                      onDrop={(event) => {
                        event.preventDefault()
                        const drag = dragRef.current
                        dragRef.current = null
                        if (!drag || drag.tierIndex !== tierIndex) return
                        moveMember(tierIndex, drag.index, index)
                      }}
                    >
                      {member.pictureUrl ? (
                        <img src={member.pictureUrl} alt="" className="top-anime-selection__picture" />
                      ) : (
                        <div
                          className="top-anime-selection__picture top-anime-selection__picture--placeholder"
                          aria-hidden="true"
                        />
                      )}
                      <span className="top-anime-selection__row-title">{member.title}</span>
                      <span className="top-anime-selection__row-buttons">
                        <button
                          type="button"
                          aria-label="Move up"
                          disabled={index === 0}
                          onClick={() => moveMember(tierIndex, index, index - 1)}
                        >
                          ↑
                        </button>
                        <button
                          type="button"
                          aria-label="Move down"
                          disabled={index === tier.members.length - 1}
                          onClick={() => moveMember(tierIndex, index, index + 1)}
                        >
                          ↓
                        </button>
                      </span>
                    </div>
                    {index === tier.includedCount - 1 && tier.includedCount < tier.members.length && (
                      <div className="top-anime-selection__cut-line" role="separator" aria-label="Top list cutoff" />
                    )}
                  </li>
                ))}
              </ul>
            </div>
          ))}
        </div>

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
