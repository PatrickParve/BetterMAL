import { useEffect, type Dispatch, type RefObject, type SetStateAction } from 'react'

// The IntersectionObserver behind every progressively revealed list (Season,
// Year, Series browser, Search, My list): as the sentinel below the list
// scrolls into view, reveal another `step` of what is already loaded — no
// network call. Slicing to the count is what keeps a several-hundred-card
// list from rendering, and painting every poster, in one go.
//
// It only ever adds to the count. It never steps an empty list, and never
// lowers the count because the list was empty or short when the sentinel was
// in view: while a list is still loading its grid is empty, the sentinel is
// on screen, and clamping to the empty list's length used to collapse the
// count to 0, so the list arrived showing nothing until the sentinel fired
// again (page-load-states, "never reveals less than its first screenful").
// A count above `total` is harmless, since the caller slices.
//
// Re-observes when `total` or `setVisibleCount` changes, so a list that has
// just arrived while the sentinel is still in view is stepped straight away.
export function useScrollReveal(
  sentinelRef: RefObject<HTMLElement | null>,
  total: number,
  step: number,
  setVisibleCount: Dispatch<SetStateAction<number>>,
) {
  useEffect(() => {
    const node = sentinelRef.current
    if (!node) return

    const observer = new IntersectionObserver((entries) => {
      if (entries[0]?.isIntersecting) {
        setVisibleCount((prev) => (total === 0 || prev >= total ? prev : prev + step))
      }
    })
    observer.observe(node)
    return () => observer.disconnect()
  }, [sentinelRef, total, step, setVisibleCount])
}
