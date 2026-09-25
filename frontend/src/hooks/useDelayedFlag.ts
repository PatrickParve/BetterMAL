import { useEffect, useState } from 'react'

// How long a read must stay in flight before anything is drawn to say so
// (page-load-states). Well above the 10-45 ms warm reads, so a fast load goes
// from the page's header straight to its content, and still short enough that
// a slow one feels acknowledged. The one tunable for every loading indicator
// in the app.
export const LOADING_INDICATOR_DELAY_MS = 300

// `true` only once `active` has stayed true for `delayMs`, and `false` the
// moment it goes false. Drives every "in progress" affordance (the loading
// notice, the season and year pages' "Updating…" label, the muted held grid),
// so work that settles inside the delay leaves no trace on screen.
export function useDelayedFlag(active: boolean, delayMs: number = LOADING_INDICATOR_DELAY_MS): boolean {
  const [elapsed, setElapsed] = useState(false)

  useEffect(() => {
    if (!active) return
    const timer = setTimeout(() => setElapsed(true), delayMs)
    return () => {
      clearTimeout(timer)
      // Reset on the way out, so a later activation waits its own full delay
      // rather than inheriting this one's elapsed timer.
      setElapsed(false)
    }
  }, [active, delayMs])

  // Gated on `active` as well, so the flag drops in the same render that
  // `active` does, ahead of the effect above resetting `elapsed`.
  return active && elapsed
}
