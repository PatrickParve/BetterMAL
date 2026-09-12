import { useState } from 'react'
import { useLocation } from 'react-router-dom'
import { useScoreVisibility } from '../context/ScoreVisibilityContext.tsx'
import './ScoreValue.css'

type ScoreValueProps = {
  value: number | null | undefined
  placeholder?: string
  completed?: boolean
}

// Renders a MAL score respecting the global hide toggle. While hidden and not
// individually revealed, the numeric value is never placed in the DOM at all —
// only the reveal control is rendered, alone — so it can't leak via
// devtools/text-selection. `completed` opts a score into the "always show for
// completed shows" setting: when that setting is on, a completed entry's
// score is shown in full with no reveal control, regardless of the global
// hide state.
export function ScoreValue({ value, placeholder = '—', completed = false }: ScoreValueProps) {
  const { hidden, alwaysShowCompletedScores } = useScoreVisibility()
  const { pathname } = useLocation()

  // `revealed` must re-hide whenever the page the reveal was granted on goes
  // away — but a route like `/anime/:id` keeps one component mounted across
  // params, so unmounting can't be relied on. Pathname (not `location.key`)
  // is the identity: it changes exactly when the user leaves a page, not on
  // every history entry a same-page filter writes to the URL. `hidden` rides
  // along so that switching the global toggle off and back on also drops the
  // reveal, keeping "while hidden, every score is replaced" true at the
  // moment it is switched back on. Compared during render, the same pattern
  // `useRestorableState`/`usePageData` use, so a hidden score never paints
  // revealed for one frame on the new identity.
  const identity = `${pathname}|${hidden}`
  const [renderedIdentity, setRenderedIdentity] = useState(identity)
  const [revealed, setRevealed] = useState(false)

  if (identity !== renderedIdentity) {
    setRenderedIdentity(identity)
    setRevealed(false)
  }

  if (value == null) return <span className="score-value">{placeholder}</span>

  if (!hidden || revealed || (completed && alwaysShowCompletedScores)) {
    return <span className="score-value">{value.toFixed(2)}</span>
  }

  return (
    <span className="score-value">
      <button
        type="button"
        className="score-value__reveal"
        onClick={(event) => {
          // ScoreValue often sits inside a clickable AnimeCard link — stop
          // the click from bubbling into a navigation/card action.
          event.preventDefault()
          event.stopPropagation()
          setRevealed(true)
        }}
        aria-label="Reveal score"
      >
        <EyeIcon />
      </button>
    </span>
  )
}

function EyeIcon() {
  return (
    <svg viewBox="0 0 24 24" width="13" height="13" fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden="true">
      <path d="M1,12 C1,12 5,5 12,5 C19,5 23,12 23,12 C23,12 19,19 12,19 C5,19 1,12 1,12 Z" />
      <circle cx="12" cy="12" r="3" />
    </svg>
  )
}
