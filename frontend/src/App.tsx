import { useEffect, useState } from 'react'
import './App.css'
import { getMalAuthStatus } from './api/client.ts'
import type { MalAuthStatus } from './api/types.ts'
import { AppShell } from './AppShell.tsx'

function App() {
  const [status, setStatus] = useState<MalAuthStatus | null>(null)
  const [statusError, setStatusError] = useState(false)

  useEffect(() => {
    getMalAuthStatus()
      .then(setStatus)
      .catch(() => setStatusError(true))
  }, [])

  if (statusError) {
    return (
      <section id="center">
        <div className="hero">
          <h1>Can't reach the backend</h1>
          <p>Make sure the backend service is running, then reload this page.</p>
        </div>
      </section>
    )
  }

  if (status === null) {
    return (
      <section id="center">
        <div className="hero">
          <p>Loading…</p>
        </div>
      </section>
    )
  }

  if (!status.connected) {
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
