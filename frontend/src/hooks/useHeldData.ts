import { useState } from 'react'

// Keeps a period page's current content on screen while the next period's
// read is in flight (page-load-states, "Stepping to another period keeps the
// current content until the next arrives"). `data` is usePageData's, which
// goes null the moment the key changes; this holds the last non-null value
// through that gap, so stepping the Season, Year or Airing page never
// collapses it to an empty page or a loading line between two periods.
//
// - `shown` is `data` when there is any, otherwise the value held from
//   before the step.
// - `failed` drops the held value, so the new period's failure state shows
//   rather than the old period's content under a "couldn't load" message.
// - `stale` is true while the held value is what's showing. Pages pass it
//   through useDelayedFlag and add `held-content--muted` once it holds, so a
//   fast step shows nothing different and a slow one marks the old content
//   as not the new period's.
//
// A restore is seeded from its snapshot, so `data` is never null there and
// nothing is held.
export function useHeldData<T>(data: T | null, failed: boolean): { shown: T | null; stale: boolean } {
  const [held, setHeld] = useState<T | null>(data)

  // Adjusted while rendering rather than in an effect, so the frame in which
  // `data` goes null still has the held value to draw and never paints empty.
  if (data !== null && held !== data) setHeld(data)
  else if (failed && held !== null) setHeld(null)

  const shown = data ?? (failed ? null : held)
  return { shown, stale: data === null && shown !== null }
}
