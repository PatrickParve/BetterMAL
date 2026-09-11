import { useCallback, useEffect, useState } from 'react'
import { getRecentUpdates } from '../../api/client.ts'
import type { AnimeUpdateDto } from '../../api/types.ts'

// 10 minutes, matching MetadataRefreshBackgroundService — the background job
// that produces most updates. Polling faster would just re-check a window
// that hasn't moved.
const POLL_INTERVAL_MS = 10 * 60 * 1000

// Fetches on mount, on a visible-tab poll, on window focus, and when the
// tab becomes visible — the two extra checks mean an update seen in one
// browser clears the bell in another as soon as I switch to it
// (store-seen-updates-on-server design.md D10), without waiting for the
// regular poll. Focus and visibilitychange often fire together, but
// fetchRaw joins identical in-flight GETs, so that costs one request. The
// menu also calls refresh() itself on open. The interval-skipped-while-
// hidden pattern is ConnectionStatusNotice's own precedent for a background
// poll that shouldn't run against a tab nobody can see.
export function useRecentUpdates() {
  const [items, setItems] = useState<AnimeUpdateDto[]>([])

  const refresh = useCallback((): Promise<void> => {
    return getRecentUpdates()
      .then(setItems)
      .catch(() => {
        // Keeps the last-known list; the next poll, focus or open tries again.
      })
  }, [])

  useEffect(() => {
    refresh()

    const interval = setInterval(() => {
      if (document.visibilityState === 'visible') refresh()
    }, POLL_INTERVAL_MS)

    function handleFocus() {
      refresh()
    }
    function handleVisibilityChange() {
      if (document.visibilityState === 'visible') refresh()
    }

    window.addEventListener('focus', handleFocus)
    document.addEventListener('visibilitychange', handleVisibilityChange)

    return () => {
      clearInterval(interval)
      window.removeEventListener('focus', handleFocus)
      document.removeEventListener('visibilitychange', handleVisibilityChange)
    }
  }, [refresh])

  return { items, refresh }
}
