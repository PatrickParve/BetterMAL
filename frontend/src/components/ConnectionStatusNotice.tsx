import { useEffect, useState, useSyncExternalStore } from 'react'
import { getHealth } from '../api/client.ts'
import { getSnapshot, subscribe } from '../api/connectionStatus.ts'
import './ConnectionStatusNotice.css'

const HEALTH_POLL_INTERVAL_MS = 5000

// Mounted once in AppShell, outside <Routes>, so it survives navigation and
// covers every page after the shell has mounted. The pre-shell "can't reach
// the backend" hero in App.tsx handles the one request this can't: the very
// first getMalAuthStatus() call, before there's a shell to show a bar over.
export function ConnectionStatusNotice() {
  const reachable = useSyncExternalStore(subscribe, getSnapshot)
  const [dismissed, setDismissed] = useState(false)

  // Dismissal only matters while the bar is showing; clearing it whenever
  // the backend is reachable means a later unreachable transition — a new
  // outage — always starts undismissed.
  useEffect(() => {
    if (reachable) setDismissed(false)
  }, [reachable])

  useEffect(() => {
    if (reachable) return
    const interval = setInterval(() => {
      getHealth().catch(() => {
        // Still down; the next tick tries again. Recovery is reported by
        // getHealth's own success through the same fetchJson path.
      })
    }, HEALTH_POLL_INTERVAL_MS)
    return () => clearInterval(interval)
  }, [reachable])

  if (reachable || dismissed) return null

  return (
    <div className="connection-status-notice" role="status">
      <span>Can't reach the server — retrying…</span>
      <button
        type="button"
        className="connection-status-notice__dismiss"
        onClick={() => setDismissed(true)}
        aria-label="Dismiss"
      >
        ×
      </button>
    </div>
  )
}
