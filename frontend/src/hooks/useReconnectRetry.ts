import { useCallback, useEffect, useRef } from 'react'
import { getSnapshot, subscribe } from '../api/connectionStatus.ts'

// The single rule behind "a failed read reloads by itself once the server is
// back" (page-load-states, design D2/D7), shared by usePageData and the pages
// that keep their own load (Top anime, Settings' app status).
//
// On the unreachable → reachable transition, if `failed` and the retry is
// still armed, calls `retry` once and disarms. It stays disarmed until the
// returned `rearm()` is called, which the pages' Try again does, so a
// persistent server error costs one automatic retry per user action and can
// never become a loop: an error marks the backend unreachable, the health
// poll marks it reachable again, and this retries once, no more.
//
// A page whose data is on screen is never `failed`, so recovery leaves it
// alone. Armed on mount.
export function useReconnectRetry(failed: boolean, retry: () => void): () => void {
  const armedRef = useRef(true)
  // Read at the moment of the transition rather than captured, so the
  // subscription is made once and always sees the latest render's values.
  const failedRef = useRef(failed)
  failedRef.current = failed
  const retryRef = useRef(retry)
  retryRef.current = retry

  useEffect(
    () =>
      subscribe(() => {
        // The store notifies on both directions of a change; only reaching the
        // backend again is a recovery.
        if (!getSnapshot()) return
        if (!failedRef.current || !armedRef.current) return
        armedRef.current = false
        retryRef.current()
      }),
    [],
  )

  return useCallback(() => {
    armedRef.current = true
  }, [])
}
