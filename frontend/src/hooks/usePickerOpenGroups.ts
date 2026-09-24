import { useCallback, useState } from 'react'
import { defaultOpenGroups, type PickerSection } from '../components/picturePickerSections.ts'

const NO_OPEN_GROUPS: ReadonlySet<string> = new Set()

// Which of a page's picture-picker groups are open (artwork-selection "The
// picture picker", design D12). Kept for as long as the page stays on
// `scopeKey`, so closing the picker and reopening it shows what was last open,
// and dropped the moment it changes: React Router keeps a page mounted when
// only its id changes (/anime/1 -> /anime/2), and a visit to another anime is
// a fresh visit that starts from the defaults again. Deliberately plain state
// rather than restorable page state (design D12): which groups are open is a
// browsing convenience, not a view setting to bring back on return.
//
// The page calls `seedOpenGroups` as the picker opens; it seeds the defaults
// the first time only. Until then `openGroups` is empty, which is never seen
// because the overlay is only mounted after a seed.
export function usePickerOpenGroups(scopeKey: number) {
  const [state, setState] = useState<{ scopeKey: number; open: Set<string> } | null>(null)
  const openGroups = state !== null && state.scopeKey === scopeKey ? state.open : NO_OPEN_GROUPS

  const seedOpenGroups = useCallback(
    (sections: PickerSection[], current: string | null) => {
      setState((prev) =>
        prev !== null && prev.scopeKey === scopeKey ? prev : { scopeKey, open: defaultOpenGroups(sections, current) },
      )
    },
    [scopeKey],
  )

  const toggleGroup = useCallback(
    (key: string) => {
      setState((prev) => {
        if (prev === null || prev.scopeKey !== scopeKey) return prev
        const open = new Set(prev.open)
        if (!open.delete(key)) open.add(key)
        return { scopeKey, open }
      })
    },
    [scopeKey],
  )

  return { openGroups, seedOpenGroups, toggleGroup }
}
