import { useSyncExternalStore } from 'react'
import { markUpdatesSeen } from '../../api/client.ts'
import type { AnimeUpdateDto } from '../../api/types.ts'

// One seen store per page, shared by the bell, the dropdown and the history
// overlay (store-seen-updates-on-server design.md D5) — module-level because
// a report made in one surface must clear the bell's dot and the other
// surface's marking, and the three live in separate components.

// Ids reported as seen in this page's lifetime, whether in flight or
// confirmed (optimistic — added the moment they're reported, not when the
// server answers). Never pruned: a refresh bringing seen: true makes the
// entry redundant, so the set holds at most the ids seen in one page
// lifetime.
const locallySeen = new Set<number>()

// Ids queued to report to the server but not yet sent.
const pendingBatch = new Set<number>()
let batchTimer: ReturnType<typeof setTimeout> | null = null
const BATCH_DELAY_MS = 500

let version = 0
const listeners = new Set<() => void>()

function notify() {
  version++
  listeners.forEach((listener) => listener())
}

export function isSeen(item: AnimeUpdateDto): boolean {
  return item.seen || locallySeen.has(item.id)
}

// Adds the id to locallySeen at once and notifies subscribers, so the dot
// and any New marking react immediately rather than waiting on the network.
// The first id queued in an empty batch starts a non-sliding timer: ids
// queued while it runs join the same batch, but don't reset the clock, so a
// long run of cards seen one after another still reaches the server at
// least every half second.
export function reportSeen(id: number) {
  if (locallySeen.has(id)) return // never report the same card twice
  locallySeen.add(id)
  pendingBatch.add(id)
  notify()

  if (batchTimer === null) {
    batchTimer = setTimeout(() => flushSeenReports(), BATCH_DELAY_MS)
  }
}

// Sends the pending batch immediately. A failure leaves those ids unseen
// again (removed from locallySeen, subscribers notified) with no notice —
// performFetch's own connection-status notice already covers an unreachable
// backend, and the tracker will report them again next time they're looked at.
export function flushSeenReports(options?: { keepalive?: boolean }) {
  if (batchTimer !== null) {
    clearTimeout(batchTimer)
    batchTimer = null
  }
  if (pendingBatch.size === 0) return

  const ids = Array.from(pendingBatch)
  pendingBatch.clear()

  markUpdatesSeen(ids, options).catch(() => {
    for (const id of ids) locallySeen.delete(id)
    notify()
  })
}

if (typeof window !== 'undefined') {
  window.addEventListener('pagehide', () => flushSeenReports({ keepalive: true }))

  // Retiring the per-browser high-water mark (store-seen-updates-on-server
  // design.md D10) now that seen state lives on the server. Can be dropped
  // once no browser holds the key.
  try {
    window.localStorage.removeItem('bettermal.updatesSeenAt')
  } catch {
    // storage can throw (private browsing, disabled storage) — nothing to do
  }
}

function subscribe(listener: () => void): () => void {
  listeners.add(listener)
  return () => listeners.delete(listener)
}

function getSnapshot(): number {
  return version
}

// Subscribes the calling component to the store's changes and hands back
// the shared isSeen/reportSeen, so a report made anywhere re-renders every
// subscriber (the bell included) without prop drilling.
export function useUpdatesSeen(): { isSeen: (item: AnimeUpdateDto) => boolean; reportSeen: (id: number) => void } {
  useSyncExternalStore(subscribe, getSnapshot)
  return { isSeen, reportSeen }
}
