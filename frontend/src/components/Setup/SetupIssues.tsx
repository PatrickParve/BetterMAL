import { useState } from 'react'
import { retrySetupNow } from '../../api/client.ts'
import type { ConnectError, SetupStatusDto } from '../../api/types.ts'
import { useNow } from '../../hooks/useNow.ts'
import { CONNECT_ERROR_MESSAGES, SETUP_STEPS, formatClock, formatCountdown } from './setupFormat.ts'
import './SetupIssues.css'

type Issue = { key: string; text: string }

// The lines the issues block shows, from the status alone (design D18): a paused
// service, a service limiting requests with its countdown, and anime waiting to retry.
// Returns whether any of them is a wait Retry now can end (a throttle isn't: the service
// asked for it).
function buildIssues(status: SetupStatusDto, nowMs: number): { issues: Issue[]; retryable: boolean } {
  const issues: Issue[] = []
  let retryable = false

  for (const service of status.services) {
    if (service.down) {
      retryable = true
      const tryAt = service.nextTryAt ? new Date(service.nextTryAt).getTime() : null
      const when =
        tryAt === null ? '' : tryAt > nowMs ? ` Next try at ${formatClock(service.nextTryAt!)}.` : ' Trying again now.'
      issues.push({
        key: `${service.name}-down`,
        text: `${service.name} appears to be down. Setup retries automatically.${when}`,
      })
    }
    const throttledUntil = service.throttledUntil ? new Date(service.throttledUntil).getTime() : null
    if (throttledUntil !== null && throttledUntil > nowMs) {
      issues.push({
        key: `${service.name}-throttled`,
        text: `${service.name} is limiting requests, resuming in ${formatCountdown(throttledUntil - nowMs)}.`,
      })
    }
  }

  for (const { key, title } of SETUP_STEPS) {
    const wait = status.steps[key].waitingRetry
    if (!wait) continue
    retryable = true
    const next = `next at ${formatClock(wait.nextAt)}`
    issues.push({
      key: `${key}-retry`,
      text:
        key === 'list'
          ? `Reading your list is waiting to retry, ${next}.`
          : `${wait.count} anime in “${title}” ${wait.count === 1 ? 'is' : 'are'} waiting to retry, ${next}.`,
    })
  }

  // A throttle countdown alone offers Retry now too ("Whenever the setup screen shows an
  // issue"), though pressing it leaves that wait alone.
  const anyThrottle = issues.some((i) => i.key.endsWith('-throttled'))
  return { issues, retryable: retryable || anyThrottle }
}

// Setup's problems and what can be done about them: which services are paused or
// limiting requests and when they resume, the anime waiting to retry, Retry now while
// there is anything to retry, and Reconnect when MyAnimeList refused the login. Renders
// nothing when all is well. Shared by the setup screen and Settings' Library data entry.
// `onRetried` is called after Retry now went through, so the caller can re-read the
// status at once rather than at its next tick.
export function SetupIssues({
  status,
  connectError = null,
  onRetried,
}: {
  status: SetupStatusDto
  connectError?: ConnectError | null
  onRetried: () => void
}) {
  const [retrying, setRetrying] = useState(false)
  const anyThrottle = status.services.some((s) => s.throttledUntil !== null)
  const nowMs = useNow(anyThrottle || status.services.some((s) => s.down))

  const { issues, retryable } = buildIssues(status, nowMs)
  const reconnect = !status.finished && (status.waitingForReconnect || status.connection.state === 'Lost')

  async function retryNow() {
    if (retrying) return
    setRetrying(true)
    try {
      await retrySetupNow()
      onRetried()
    } catch {
      // Nothing to say: the status read that follows shows what is still waiting, and
      // the button stays pressable.
    } finally {
      setRetrying(false)
    }
  }

  if (issues.length === 0 && !reconnect) return null

  return (
    <div className="setup-issues">
      {issues.length > 0 && (
        <>
          <ul className="setup-issues__list">
            {issues.map((issue) => (
              <li key={issue.key} className="setup-issue">
                {issue.text}
              </li>
            ))}
          </ul>
          {retryable && (
            <button type="button" className="setup-issues__button" onClick={retryNow} disabled={retrying}>
              Retry now
            </button>
          )}
        </>
      )}
      {reconnect && (
        <div className="setup-issues__reconnect">
          <p className="setup-issue">
            MyAnimeList refused the stored login.{' '}
            {status.steps.list.phase === 'Done'
              ? 'Setup carries on without it, but reconnect so the app can keep your list in sync.'
              : 'Reading your list waits until you reconnect. The other steps carry on.'}
          </p>
          {connectError && (
            <p className="setup-issue setup-issue--error" role="alert">
              {CONNECT_ERROR_MESSAGES[connectError]}
            </p>
          )}
          <a className="setup-issues__button" href="/api/mal-auth/start">
            Reconnect
          </a>
        </div>
      )}
    </div>
  )
}
