import { useEffect, useMemo, useState } from 'react'
import { useLocation, useNavigationType } from 'react-router-dom'

/**
 * The song a visit to an anime's page is to open at: AnimeThemes' id for the theme, and the
 * visit it belongs to (the location key). The key is what makes it once per visit: the page
 * stays mounted from /anime/1 to /anime/2, so the anime alone can't tell two visits apart.
 */
export type OpenAt = { themeId: number; key: string }

// Keys that move the page, the list useScrollRestoration and navbarReveal each hold.
export const SCROLL_KEYS = new Set(['ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight', 'PageUp', 'PageDown', 'Home', 'End', ' '])

/**
 * The theme id a navigation carried in its router state, or null when it carried none or
 * something else. Picking a song in the search page's OP/ED dropdown navigates with
 * `{ themeId }`; no other way into an anime's page sets it.
 */
export function themeIdFromState(state: unknown): number | null {
  if (typeof state !== 'object' || state === null) return null
  const themeId = (state as { themeId?: unknown }).themeId
  return typeof themeId === 'number' && Number.isInteger(themeId) ? themeId : null
}

/**
 * Whether this visit to an anime's page was opened by picking a song, and which
 * (improve-search-page design.md D12, anime-themes "The anime page can open at one
 * song"). Null for every other way in:
 *   - only a push counts. Back, Forward and a reload are all POP, and restore the page as
 *     usual with nothing playing and no move: the song is the visit's, not the page's,
 *     and the state a reload keeps in the history entry must not play it again;
 *   - the state must carry a theme id, which a card, a row, an Anime search result and
 *     the anime's name on an OP/ED result never set;
 *   - I must not have scrolled before the page's themes rendered. Wheel, touch and the
 *     keys that move the page count, from the moment the visit began, which is why this
 *     lives on the page and not in the themes section, which only mounts once the page's
 *     read has finished. Nothing starts then either, so a song never starts off screen.
 * The page hands this to its themes section, which does the opening.
 */
export function useOpenAtSong(): OpenAt | null {
  const location = useLocation()
  const navigationType = useNavigationType()
  const themeId = navigationType === 'PUSH' ? themeIdFromState(location.state) : null
  // The visit I scrolled in. A key rather than a flag, so a new visit starts unscrolled
  // without anything having to reset it.
  const [scrolledKey, setScrolledKey] = useState<string | null>(null)

  useEffect(() => {
    if (themeId === null) return
    const visit = location.key
    const markScrolled = () => setScrolledKey(visit)
    const onKeyDown = (event: KeyboardEvent) => {
      if (SCROLL_KEYS.has(event.key)) markScrolled()
    }
    window.addEventListener('wheel', markScrolled, { passive: true })
    window.addEventListener('touchstart', markScrolled, { passive: true })
    window.addEventListener('keydown', onKeyDown)
    return () => {
      window.removeEventListener('wheel', markScrolled)
      window.removeEventListener('touchstart', markScrolled)
      window.removeEventListener('keydown', onKeyDown)
    }
  }, [themeId, location.key])

  return useMemo(
    () => (themeId !== null && scrolledKey !== location.key ? { themeId, key: location.key } : null),
    [themeId, scrolledKey, location.key],
  )
}
