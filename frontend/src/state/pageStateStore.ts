export interface PageSnapshot {
  data: Map<string, unknown>
  view: Map<string, unknown>
  scrollY: number
}

// Bounds how many history entries' worth of state stay pinned in memory.
// Long browsing sessions accumulate history entries and each snapshot pins a
// page's worth of DTOs; 30 comfortably covers realistic back-navigation depth.
const MAX_ENTRIES = 30

// Keyed by React Router's `location.key` (unique per history entry), not
// pathname — two entries for the same path hold separate snapshots and
// cannot leak into each other. Map iteration is insertion-ordered, so the
// first key is always the oldest for eviction purposes.
const snapshots = new Map<string, PageSnapshot>()

function evictOldest(): void {
  while (snapshots.size > MAX_ENTRIES) {
    const oldestKey = snapshots.keys().next().value
    if (oldestKey === undefined) break
    snapshots.delete(oldestKey)
  }
}

export function get(key: string): PageSnapshot | undefined {
  return snapshots.get(key)
}

export function ensure(key: string): PageSnapshot {
  let snapshot = snapshots.get(key)
  if (!snapshot) {
    snapshot = { data: new Map(), view: new Map(), scrollY: 0 }
    snapshots.set(key, snapshot)
    evictOldest()
  }
  return snapshot
}

export function putData(key: string, dataKey: string, value: unknown): void {
  ensure(key).data.set(dataKey, value)
}

export function putView(key: string, viewKey: string, value: unknown): void {
  ensure(key).view.set(viewKey, value)
}

export function putScroll(key: string, scrollY: number): void {
  ensure(key).scrollY = scrollY
}
