import { useState } from 'react'
import { useLocation } from 'react-router-dom'
import { useScoreVisibility } from '../context/ScoreVisibilityContext.tsx'

// `revealed` must re-hide whenever the page the reveal was granted on goes
// away — but a route like `/anime/:id` keeps one component mounted across
// params, so unmounting can't be relied on. Pathname (not `location.key`)
// is the identity: it changes exactly when the user leaves a page, not on
// every history entry a same-page filter writes to the URL. `hidden` rides
// along so that switching the global toggle off and back on also drops the
// reveal, keeping "while hidden, every value is replaced" true at the
// moment it is switched back on. Compared during render, the same pattern
// `useRestorableState`/`usePageData` use, so a hidden value never paints
// revealed for one frame on the new identity. Shared verbatim by the score
// itself, the anime detail page's rank, and the series page's highest-MAL
// stat — all three want the identical reset.
export function useScoreReveal(): [boolean, () => void] {
  const { hidden } = useScoreVisibility()
  const { pathname } = useLocation()
  const identity = `${pathname}|${hidden}`
  const [renderedIdentity, setRenderedIdentity] = useState(identity)
  const [revealed, setRevealed] = useState(false)

  if (identity !== renderedIdentity) {
    setRenderedIdentity(identity)
    setRevealed(false)
  }

  return [revealed, () => setRevealed(true)]
}
