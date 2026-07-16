import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'

type ScoreVisibilityContextValue = {
  hidden: boolean
  toggle: () => void
  alwaysShowCompletedScores: boolean
  toggleAlwaysShowCompletedScores: () => void
}

const ScoreVisibilityContext = createContext<ScoreVisibilityContextValue | null>(null)

// Persisted so the choice survives a reload / new tab — otherwise a user who
// hides scores sees them reappear on every refresh.
const STORAGE_KEY = 'bettermal.scoresHidden'
const ALWAYS_SHOW_COMPLETED_STORAGE_KEY = 'bettermal.alwaysShowCompletedScores'

function readInitialHidden(): boolean {
  if (typeof window === 'undefined') return false
  return window.localStorage.getItem(STORAGE_KEY) === 'true'
}

function readInitialAlwaysShowCompletedScores(): boolean {
  if (typeof window === 'undefined') return false
  return window.localStorage.getItem(ALWAYS_SHOW_COMPLETED_STORAGE_KEY) === 'true'
}

export function ScoreVisibilityProvider({ children }: { children: ReactNode }) {
  const [hidden, setHidden] = useState(readInitialHidden)
  const [alwaysShowCompletedScores, setAlwaysShowCompletedScores] = useState(readInitialAlwaysShowCompletedScores)

  useEffect(() => {
    window.localStorage.setItem(STORAGE_KEY, String(hidden))
  }, [hidden])

  useEffect(() => {
    window.localStorage.setItem(ALWAYS_SHOW_COMPLETED_STORAGE_KEY, String(alwaysShowCompletedScores))
  }, [alwaysShowCompletedScores])

  const value = useMemo(
    () => ({
      hidden,
      toggle: () => setHidden((h) => !h),
      alwaysShowCompletedScores,
      toggleAlwaysShowCompletedScores: () => setAlwaysShowCompletedScores((v) => !v),
    }),
    [hidden, alwaysShowCompletedScores],
  )

  return <ScoreVisibilityContext.Provider value={value}>{children}</ScoreVisibilityContext.Provider>
}

export function useScoreVisibility(): ScoreVisibilityContextValue {
  const ctx = useContext(ScoreVisibilityContext)
  if (!ctx) throw new Error('useScoreVisibility must be used within a ScoreVisibilityProvider')
  return ctx
}
