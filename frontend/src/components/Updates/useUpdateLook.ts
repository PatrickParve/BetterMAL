import { useRef } from 'react'
import type { AnimeUpdateDto } from '../../api/types.ts'

// The set of ids to mark New for the life of the calling component — one
// "look" (store-seen-updates-on-server design.md D6). Two refs, never
// cleared: the ids already shown in this look, and the ids marked New. On
// each render, any id in `items` not yet shown is recorded as shown and, if
// unseen, added to the New set. Nothing is ever removed, so:
// - a refresh can't remove or add a marking on a card already shown;
// - a card arriving mid-look is judged when it first appears;
// - a card seen during the look stays in the set.
// Accumulating during render (not in an effect) is idempotent, so
// StrictMode's double render is safe. The look's lifetime comes from the
// calling component's own mount, so a component rendered only while its
// panel is open (UpdatesDropdown, UpdatesHistoryOverlay) starts a fresh look
// on every open by construction.
export function useUpdateLook(items: AnimeUpdateDto[], isSeen: (item: AnimeUpdateDto) => boolean): Set<number> {
  const shownRef = useRef<Set<number>>(new Set())
  const newRef = useRef<Set<number>>(new Set())

  for (const item of items) {
    if (shownRef.current.has(item.id)) continue
    shownRef.current.add(item.id)
    if (!isSeen(item)) newRef.current.add(item.id)
  }

  return newRef.current
}
