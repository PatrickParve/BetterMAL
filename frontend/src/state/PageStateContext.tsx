import { createContext, useContext, useMemo, type ReactNode } from 'react'
import { useLocation, useNavigationType } from 'react-router-dom'
import * as pageStateStore from './pageStateStore.ts'
import type { PageSnapshot } from './pageStateStore.ts'

interface PageStateValue {
  key: string
  isRestore: boolean
  snapshot: PageSnapshot
}

const PageStateContext = createContext<PageStateValue | null>(null)

// Mounted once in AppShell, above every route, so every page can read
// whether it is being restored (back/forward to an entry seen before) or
// freshly visited, and reach the snapshot that belongs to this history entry.
export function PageStateProvider({ children }: { children: ReactNode }) {
  const location = useLocation()
  const navigationType = useNavigationType()

  const value = useMemo<PageStateValue>(() => {
    const key = location.key
    // `useNavigationType()` reports 'POP' for back/forward, but also for a
    // session's very first render and for a history entry never rendered in
    // this session (e.g. reload then back) — requiring an existing snapshot
    // collapses both of those into "fresh visit" alongside real restores.
    const isRestore = navigationType === 'POP' && pageStateStore.get(key) !== undefined
    const snapshot = pageStateStore.ensure(key)
    return { key, isRestore, snapshot }
  }, [location.key, navigationType])

  return <PageStateContext.Provider value={value}>{children}</PageStateContext.Provider>
}

export function usePageState(): PageStateValue {
  const context = useContext(PageStateContext)
  if (!context) throw new Error('usePageState must be used within a PageStateProvider')
  return context
}
