import { useEffect, useRef, useState, type PointerEvent } from 'react'
import { Modal } from './Modal.tsx'
import { putTopAnimeOrder } from '../api/client.ts'
import type { TopAnimeMediaType, TopAnimeSectionDto, TopAnimeTierDto } from '../api/types.ts'
import { pickDisplayTitle } from '../utils/anime.ts'
import './TopAnimeSelectionOverlay.css'

type TopAnimeSelectionOverlayProps = {
  section: TopAnimeSectionDto
  mediaType: TopAnimeMediaType
  mediaTypeLabel: string
  onClose: () => void
  onSaved: () => void
}

type EditableTier = { score: number; members: TopAnimeTierDto['members']; includedCount: number }
type Member = TopAnimeTierDto['members'][number]

const TOP_LIST_SIZE = 10
const DRAG_THRESHOLD = 4
const EDGE_ZONE = 48
const MIN_SCROLL_SPEED = 4
const MAX_SCROLL_SPEED = 18

function promoteBoundary(tier: EditableTier): number {
  return Math.min(tier.includedCount, TOP_LIST_SIZE)
}

// Captured on pointerdown; promoted to an ActiveDrag once the pointer clears
// DRAG_THRESHOLD, so a plain click on a row still behaves like a click.
type ArmedDrag = {
  tierIndex: number
  fromIndex: number
  startX: number
  startY: number
  offsetX: number
  offsetY: number
  width: number
}

type ActiveDrag = {
  tierIndex: number
  fromIndex: number
  targetIndex: number
  x: number
  y: number
  offsetX: number
  offsetY: number
  width: number
}

function resolveTargetIndex(clientX: number, clientY: number, tierIndex: number, fallback: number): number {
  const hit = document.elementFromPoint(clientX, clientY)
  const rowEl = hit instanceof Element ? hit.closest('[data-row-index]') : null
  if (!(rowEl instanceof HTMLElement)) return fallback
  if (Number(rowEl.dataset.tierIndex) !== tierIndex) return fallback
  return Number(rowEl.dataset.rowIndex)
}

function MemberPicture({ member }: { member: Member }) {
  return member.pictureUrl ? (
    <img src={member.pictureUrl} alt="" className="top-anime-selection__picture" />
  ) : (
    <div className="top-anime-selection__picture top-anime-selection__picture--placeholder" aria-hidden="true" />
  )
}

// Lets the user reorder every tier of the current "My top anime" list.
// Score still dominates (each tier is edited independently), but within a
// tier the order is fully up to the user, and a tier that doesn't fully fit
// shows a cut line after includedCount: moving a member above it adds that
// anime to the top list, displacing whichever member drops below. Rows
// before a tier's promote boundary (min(includedCount, 10)) get single-step
// arrows; rows at or past it get a single promote button. Reordering is
// also a pointer-driven drag with a floating preview and edge auto-scroll;
// the array itself is only mutated once, on release.
export function TopAnimeSelectionOverlay({ section, mediaType, mediaTypeLabel, onClose, onSaved }: TopAnimeSelectionOverlayProps) {
  const [tiers, setTiers] = useState<EditableTier[]>(
    section.tiers.map((tier) => ({ score: tier.score, members: [...tier.members], includedCount: tier.includedCount })),
  )
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [drag, setDrag] = useState<ActiveDrag | null>(null)

  const armRef = useRef<ArmedDrag | null>(null)
  const dragStateRef = useRef<ActiveDrag | null>(null)
  const pointerYRef = useRef(0)
  const rafRef = useRef<number | null>(null)
  const tiersRef = useRef<HTMLDivElement | null>(null)

  function updateDrag(next: ActiveDrag | null) {
    dragStateRef.current = next
    setDrag(next)
  }

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

  function stopAutoScroll() {
    if (rafRef.current != null) {
      cancelAnimationFrame(rafRef.current)
      rafRef.current = null
    }
  }

  function startAutoScroll() {
    if (rafRef.current != null) return
    const tick = () => {
      const container = tiersRef.current
      const current = dragStateRef.current
      if (!container || !current) {
        rafRef.current = null
        return
      }
      const rect = container.getBoundingClientRect()
      const y = pointerYRef.current
      let direction = 0
      let proximity = 0
      if (y < rect.top + EDGE_ZONE) {
        direction = -1
        proximity = (rect.top + EDGE_ZONE - y) / EDGE_ZONE
      } else if (y > rect.bottom - EDGE_ZONE) {
        direction = 1
        proximity = (y - (rect.bottom - EDGE_ZONE)) / EDGE_ZONE
      }
      if (direction !== 0) {
        const speed = MIN_SCROLL_SPEED + (MAX_SCROLL_SPEED - MIN_SCROLL_SPEED) * Math.min(proximity, 1)
        container.scrollTop += direction * speed
        const targetIndex = resolveTargetIndex(current.x, current.y, current.tierIndex, current.targetIndex)
        if (targetIndex !== current.targetIndex) updateDrag({ ...current, targetIndex })
      }
      rafRef.current = requestAnimationFrame(tick)
    }
    rafRef.current = requestAnimationFrame(tick)
  }

  // Escape is claimed here, in the capture phase, so a drag in progress
  // consumes it before Modal's bubble-phase listener can close the editor.
  useEffect(() => {
    function handleKeyDown(event: KeyboardEvent) {
      if (event.key !== 'Escape' || !dragStateRef.current) return
      event.stopPropagation()
      armRef.current = null
      stopAutoScroll()
      updateDrag(null)
    }
    document.addEventListener('keydown', handleKeyDown, { capture: true })
    return () => {
      document.removeEventListener('keydown', handleKeyDown, { capture: true })
      stopAutoScroll()
    }
  }, [])

  function handlePointerDown(event: PointerEvent<HTMLDivElement>, tierIndex: number, index: number) {
    if (event.button !== 0) return
    // Without this, holding and moving the mouse starts the browser's native
    // text-selection drag on whatever rows the pointer passes over.
    event.preventDefault()
    const rect = event.currentTarget.getBoundingClientRect()
    armRef.current = {
      tierIndex,
      fromIndex: index,
      startX: event.clientX,
      startY: event.clientY,
      offsetX: event.clientX - rect.left,
      offsetY: event.clientY - rect.top,
      width: rect.width,
    }
    event.currentTarget.setPointerCapture(event.pointerId)
  }

  function handlePointerMove(event: PointerEvent<HTMLDivElement>, tierIndex: number, index: number) {
    const current = dragStateRef.current
    if (current) {
      pointerYRef.current = event.clientY
      const targetIndex = resolveTargetIndex(event.clientX, event.clientY, current.tierIndex, current.targetIndex)
      updateDrag({ ...current, x: event.clientX, y: event.clientY, targetIndex })
      return
    }
    const armed = armRef.current
    if (!armed || armed.tierIndex !== tierIndex || armed.fromIndex !== index) return
    const dx = event.clientX - armed.startX
    const dy = event.clientY - armed.startY
    if (Math.hypot(dx, dy) < DRAG_THRESHOLD) return
    pointerYRef.current = event.clientY
    updateDrag({
      tierIndex: armed.tierIndex,
      fromIndex: armed.fromIndex,
      targetIndex: armed.fromIndex,
      x: event.clientX,
      y: event.clientY,
      offsetX: armed.offsetX,
      offsetY: armed.offsetY,
      width: armed.width,
    })
    startAutoScroll()
  }

  function handlePointerUp() {
    armRef.current = null
    const current = dragStateRef.current
    if (!current) return
    stopAutoScroll()
    if (current.targetIndex !== current.fromIndex) moveMember(current.tierIndex, current.fromIndex, current.targetIndex)
    updateDrag(null)
  }

  function handlePointerCancel() {
    armRef.current = null
    if (!dragStateRef.current) return
    stopAutoScroll()
    updateDrag(null)
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
          Editing order for: {mediaTypeLabel}. Drag a row, use the arrows, or promote a row into the top 10 to reorder within
          its score tier
          {tiers.some((t) => t.includedCount < t.members.length) ? '; the line marks the cutoff for the top list.' : '.'}
        </p>

        <div className="top-anime-selection__tiers" ref={tiersRef}>
          {tiers.map((tier, tierIndex) => {
            const boundary = promoteBoundary(tier)
            return (
              <div key={tier.score} className="top-anime-selection__tier">
                <h3 className="top-anime-selection__tier-title">Score {tier.score}</h3>
                <ul className="top-anime-selection__list">
                  {tier.members.map((member, index) => {
                    const isSource = drag != null && drag.tierIndex === tierIndex && drag.fromIndex === index
                    const isTarget =
                      drag != null && drag.tierIndex === tierIndex && drag.targetIndex === index && drag.targetIndex !== drag.fromIndex
                    const insertAbove = isTarget && drag != null && drag.targetIndex < drag.fromIndex
                    const insertBelow = isTarget && drag != null && drag.targetIndex > drag.fromIndex
                    return (
                      <li key={member.animeId}>
                        {insertAbove && <div className="top-anime-selection__insertion-line" />}
                        <div
                          className={
                            isSource ? 'top-anime-selection__row top-anime-selection__row--dragging' : 'top-anime-selection__row'
                          }
                          data-tier-index={tierIndex}
                          data-row-index={index}
                          onPointerDown={(event) => handlePointerDown(event, tierIndex, index)}
                          onPointerMove={(event) => handlePointerMove(event, tierIndex, index)}
                          onPointerUp={handlePointerUp}
                          onPointerCancel={handlePointerCancel}
                        >
                          <MemberPicture member={member} />
                          <span
                            className="top-anime-selection__row-title"
                            title={pickDisplayTitle(member.title, member.englishTitle)}
                          >
                            {pickDisplayTitle(member.title, member.englishTitle)}
                          </span>
                          <span className="top-anime-selection__row-buttons" onPointerDown={(event) => event.stopPropagation()}>
                            {index < boundary ? (
                              <>
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
                              </>
                            ) : (
                              <button
                                type="button"
                                className="top-anime-selection__promote"
                                aria-label="Move into top 10"
                                onClick={() => moveMember(tierIndex, index, boundary - 1)}
                              >
                                ⤒
                              </button>
                            )}
                          </span>
                        </div>
                        {insertBelow && <div className="top-anime-selection__insertion-line" />}
                        {index === tier.includedCount - 1 && tier.includedCount < tier.members.length && (
                          <div className="top-anime-selection__cut-line" role="separator" aria-label="Top list cutoff" />
                        )}
                      </li>
                    )
                  })}
                </ul>
              </div>
            )
          })}
        </div>

        {drag && (
          <div
            className="top-anime-selection__preview"
            style={{ left: drag.x - drag.offsetX, top: drag.y - drag.offsetY, width: drag.width }}
          >
            <MemberPicture member={tiers[drag.tierIndex].members[drag.fromIndex]} />
            <span className="top-anime-selection__row-title">
              {pickDisplayTitle(
                tiers[drag.tierIndex].members[drag.fromIndex].title,
                tiers[drag.tierIndex].members[drag.fromIndex].englishTitle,
              )}
            </span>
          </div>
        )}

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
