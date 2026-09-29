import { useCallback, useEffect, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import './App.css'
import { getSetupStatus } from './api/client.ts'
import { HEALTH_POLL_INTERVAL_MS } from './api/connectionStatus.ts'
import type { ConnectError, SetupStatusDto } from './api/types.ts'
import { AppShell } from './AppShell.tsx'
import { LoadingNotice } from './components/LoadingNotice.tsx'
import { SetupScreen } from './components/Setup/SetupScreen.tsx'
import { setupScreenKind } from './components/Setup/setupFormat.ts'

// How often the progress screen re-reads setup's status (first-run-setup design D18):
// the same cheap, no-outside-call read Settings' Library data entry repeats, at the
// pace of the backend's own five-second Home check and one-second progress.
const SETUP_POLL_INTERVAL_MS = 1000

// The failure the OAuth callback carried back (?connectError=, mal-api-integration).
// Read without touching the URL, so a StrictMode double call of this initializer can't
// take it away from the second call.
function readConnectError(): ConnectError | null {
  const value = new URLSearchParams(window.location.search).get('connectError')
  return value === 'denied' || value === 'expired' || value === 'failed' ? value : null
}

// Takes ?connectError= out of the address once the setup screen has it in state, so a
// reload no longer shows the message ("The message SHALL stay only until I leave or
// reload the screen"). history.replaceState rather than a router navigation: it changes
// nothing the router or a page reads, and the shell isn't mounted to notice.
function clearConnectErrorFromUrl() {
  const url = new URL(window.location.href)
  if (!url.searchParams.has('connectError')) return
  url.searchParams.delete('connectError')
  window.history.replaceState(window.history.state, '', `${url.pathname}${url.search}${url.hash}`)
}

function App() {
  const navigate = useNavigate()
  const [status, setStatus] = useState<SetupStatusDto | null>(null)
  const [statusError, setStatusError] = useState(false)
  // A read that failed while a setup screen was already showing: the screen keeps what it
  // last read and says it is retrying, instead of the full-page message that only the
  // very first read gets (connection-status, "First-load failure keeps its full-page
  // message").
  const [pollFailed, setPollFailed] = useState(false)
  const [connectError] = useState(readConnectError)

  // Whether a setup screen has been shown in this page load: finishing while one is up
  // sends me Home, whatever URL I had open, but a page that opened on a finished install
  // stays where it was opened.
  const sawSetupScreenRef = useRef(false)
  // Read at the moment of use rather than a dependency: useNavigate's function changes
  // with the location, and every read below would change identity with it.
  const navigateRef = useRef(navigate)
  navigateRef.current = navigate

  const applyStatus = useCallback((next: SetupStatusDto) => {
    if (setupScreenKind(next) !== null) {
      sawSetupScreenRef.current = true
      clearConnectErrorFromUrl()
    } else if (sawSetupScreenRef.current) {
      sawSetupScreenRef.current = false
      navigateRef.current('/', { replace: true })
    }
    setStatus(next)
    setStatusError(false)
    setPollFailed(false)
  }, [])

  // The first read. A success clears statusError, so the same function serves the
  // first load, the retry below and Try again: whichever one lands first continues
  // the app exactly as a first load that had succeeded (connection-status,
  // "First-load failure keeps its full-page message"). Concurrent calls are one
  // GET, since fetchRaw joins identical in-flight requests (api/client.ts).
  const readStatus = useCallback(() => {
    getSetupStatus()
      .then(applyStatus)
      .catch(() => setStatusError(true))
  }, [applyStatus])

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

  const kind = status === null ? null : setupScreenKind(status)

  // The progress screen follows the work by polling (first-run-setup "Setup's work runs in
  // the backend": the tab only shows progress). A failed poll leaves the last status up.
  // fetchRaw joins a poll with any other identical read in flight (api/client.ts).
  const pollStatus = useCallback(() => {
    getSetupStatus()
      .then(applyStatus)
      .catch(() => setPollFailed(true))
  }, [applyStatus])

  useEffect(() => {
    if (kind !== 'progress') return
    const interval = setInterval(pollStatus, SETUP_POLL_INTERVAL_MS)
    return () => clearInterval(interval)
  }, [kind, pollStatus])

  // The connect and credentials screens have nothing to poll, since only I can change
  // what they wait for. Authorizing leaves this tab, or happens in another one, and
  // fixing .env means a restart, so re-read the state whenever one of these is showing
  // and I return to it (mal-api-integration, "The connection state is reported"). This is
  // the focus/visibilitychange pattern from AppStatusContext.tsx, plus pageshow, because
  // a Back navigation restored from the back/forward cache doesn't remount and isn't
  // reliably followed by the other two events (design D8). Only listen while one of them
  // is showing: a re-read inside the shell could unmount it. focus and visibilitychange
  // firing together are the same GET, so fetchRaw joins them into one request.
  const waitingOnMe = kind === 'connect' || kind === 'credentials'

  useEffect(() => {
    if (!waitingOnMe) return

    function recheck() {
      if (document.visibilityState !== 'visible') return
      getSetupStatus()
        .then(applyStatus)
        .catch(() => {
          // Keep the screen rather than setting statusError: only the first read decides
          // the backend-down screen, and the next return retries (design D7).
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
  }, [waitingOnMe, applyStatus])

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

  if (kind !== null) {
    return (
      <SetupScreen
        kind={kind}
        status={status}
        connectError={connectError}
        unreachable={pollFailed}
        onRefresh={pollStatus}
      />
    )
  }

  return <AppShell />
}

export default App
