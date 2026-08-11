import { useCallback, useState, type Dispatch, type SetStateAction } from 'react'
import { usePageState } from '../state/PageStateContext.tsx'

// Drop-in for `useState` on a view control (filter, sort, page number, ...)
// that should survive a back/forward restore. Seeds from the current history
// entry's snapshot when restoring, writes through to it on every change, and
// falls back to `initial` on a fresh visit — including a fresh visit that
// reuses the same mounted component (e.g. a new search query), which is
// detected by the underlying history entry changing under an unchanged key.
export function useRestorableState<T>(key: string, initial: T): [T, Dispatch<SetStateAction<T>>] {
  const { key: locationKey, isRestore, snapshot } = usePageState()

  const isSeeded = isRestore && snapshot.view.has(key)
  const seededValue = () => (isSeeded ? (snapshot.view.get(key) as T) : initial)

  const identity = `${locationKey}:${key}`
  const [renderedIdentity, setRenderedIdentity] = useState(identity)
  const [value, setValueState] = useState<T>(seededValue)

  if (identity !== renderedIdentity) {
    setRenderedIdentity(identity)
    setValueState(seededValue())
  }

  const setValue = useCallback<Dispatch<SetStateAction<T>>>(
    (update) => {
      setValueState((prev) => {
        const next = typeof update === 'function' ? (update as (prev: T) => T)(prev) : update
        snapshot.view.set(key, next)
        return next
      })
    },
    [snapshot, key],
  )

  return [value, setValue]
}
