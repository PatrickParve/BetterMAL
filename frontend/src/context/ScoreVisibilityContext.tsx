import { createContext, useContext, useMemo, useState, type ReactNode } from 'react'

type ScoreVisibilityContextValue = {
  hidden: boolean
  toggle: () => void
}

const ScoreVisibilityContext = createContext<ScoreVisibilityContextValue | null>(null)

export function ScoreVisibilityProvider({ children }: { children: ReactNode }) {
  const [hidden, setHidden] = useState(false)
  const value = useMemo(() => ({ hidden, toggle: () => setHidden((h) => !h) }), [hidden])

  return <ScoreVisibilityContext.Provider value={value}>{children}</ScoreVisibilityContext.Provider>
}

export function useScoreVisibility(): ScoreVisibilityContextValue {
  const ctx = useContext(ScoreVisibilityContext)
  if (!ctx) throw new Error('useScoreVisibility must be used within a ScoreVisibilityProvider')
  return ctx
}
