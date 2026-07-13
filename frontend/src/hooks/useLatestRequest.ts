import { useRef } from 'react'

// Stale-response guard for async effects: bump to a new generation before
// starting a request, then check it's still current before applying the
// result. `current()` snapshots the *existing* generation for a follow-up
// request (e.g. infinite-scroll "load more") that shouldn't itself start a
// new one — only a real parameter change (new query, new season) should.
export function useLatestRequest() {
  const idRef = useRef(0)

  function start(): number {
    return ++idRef.current
  }

  function current(): number {
    return idRef.current
  }

  function isLatest(id: number): boolean {
    return idRef.current === id
  }

  return { start, current, isLatest }
}
