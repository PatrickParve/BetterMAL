import { useEffect, useRef } from 'react'

// "My ranking may have changed" (add-my-rank-badges design.md D6): a tiny
// pub/sub the rank badges listen to. The API client fires it after any save
// that can move a rank — an entry save or removal, a ranking-editor reorder, a
// This or That placement, a series favourite reorder — so a page that shows a
// rank fetches just that rank again. Firing it from the client layer, rather
// than from each caller, covers the app-root overlays a page cannot see and any
// future caller, with no prop threading.
const listeners = new Set<() => void>()

// Called by the client once a save has succeeded; a failed save changed nothing.
// A listener that throws must not turn a save that worked into a rejected one,
// nor keep the listeners after it from hearing.
export function notifyRankingChanged(): void {
  for (const listener of [...listeners]) {
    try {
      listener()
    } catch (error) {
      console.error(error)
    }
  }
}

// Runs `callback` on every signal while the calling component is mounted. The
// latest callback is the one called, so it may close over fresh state without
// the subscription being redone.
export function useRankingChanged(callback: () => void): void {
  const callbackRef = useRef(callback)
  useEffect(() => {
    callbackRef.current = callback
  })

  useEffect(() => {
    const listener = () => callbackRef.current()
    listeners.add(listener)
    return () => {
      listeners.delete(listener)
    }
  }, [])
}
