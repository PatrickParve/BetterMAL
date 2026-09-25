import { useCallback, useEffect, useState } from 'react'
import './App.css'
import { getMalAuthStatus } from './api/client.ts'
import { HEALTH_POLL_INTERVAL_MS } from './api/connectionStatus.ts'
import type { MalAuthStatus } from './api/types.ts'
import { AppShell } from './AppShell.tsx'
import { LoadingNotice } from './components/LoadingNotice.tsx'

function App() {
  const [status, setStatus] = useState<MalAuthStatus | null>(null)
  const [statusError, setStatusError] = useState(false)

  // The first read. A success clears statusError, so the same function serves the
  // first load, the retry below and Try again: whichever one lands first continues
  // the app exactly as a first load that had succeeded (connection-status,
  // "First-load failure keeps its full-page message"). Concurrent calls are one
  // GET, since fetchRaw joins identical in-flight requests (api/client.ts).
  const readStatus = useCallback(() => {
    getMalAuthStatus()
      .then((next) => {
        setStatus(next)
        setStatusError(false)
      })
      .catch(() => setStatusError(true))
  }, [])

  useEffect(() => {
    readStatus()
  }, [readStatus])

  // While the full-page message is showing there is no shell, and so no health poll,
  // to notice the backend coming back (design D8). Re-read at the poll's own interval.
  useEffect(() => {
    if (!statusError) return
    const interval = setInterval(readStatus, HEALTH_POLL_INTERVAL_MS)
    return () => clearInterval(interval)
  }, [statusError, readStatus])

  // Authorizing leaves this tab, or happens in another one, and the OAuth callback page
  // doesn't redirect back, so re-read the connection state whenever the connect screen is
  // showing and I return to it (mal-api-integration, "The connection state is reported").
  // This is the focus/visibilitychange pattern from AppStatusContext.tsx, plus pageshow,
  // because a Back navigation restored from the back/forward cache doesn't remount and
  // isn't reliably followed by the other two events (design D8).
  // Only listen while the connect screen is showing: a re-read inside the shell could
  // unmount it, and a lost login is already reported inside the app (design D6).
  // focus and visibilitychange firing together are the same GET, so fetchRaw joins them
  // into one request (api/client.ts).
  const showingConnectScreen = status?.state === 'NotConnected'

  useEffect(() => {
    if (!showingConnectScreen) return

    function recheck() {
      if (document.visibilityState !== 'visible') return
      getMalAuthStatus()
        .then(setStatus)
        .catch(() => {
          // Keep the connect screen rather than setting statusError: only the first read
          // decides the backend-down screen, and the next return retries (design D7).
        })
    }

    window.addEventListener('focus', recheck)
    document.addEventListener('visibilitychange', recheck)
    window.addEventListener('pageshow', recheck)
    return () => {
      window.removeEventListener('focus', recheck)
      document.removeEventListener('visibilitychange', recheck)
      window.removeEventListener('pageshow', recheck)
    }
  }, [showingConnectScreen])

  if (statusError) {
    return (
      <section id="center">
        <div className="hero">
          <h1>Can't reach the backend</h1>
          <p>Make sure the backend service is running — retrying, and this page will continue by itself once it's back.</p>
          <button type="button" className="counter" onClick={readStatus}>
            Try again
          </button>
        </div>
      </section>
    )
  }

  if (status === null) {
    return (
      <section id="center">
        <div className="hero">
          <LoadingNotice />
        </div>
      </section>
    )
  }

  if (status.state === 'NotConnected') {
    return (
      <section id="center">
        <div className="hero">
          <h1>Connect your MyAnimeList account</h1>
          <p>
            This app mirrors your MAL list locally and keeps it in sync. Connect your account to import your list and
            get started.
          </p>
          <a className="counter" href="/api/mal-auth/start">
            Connect to MAL
          </a>
        </div>
      </section>
    )
  }

  return <AppShell />
}

export default App
