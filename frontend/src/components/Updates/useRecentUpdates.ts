import { useCallback, useEffect, useState } from 'react'
import { getRecentUpdates } from '../../api/client.ts'
import type { AnimeUpdateDto } from '../../api/types.ts'

// 10 minutes, matching MetadataRefreshBackgroundService — the background job
// that produces most updates. Polling faster would just re-check a window
// that hasn't moved (design.md D3).
const POLL_INTERVAL_MS = 10 * 60 * 1000

// Fetches on mount and on a visible-tab interval; the menu additionally
// calls refresh() itself on open, since mount and open are the two moments
// the data is about to be looked at (design.md D3). The interval-skipped-
// while-hidden pattern is ConnectionStatusNotice's own precedent for a
// background poll that shouldn't run against a tab nobody can see.
export function useRecentUpdates() {
  const [items, setItems] = useState<AnimeUpdateDto[]>([])

  // Resolves with the fetched list (or undefined on failure) rather than
  // void, so a caller that needs the data the moment it lands — the menu
  // marking it seen — reads it straight from the resolved value instead of
  // racing the `items` state update (design.md D2).
  const refresh = useCallback((): Promise<AnimeUpdateDto[] | undefined> => {
    return getRecentUpdates()
      .then((data) => {
        setItems(data)
        return data
      })
      .catch(() => {
        // Keeps the last-known list; the next poll or open tries again.
        return undefined
      })
  }, [])

  useEffect(() => {
    refresh()

    const interval = setInterval(() => {
      if (document.visibilityState === 'visible') refresh()
    }, POLL_INTERVAL_MS)
    return () => clearInterval(interval)
  }, [refresh])

  return { items, refresh }
}
