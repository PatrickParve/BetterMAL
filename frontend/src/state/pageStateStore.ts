export interface PageSnapshot {
  data: Map<string, unknown>
  view: Map<string, unknown>
  scrollY: number
  strips: Map<string, number>
}

// Bounds how many history entries' worth of state stay pinned in memory.
// Long browsing sessions accumulate history entries and each snapshot pins a
// page's worth of DTOs. Eviction is least-recently-*used*, not
// oldest-created (see below), so this only has to cover how deep a session's
// *active* back-navigation gets, not its total length — 50 is comfortable
// headroom for that.
const MAX_ENTRIES = 50

// Keyed by React Router's `location.key` (unique per history entry), not
// pathname — two entries for the same path hold separate snapshots and
// cannot leak into each other. `get`/`ensure` re-insert the snapshot they
// return, so Map iteration order tracks recency of *use* rather than of
// creation — a page one step back in the history stack that the user keeps
// returning to stays pinned instead of aging out from entries created after
// it but never revisited.
const snapshots = new Map<string, PageSnapshot>()

function evictOldest(): void {
  while (snapshots.size > MAX_ENTRIES) {
    const oldestKey = snapshots.keys().next().value
    if (oldestKey === undefined) break
    snapshots.delete(oldestKey)
  }
}

function touch(key: string, snapshot: PageSnapshot): void {
  snapshots.delete(key)
  snapshots.set(key, snapshot)
}

export function get(key: string): PageSnapshot | undefined {
  const snapshot = snapshots.get(key)
  if (snapshot) touch(key, snapshot)
  return snapshot
}

export function ensure(key: string): PageSnapshot {
  let snapshot = snapshots.get(key)
  if (!snapshot) {
    snapshot = { data: new Map(), view: new Map(), scrollY: 0, strips: new Map() }
    snapshots.set(key, snapshot)
    evictOldest()
  } else {
    touch(key, snapshot)
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

export function putStripScroll(key: string, stripKey: string, offset: number): void {
  ensure(key).strips.set(stripKey, offset)
}
