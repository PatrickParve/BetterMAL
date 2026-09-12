import { useCallback, useEffect, useRef, useState, type Dispatch, type SetStateAction } from 'react'
import { usePageState } from '../state/PageStateContext.tsx'

export interface UsePageDataResult<T> {
  data: T | null
  loading: boolean
  setData: Dispatch<SetStateAction<T | null>>
  reload: () => Promise<void>
}

// One call per resource a page loads. `key` names the resource within the
// page and must include any route/query parameter the data depends on, so a
// parameterised page (anime detail, search, season) loads fresh data when
// its parameter changes even though React Router keeps the component mounted
// across that change.
//
// On a restore (this key's data is in the current history entry's snapshot),
// state seeds from the snapshot with `loading: false` and `load` re-runs
// silently in the background, replacing the data on success and leaving it
// alone on failure. Otherwise this is a fresh load: `loading` is true until
// `load` resolves.
export function usePageData<T>(key: string, load: () => Promise<T>): UsePageDataResult<T> {
  const { isRestore, snapshot } = usePageState()

  const loadRef = useRef(load)
  loadRef.current = load

  // Guards against out-of-order resolution: a key can change again (a new
  // season, a fast-typed search) before an in-flight load for the previous
  // key has resolved, and that stale response must not clobber what's
  // current.
  const generationRef = useRef(0)

  // Tracks which generation is currently responsible for clearing `loading`
  // — separate from `generationRef` (which only gates whether a result gets
  // applied to `data`). A page's own PageStateContext key changes on every
  // URL update (e.g. a filter toggle via setSearchParams), which can start a
  // second, competing `runLoad(true)` here while an explicit `reload()`
  // (showLoading: false) from the page is also in flight for the same
  // change. Without this, whichever call finishes first would find itself
  // no longer "the latest" generation and skip clearing `loading`, leaving
  // it stuck true forever with nothing left to reset it.
  const loadingGenerationRef = useRef<number | null>(null)

  // Tracks the key the data currently held belongs to, so the load effect
  // below can tell a fresh history entry for an unchanged key (e.g. a filter
  // written via setSearchParams) apart from a key that genuinely needs
  // loading.
  const loadedKeyRef = useRef<string | null>(null)

  const isSeeded = isRestore && snapshot.data.has(key)
  const seededValue = () => (isSeeded ? (snapshot.data.get(key) as T) : null)

  const [renderedKey, setRenderedKey] = useState(key)
  const [data, setDataState] = useState<T | null>(seededValue)
  const [loading, setLoading] = useState(() => !isSeeded)

  // `key` can change without the component remounting — e.g. navigating from
  // one anime's detail page to another's keeps the same AnimeDetailPage
  // instance mounted. Adjust state synchronously during render (rather than
  // in an effect) so a restore never paints an empty frame first.
  if (key !== renderedKey) {
    setRenderedKey(key)
    setDataState(seededValue())
    setLoading(!isSeeded)
  }

  const setData = useCallback<Dispatch<SetStateAction<T | null>>>(
    (update) => {
      setDataState((prev) => {
        const next = typeof update === 'function' ? (update as (prev: T | null) => T | null)(prev) : update
        loadedKeyRef.current = key
        snapshot.data.set(key, next)
        return next
      })
    },
    [snapshot, key],
  )

  const runLoad = useCallback(
    (showLoading: boolean) => {
      const generation = ++generationRef.current
      if (showLoading) {
        loadingGenerationRef.current = generation
        setLoading(true)
      }
      return loadRef
        .current()
        .then((result) => {
          if (generationRef.current !== generation) return
          loadedKeyRef.current = key
          snapshot.data.set(key, result)
          setDataState(result)
        })
        .catch(() => {
          // Restored (or previously loaded) data stays on screen; a fresh
          // load with nothing to show simply stays empty.
        })
        .finally(() => {
          // Only the call currently holding the loading flag clears it — see
          // loadingGenerationRef above.
          if (loadingGenerationRef.current === generation) {
            loadingGenerationRef.current = null
            setLoading(false)
          }
        })
    },
    [snapshot, key],
  )

  const reload = useCallback(() => runLoad(false), [runLoad])

  useEffect(() => {
    // A URL change that leaves this resource's key untouched (e.g. a filter
    // toggled via setSearchParams) still creates a new history entry, and
    // therefore a new snapshot — but the key's contract already covers every
    // parameter the data depends on, so an unchanged key means this change
    // isn't one of them. Skip the reload and carry the data already in hand
    // into the new entry's snapshot, so a later back/forward restore of it
    // is still seeded. A restore keeps its silent background refresh
    // regardless.
    if (!isRestore && key === loadedKeyRef.current && data !== null) {
      snapshot.data.set(key, data)
      return
    }
    runLoad(!isSeeded)
    // Re-runs only when the resource key or restore context changes, not on
    // every render — `loadRef` carries the latest closure regardless. `data`
    // is read above but deliberately left out of the deps: it changes on
    // every load, and depending on it would refire this effect right after
    // committing that load's own result.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [key, isRestore, snapshot])

  return { data, loading, setData, reload }
}
