import { useEffect, useRef, useState } from 'react'
import { IncrementButton } from './IncrementButton.tsx'
import './ProgressBar.css'

type ProgressBarProps = {
  watched: number
  total: number | null
  /** When provided, renders a "+" button after the label that bumps episodes watched by one. */
  onIncrement?: () => void
  /** When provided, the `watched` half of the label becomes a click/focus-to-edit field that sets episodes watched to a typed value. */
  onSetWatched?: (value: number) => void | Promise<void>
  /** Clamp ceiling for the editable field; `null`/`undefined` means no upper cap. */
  max?: number | null
  incrementPending?: boolean
  incrementLabel?: string
}

// Shared "watched/total" episode progress bar — shows `watched/?` when the
// total episode count isn't known yet. Reused across the dashboard, my list,
// and detail pages. When `onSetWatched` is given, the `watched` count becomes
// a click/focus-to-edit field so a specific episode number can be set
// directly; the save always routes back through the caller's
// useEpisodeIncrement()/useSetEpisodesWatched() target so the completion
// prompt still fires when appropriate.
export function ProgressBar({ watched, total, onIncrement, onSetWatched, max, incrementPending, incrementLabel }: ProgressBarProps) {
  const pct = total ? Math.min(100, (watched / total) * 100) : 0
  // The "+" button shares the same ceiling as the editable field — for a
  // still-airing show that's episodes aired so far, not the eventual total —
  // so it disables at the same point a typed value would clamp at.
  const cap = max === undefined ? total : max
  const atMax = cap !== null && watched >= cap

  const [editing, setEditing] = useState(false)
  const [draft, setDraft] = useState(String(watched))
  const inputRef = useRef<HTMLInputElement>(null)
  // Mirrors `editing` but updates synchronously (unlike React state), so a
  // second close trigger for the same edit session (e.g. a blur that follows
  // an Enter/Escape that already closed the field) can be detected as a
  // no-op instead of racing a stale flag.
  const editingRef = useRef(false)

  useEffect(() => {
    if (editing && inputRef.current) {
      inputRef.current.focus()
      inputRef.current.select()
    }
  }, [editing])

  function startEditing() {
    if (incrementPending) return
    setDraft(String(watched))
    editingRef.current = true
    setEditing(true)
  }

  function closeEditing(save: boolean) {
    if (!editingRef.current) return
    editingRef.current = false
    setEditing(false)
    if (!save) return

    const trimmed = draft.trim()
    if (trimmed === '') return
    const parsed = Number(trimmed)
    if (!Number.isFinite(parsed)) return
    const clamped = max != null ? Math.min(parsed, max) : parsed
    if (clamped === watched) return
    onSetWatched?.(clamped)
  }

  function handleChange(event: React.ChangeEvent<HTMLInputElement>) {
    const digitsOnly = event.target.value.replace(/\D/g, '')
    if (digitsOnly === '' || max == null) {
      setDraft(digitsOnly)
      return
    }
    // Clamp as you type, not just on commit, so a value above the cap is
    // never even visible in the field.
    setDraft(String(Math.min(Number(digitsOnly), max)))
  }

  function handleBlur() {
    closeEditing(true)
  }

  function handleKeyDown(event: React.KeyboardEvent<HTMLInputElement>) {
    event.stopPropagation()
    if (event.key === 'Enter') {
      event.preventDefault()
      closeEditing(true)
    } else if (event.key === 'Escape') {
      event.preventDefault()
      closeEditing(false)
    }
  }

  return (
    <div className="progress-bar">
      <div className="progress-bar__track">
        <div className="progress-bar__fill" style={{ width: `${pct}%` }} />
      </div>
      <span className="progress-bar__label">
        {onSetWatched ? (
          editing ? (
            <input
              ref={inputRef}
              type="text"
              inputMode="numeric"
              className="progress-bar__watched-input"
              style={{ width: `${Math.max(draft.length, 1)}ch` }}
              value={draft}
              disabled={incrementPending}
              onChange={handleChange}
              onKeyDown={handleKeyDown}
              onBlur={handleBlur}
              onClick={(event) => {
                event.preventDefault()
                event.stopPropagation()
              }}
              aria-label="Set episodes watched"
            />
          ) : (
            <button
              type="button"
              className="progress-bar__watched"
              style={{ width: `${Math.max(String(watched).length, 1)}ch` }}
              disabled={incrementPending}
              onClick={(event) => {
                event.preventDefault()
                event.stopPropagation()
                startEditing()
              }}
              onFocus={startEditing}
              aria-label="Edit episodes watched"
            >
              {watched}
            </button>
          )
        ) : (
          watched
        )}
        /{total ?? '?'}
      </span>
      {onIncrement && (
        <IncrementButton
          onIncrement={onIncrement}
          disabled={incrementPending || atMax}
          label={incrementLabel ?? 'Increment episodes watched'}
        />
      )}
    </div>
  )
}
