import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'

type ContentFilterContextValue = {
  hideHentai: boolean
  toggleHideHentai: () => void
}

const ContentFilterContext = createContext<ContentFilterContextValue | null>(null)

// Persisted so the choice survives a reload / new tab — otherwise a user who
// hides NSFW sees it reappear on every refresh.
const STORAGE_KEY = 'bettermal.hideNsfw'

// Defaults to hidden whenever nothing is stored, including on a first run
// (design.md D7) — a stored choice, true or false, always wins.
function readInitialHideHentai(): boolean {
  if (typeof window === 'undefined') return true
  const stored = window.localStorage.getItem(STORAGE_KEY)
  return stored === null ? true : stored === 'true'
}

export function ContentFilterProvider({ children }: { children: ReactNode }) {
  const [hideHentai, setHideHentai] = useState(readInitialHideHentai)

  useEffect(() => {
    window.localStorage.setItem(STORAGE_KEY, String(hideHentai))
  }, [hideHentai])

  const value = useMemo(
    () => ({
      hideHentai,
      toggleHideHentai: () => setHideHentai((h) => !h),
    }),
    [hideHentai],
  )

  return <ContentFilterContext.Provider value={value}>{children}</ContentFilterContext.Provider>
}

export function useContentFilter(): ContentFilterContextValue {
  const ctx = useContext(ContentFilterContext)
  if (!ctx) throw new Error('useContentFilter must be used within a ContentFilterProvider')
  return ctx
}
