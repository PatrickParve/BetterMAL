import { useEffect, useState } from 'react'
import './App.css'

type MalAuthStatus = {
  connected: boolean
}

function App() {
  const [status, setStatus] = useState<MalAuthStatus | null>(null)
  const [statusError, setStatusError] = useState(false)

  useEffect(() => {
    fetch('/api/mal-auth/status')
      .then((res) => {
        if (!res.ok) throw new Error(`status ${res.status}`)
        return res.json() as Promise<MalAuthStatus>
      })
      .then(setStatus)
      .catch(() => setStatusError(true))
  }, [])

  return (
    <section id="center">
      <div className="hero">
        {statusError ? (
          <>
            <h1>Can't reach the backend</h1>
            <p>Make sure the backend service is running, then reload this page.</p>
          </>
        ) : status === null ? (
          <p>Loading…</p>
        ) : status.connected ? (
          <>
            <h1>Connected to MyAnimeList</h1>
            <p>Pages are coming soon.</p>
          </>
        ) : (
          <>
            <h1>Connect your MyAnimeList account</h1>
            <p>
              This app mirrors your MAL list locally and keeps it in sync.
              Connect your account to import your list and get started.
            </p>
            <a className="counter" href="/api/mal-auth/start">
              Connect to MAL
            </a>
          </>
        )}
      </div>
    </section>
  )
}

export default App
