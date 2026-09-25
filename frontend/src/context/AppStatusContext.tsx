import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { getAppStatus } from '../api/client.ts'
import type { AppStatusDto, AppStatusJobsDto, JobStatusDto } from '../api/types.ts'
import { useReconnectRetry } from '../hooks/useReconnectRetry.ts'

type AppStatusContextValue = {
  status: AppStatusDto | null
  // A read failed and there is no status to show yet (page-load-states), so
  // the Settings page can say so instead of loading for ever. Never set once a
  // status is held: a later failed poll just keeps what is there.
  failed: boolean
  refresh: () => Promise<void>
  // Try again: re-runs the read, and allows one more automatic retry when the
  // server next becomes reachable.
  retry: () => void
  applyJob: <K extends keyof AppStatusJobsDto>(key: K, dto: AppStatusJobsDto[K]) => void
  applyWeeklyOutcomeSeen: () => void
}

const AppStatusContext = createContext<AppStatusContextValue | null>(null)

// App-wide provider for the combined api/app-status read (navbar-settings-
// status-indicator design.md D7), replacing the old per-page
// hooks/useAppStatus.ts. Mounted once in AppShell around the navbar and the
// routes, so the navbar and the Settings page share one poll and one
// snapshot — a press or a report shows up in both in the same render instead
// of the two disagreeing for up to a second.
export function AppStatusProvider({ children }: { children: ReactNode }) {
  const [status, setStatus] = useState<AppStatusDto | null>(null)
  const [failed, setFailed] = useState(false)
  const statusRef = useRef<AppStatusDto | null>(null)

  const refresh = useCallback(async () => {
    try {
      const next = await getAppStatus()
      statusRef.current = next
      setStatus(next)
      setFailed(false)
    } catch {
      // Leave whatever was already there; the next poll or refresh retries.
      // With nothing there yet, that is a failure the Settings page reports.
      if (statusRef.current === null) setFailed(true)
    }
  }, [])

  useEffect(() => {
    void refresh()
  }, [refresh])

  // Reads again at once when the server becomes reachable, rather than
  // leaving Settings on its failure state until the next 10-second tick. The
  // shared once-per-failure rule (design.md D2/D8 of smooth-page-loading)
  // applies here too, though the poll below keeps trying regardless.
  const retryRead = useCallback(() => {
    setFailed(false)
    void refresh()
  }, [refresh])
  const rearmReconnectRetry = useReconnectRetry(failed, retryRead)
  const retry = useCallback(() => {
    rearmReconnectRetry()
    retryRead()
  }, [rearmReconnectRetry, retryRead])

  // Polls every second while any job is running, and every 10 seconds
  // otherwise, and only while the tab is visible (design.md D7, unchanged
  // from the old per-page hook). Reschedules itself after each attempt
  // rather than using setInterval, so a change in "any job running" takes
  // effect on the very next tick.
  useEffect(() => {
    let cancelled = false
    let timeoutId: ReturnType<typeof setTimeout>

    function scheduleNext() {
      const anyRunning = Object.values(statusRef.current?.jobs ?? ({} as AppStatusJobsDto)).some(
        (job) => job.phase === 'Running',
      )
      const delay = anyRunning ? 1000 : 10000
      timeoutId = setTimeout(async () => {
        if (cancelled) return
        if (document.visibilityState === 'visible') await refresh()
        if (!cancelled) scheduleNext()
      }, delay)
    }

    scheduleNext()
    return () => {
      cancelled = true
      clearTimeout(timeoutId)
    }
  }, [refresh])

  useEffect(() => {
    function onVisible() {
      if (document.visibilityState === 'visible') void refresh()
    }
    window.addEventListener('focus', onVisible)
    document.addEventListener('visibilitychange', onVisible)
    return () => {
      window.removeEventListener('focus', onVisible)
      document.removeEventListener('visibilitychange', onVisible)
    }
  }, [refresh])

  // Merges a trigger's response at once, so the first press shows the bar
  // without waiting for the next poll (design.md D7).
  const applyJob = useCallback(<K extends keyof AppStatusJobsDto>(key: K, dto: AppStatusJobsDto[K]) => {
    setStatus((prev) => {
      if (!prev) return prev
      const next = { ...prev, jobs: { ...prev.jobs, [key]: dto } }
      statusRef.current = next
      return next
    })
  }, [])

  // Merges the weekly check's own seen report at once, the same way applyJob
  // does for a job (design.md D9): the Settings page reports it as it shows
  // it, and the navbar's indicator clears in the same render rather than
  // waiting for the next poll.
  const applyWeeklyOutcomeSeen = useCallback(() => {
    setStatus((prev) => {
      if (!prev?.weeklyCheck) return prev
      const next = { ...prev, weeklyCheck: { ...prev.weeklyCheck, outcomeSeen: true } }
      statusRef.current = next
      return next
    })
  }, [])

  const value = useMemo(
    () => ({ status, failed, refresh, retry, applyJob, applyWeeklyOutcomeSeen }),
    [status, failed, refresh, retry, applyJob, applyWeeklyOutcomeSeen],
  )

  return <AppStatusContext.Provider value={value}>{children}</AppStatusContext.Provider>
}

export function useAppStatus(): AppStatusContextValue {
  const ctx = useContext(AppStatusContext)
  if (!ctx) throw new Error('useAppStatus must be used within an AppStatusProvider')
  return ctx
}

// The fixed order oldestRunningJob falls back to on a tie or a missing start
// time (design.md D8) — AppStatusJobsDto's own field order, so every browser
// resolves a tie identically. unseenOutcomes reports in this same order too,
// for a deterministic report rather than object-key iteration order.
const JOB_KEY_ORDER: (keyof AppStatusJobsDto)[] = [
  'listImport',
  'syncNow',
  'reconcile',
  'heldDecision',
  'airingRefresh',
  'seriesBuild',
  'fileImport',
]

export type UnseenOutcomes = {
  jobs: { name: keyof AppStatusJobsDto; finishedAt: string }[]
  weeklyCheckLastRunAt: string | null
}

// Every outcome the navbar's indicator counts and the Settings page reports
// as seen (design.md D8, D9): a job that ended — Complete or Failed, with a
// finishedAt — whose outcome hasn't been reported yet, plus the weekly
// check's own failure when it hasn't been reported either. A null status
// (nothing read yet) claims nothing.
export function unseenOutcomes(status: AppStatusDto | null): UnseenOutcomes {
  if (!status) return { jobs: [], weeklyCheckLastRunAt: null }

  const jobs = JOB_KEY_ORDER.filter((key) => {
    const job = status.jobs[key]
    return (job.phase === 'Complete' || job.phase === 'Failed') && job.finishedAt !== null && !job.outcomeSeen
  }).map((key) => ({ name: key, finishedAt: status.jobs[key].finishedAt as string }))

  const weekly = status.weeklyCheck
  const weeklyCheckLastRunAt = weekly && weekly.failed === true && !weekly.outcomeSeen ? weekly.lastRunAt : null

  return { jobs, weeklyCheckLastRunAt }
}

// Whether the navbar's Settings control shows its indicator (design.md D8;
// navbar-settings-status "The Settings control signals that something needs
// me"): held changes, a waiting diff, a lost connection, or any unseen
// outcome. A null status claims nothing before the first read lands.
export function settingsNeedsAttention(status: AppStatusDto | null): boolean {
  if (!status) return false
  const outcomes = unseenOutcomes(status)
  return (
    status.sync.heldCount > 0 ||
    status.sync.diffPending ||
    status.malConnection.state === 'Lost' ||
    outcomes.jobs.length > 0 ||
    outcomes.weeklyCheckLastRunAt !== null
  )
}

// The job whose progress bar the navbar's Settings control shows (design.md
// D8; navbar-settings-status "One bar at a time, oldest first"): among
// Running jobs, the one with the smallest startedAt. A tie, or a Running job
// with no startedAt at all, falls back to JOB_KEY_ORDER, so every browser
// picks the same one.
export function oldestRunningJob(status: AppStatusDto | null): JobStatusDto | null {
  if (!status) return null
  let best: JobStatusDto | null = null
  let bestStartedAt: number | null = null
  for (const key of JOB_KEY_ORDER) {
    const job = status.jobs[key]
    if (job.phase !== 'Running') continue
    if (best === null) {
      best = job
      bestStartedAt = job.startedAt ? new Date(job.startedAt).getTime() : null
      continue
    }
    const startedAt = job.startedAt ? new Date(job.startedAt).getTime() : null
    if (startedAt !== null && (bestStartedAt === null || startedAt < bestStartedAt)) {
      best = job
      bestStartedAt = startedAt
    }
  }
  return best
}
