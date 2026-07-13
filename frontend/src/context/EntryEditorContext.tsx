import { createContext, useContext, useState, type ReactNode } from 'react'
import type { EntryEditorTarget } from '../api/types.ts'
import { EntryEditorOverlay } from '../components/EntryEditorOverlay.tsx'

type EntryEditorContextValue = {
  openEditor: (target: EntryEditorTarget) => void
}

const EntryEditorContext = createContext<EntryEditorContextValue | null>(null)

// Mounts a single overlay instance at the app root; any component anywhere
// can open it via useEntryEditor().openEditor(...) instead of each page
// managing its own modal state.
export function EntryEditorProvider({ children }: { children: ReactNode }) {
  const [target, setTarget] = useState<EntryEditorTarget | null>(null)

  return (
    <EntryEditorContext.Provider value={{ openEditor: setTarget }}>
      {children}
      {target && <EntryEditorOverlay key={target.animeId} target={target} onClose={() => setTarget(null)} />}
    </EntryEditorContext.Provider>
  )
}

export function useEntryEditor(): EntryEditorContextValue {
  const ctx = useContext(EntryEditorContext)
  if (!ctx) throw new Error('useEntryEditor must be used within an EntryEditorProvider')
  return ctx
}
