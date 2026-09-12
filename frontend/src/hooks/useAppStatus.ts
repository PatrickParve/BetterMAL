import { useCallback, useEffect, useRef, useState } from 'react'
import { getAppStatus } from '../api/client.ts'
import type { AppStatusDto, AppStatusJobsDto } from '../api/types.ts'

// Polls the combined api/app-status read for the Settings page (design.md
// D16 of report-jobs-and-lost-mal-connection). Every consumer of job or
// connection state reads from this one hook instead of its own poll, so
// every browser and every part of the page agree on the same snapshot.
export function useAppStatus() {
  const [status, setStatus] = useState<AppStatusDto | null>(null)
  const statusRef = useRef<AppStatusDto | null>(null)

  const refresh = useCallback(async () => {
    try {
      const next = await getAppStatus()
      statusRef.current = next
      setStatus(next)
    } catch {
      // Leave whatever was already there; the next poll or refresh retries.
    }
  }, [])

  useEffect(() => {
    void refresh()
  }, [refresh])

  // Polls every second while any job is running, and every 10 seconds
  // otherwise, and only while the tab is visible (design.md D16). Reschedules
  // itself after each attempt rather than using setInterval, so a change in
  // "any job running" takes effect on the very next tick.
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
  // without waiting for the next poll (design.md D16).
  const applyJob = useCallback(<K extends keyof AppStatusJobsDto>(key: K, dto: AppStatusJobsDto[K]) => {
    setStatus((prev) => {
      if (!prev) return prev
      const next = { ...prev, jobs: { ...prev.jobs, [key]: dto } }
      statusRef.current = next
      return next
    })
  }, [])

  return { status, refresh, applyJob }
}
