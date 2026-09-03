import { useCallback, useRef } from 'react'
import { usePageState } from '../state/PageStateContext.tsx'
import * as pageStateStore from '../state/pageStateStore.ts'

export type ScrollAxis = 'horizontal' | 'vertical'

const AXIS_PROPS = {
  horizontal: { offset: 'scrollLeft', size: 'scrollWidth', client: 'clientWidth' },
  vertical: { offset: 'scrollTop', size: 'scrollHeight', client: 'clientHeight' },
} as const

// Record-and-restore for a named in-page scroll container: attaches a
// `scroll` listener that writes the offset through to the snapshot, and, on
// a restore, re-applies the recorded offset until it's reachable or the user
// scrolls the container themselves. Returns a ref callback to attach to the
// scroll container; callers that also need drag-to-scroll or other wiring on
// the same node compose it with their own ref callback.
export function useRestorableScroll(restoreKey: string, axis: ScrollAxis) {
  const { key, isRestore, snapshot } = usePageState()

  // Read by the ref callback below, which — unlike an effect keyed on
  // `restoreKey` — only runs when React actually attaches or detaches the
  // container's DOM node. A container whose section is still loading on
  // first mount attaches that node later, on a render an effect with an
  // unrelated dependency list would never repeat for; refs sidestep that by
  // always being current whenever the callback next fires.
  const keyRef = useRef(key)
  keyRef.current = key
  const restoreKeyRef = useRef(restoreKey)
  restoreKeyRef.current = restoreKey
  const axisRef = useRef(axis)
  axisRef.current = axis
  const isRestoreRef = useRef(isRestore)
  isRestoreRef.current = isRestore
  const snapshotRef = useRef(snapshot)
  snapshotRef.current = snapshot

  return useCallback((el: HTMLElement | null) => {
    if (!el) return
    const node = el
    const { offset: offsetProp, size: sizeProp, client: clientProp } = AXIS_PROPS[axisRef.current]

    // Recorded synchronously, same reasoning as the page's own scroll
    // position (useScrollRestoration decision 1).
    function onScroll() {
      pageStateStore.putScrollerOffset(keyRef.current, restoreKeyRef.current, node[offsetProp])
    }
    node.addEventListener('scroll', onScroll, { passive: true })

    // Applied on a restore only, once the container's content is laid out —
    // usePageData seeds restored data synchronously, so on the first paint
    // of a restore the content is already in the DOM and the offset is
    // reachable. A cheap analogue of the page-level retry loop, without a
    // timer: a ResizeObserver re-applies the offset if the container was too
    // small to reach it when first measured, and stops for good once it's
    // reached or the user scrolls the container themselves. On a fresh visit
    // this does nothing, leaving the container at its start.
    let observer: ResizeObserver | undefined
    let onUserInput: (() => void) | undefined

    if (isRestoreRef.current) {
      const target = snapshotRef.current.scrollers.get(restoreKeyRef.current)
      if (target !== undefined) {
        let done = false
        let stopped = false

        const attempt = () => {
          if (done || stopped) return
          const reachable = node[sizeProp] - node[clientProp] >= target
          node[offsetProp] = target
          if (reachable) done = true
        }
        attempt()

        observer = new ResizeObserver(() => attempt())
        observer.observe(node)

        onUserInput = () => {
          stopped = true
        }
        node.addEventListener('wheel', onUserInput, { passive: true })
        node.addEventListener('touchstart', onUserInput, { passive: true })
        node.addEventListener('mousedown', onUserInput)
      }
    }

    return () => {
      node.removeEventListener('scroll', onScroll)
      observer?.disconnect()
      if (onUserInput) {
        node.removeEventListener('wheel', onUserInput)
        node.removeEventListener('touchstart', onUserInput)
        node.removeEventListener('mousedown', onUserInput)
      }
    }
  }, [])
}
