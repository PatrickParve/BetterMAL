import { useSyncExternalStore } from 'react'
import { getSnapshot, subscribe } from '../api/connectionStatus.ts'
import './LoadFailedNotice.css'

// The one failure state every data page, profile section and overlay shows
// when its read failed and there is nothing from it to draw (page-load-states),
// so a failure is never dressed up as emptiness. `what` is the noun the page
// passes ("your list", "this season"). The second line says whether the
// server is down, in which case the page will come back by itself
// (useReconnectRetry), or answered with an error. `compact` is the same block
// for a section inside a page, left-aligned and without the page-level
// padding.
export function LoadFailedNotice({
  what,
  onRetry,
  compact = false,
}: {
  what: string
  onRetry: () => void
  compact?: boolean
}) {
  const reachable = useSyncExternalStore(subscribe, getSnapshot)

  return (
    <div className={compact ? 'load-failed-notice load-failed-notice--compact' : 'load-failed-notice'} role="alert">
      <p className="load-failed-notice__title">Couldn't load {what}.</p>
      <p className="load-failed-notice__reason">
        {reachable
          ? 'Something went wrong on the server.'
          : "The server can't be reached — this will load by itself once it's back."}
      </p>
      <button type="button" className="load-failed-notice__retry" onClick={onRetry}>
        Try again
      </button>
    </div>
  )
}
