import { useCallback, useEffect, useState } from 'react'

// Must match the caller's own list gap — UpdatesMenu.css's
// `.updates-menu__list` and UpdatesHistoryOverlay.css's
// `.updates-history__list` both use this hook and are both kept at 8px.
const LIST_GAP_PX = 8
const VIEWPORT_MARGIN_PX = 16
// How many of the newest cards set the opening height, in the dropdown and
// (design.md D5) the history overlay alike.
export const VISIBLE_CARD_CAP = 3

// The dropdown's opening height is measured from the newest cards as
// actually rendered rather than multiplied from an assumed row height —
// with heights that follow their content (design.md D4) there's no constant
// to multiply, which is exactly what forced the history overlay's rows to a
// fixed height in the first place (`calc(5 * (row-h + 2px) + 4 * 6px)`,
// design.md D5). A ResizeObserver, not one measurement on open, because the
// cards' remote pictures have no known size until they load, so a
// first-frame measurement is wrong for every card whose image hasn't
// arrived yet.
//
// The list node is tracked as state via a callback ref rather than a plain
// `useRef` read inside an effect keyed on `itemCount`: the items usually
// finish loading before the dropdown is ever opened, so by the time
// `itemCount` last changed the `<ul>` didn't exist yet and the effect bailed
// out with nothing observed. A callback ref re-fires exactly when the `<ul>`
// itself mounts (every time the dropdown opens), which is what needs to
// retrigger the measurement.
export function useCappedCardHeight(itemCount: number): {
  listRef: (node: HTMLUListElement | null) => void
  maxHeight: number | undefined
  listNode: HTMLUListElement | null
} {
  const [node, setNode] = useState<HTMLUListElement | null>(null)
  const [maxHeight, setMaxHeight] = useState<number | undefined>(undefined)
  const listRef = useCallback((el: HTMLUListElement | null) => setNode(el), [])

  useEffect(() => {
    if (!node) return

    function recompute() {
      if (!node) return
      const cards = Array.from(node.children) as HTMLElement[]
      // Fewer cards than the cap: no cap at all, the list is as tall as it needs.
      if (cards.length < VISIBLE_CARD_CAP) {
        setMaxHeight(undefined)
        return
      }

      const visible = cards.slice(0, VISIBLE_CARD_CAP)
      const cardsHeight = visible.reduce((sum, card) => sum + card.getBoundingClientRect().height, 0)
      const natural = cardsHeight + LIST_GAP_PX * (visible.length - 1)

      const top = node.getBoundingClientRect().top
      const viewportLimit = window.innerHeight - top - VIEWPORT_MARGIN_PX

      setMaxHeight(Math.max(0, Math.min(natural, viewportLimit)))
    }

    recompute()

    const observer = new ResizeObserver(recompute)
    Array.from(node.children)
      .slice(0, VISIBLE_CARD_CAP)
      .forEach((child) => observer.observe(child))

    window.addEventListener('resize', recompute)
    return () => {
      observer.disconnect()
      window.removeEventListener('resize', recompute)
    }
  }, [node, itemCount])

  return { listRef, maxHeight, listNode: node }
}
