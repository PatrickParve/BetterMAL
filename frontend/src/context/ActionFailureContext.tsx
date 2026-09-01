import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { useLocation } from 'react-router-dom'

const MAX_NOTICES = 3
const AUTO_DISMISS_MS = 8000

export type ActionFailure = {
  id: number
  title: string
  reason: string | null
}

type ActionFailureContextValue = {
  failures: ActionFailure[]
  reportFailure: (failure: { title: string; reason: string | null }) => void
  dismissFailure: (id: number) => void
}

const ActionFailureContext = createContext<ActionFailureContextValue | null>(null)

// App-wide mechanism for reporting an action that failed silently — mirrors
// ConnectionStatusNotice's "mounted once, outside the routed pages" shape
// (action-failure-notices capability, design D6) so a control added later is
// covered without asking to be.
export function ActionFailureProvider({ children }: { children: ReactNode }) {
  const [failures, setFailures] = useState<ActionFailure[]>([])
  const nextIdRef = useRef(0)
  const { pathname } = useLocation()

  const dismissFailure = useCallback((id: number) => {
    setFailures((prev) => prev.filter((failure) => failure.id !== id))
  }, [])

  const reportFailure = useCallback(({ title, reason }: { title: string; reason: string | null }) => {
    const id = nextIdRef.current++
    setFailures((prev) => [{ id, title, reason }, ...prev].slice(0, MAX_NOTICES))
    setTimeout(() => dismissFailure(id), AUTO_DISMISS_MS)
  }, [dismissFailure])

  // A route change is a page change (the same rule Modal already applies to
  // overlays) — the control that raised a notice is no longer on screen.
  useEffect(() => {
    setFailures([])
  }, [pathname])

  const value = useMemo(() => ({ failures, reportFailure, dismissFailure }), [failures, reportFailure, dismissFailure])

  return <ActionFailureContext.Provider value={value}>{children}</ActionFailureContext.Provider>
}

export function useActionFailure(): (failure: { title: string; reason: string | null }) => void {
  const ctx = useContext(ActionFailureContext)
  if (!ctx) throw new Error('useActionFailure must be used within an ActionFailureProvider')
  return ctx.reportFailure
}

export function useActionFailures(): { failures: ActionFailure[]; dismissFailure: (id: number) => void } {
  const ctx = useContext(ActionFailureContext)
  if (!ctx) throw new Error('useActionFailures must be used within an ActionFailureProvider')
  return { failures: ctx.failures, dismissFailure: ctx.dismissFailure }
}
