// Placing one anime by halving among a list of others (add-this-or-that-ranking
// design.md D1 and D7). The ranking pop-up's placement view runs on this: it holds
// the score's rows in the order the editor shows them, unsaved reorders included,
// so the placement has to compare against that list and not the stored one.
//
// A placement is a range [lo, hi) of the places the anime could still take among
// the other rows: place `i` is "just above row i", and place `others.length` is
// "below the last row". Each pick narrows the range to the half above or below
// the middle row, until one place is left. The result is that place, which is
// also the index the anime ends up at once it is moved there.

export type PlacementOutcome = 'above' | 'below' | 'cant-decide'

export type Placement = {
  lo: number
  hi: number
  /** The ranges before each pick, last first out, so Undo is a pop. */
  history: ReadonlyArray<readonly [number, number]>
}

export function startPlacement(otherCount: number): Placement {
  return { lo: 0, hi: otherCount, history: [] }
}

/**
 * The row the anime is shown with: the middle of what is left. When the range holds an even
 * count the middle is the lower-placed of its two (index n / 2 in integer division, counted
 * from the range's top).
 */
export function pivotIndex(lo: number, hi: number): number {
  return lo + Math.floor((hi - lo) / 2)
}

/** One place left (or none to choose between): the anime goes at `placeOf`. */
export function isPlaced(placement: Placement): boolean {
  return placement.lo >= placement.hi
}

/** Valid once the placement is finished: the index among the other rows the anime goes in at. */
export function placeOf(placement: Placement): number {
  return placement.lo
}

/**
 * One pick. `above` means the anime ranks higher than the pivot row, so the range keeps the
 * half above it; `below` keeps the half below; `cant-decide` places it directly below the pivot.
 */
export function choose(placement: Placement, outcome: PlacementOutcome): Placement {
  const pivot = pivotIndex(placement.lo, placement.hi)
  const history = [...placement.history, [placement.lo, placement.hi] as const]
  if (outcome === 'above') return { lo: placement.lo, hi: pivot, history }
  if (outcome === 'below') return { lo: pivot + 1, hi: placement.hi, history }
  return { lo: pivot + 1, hi: pivot + 1, history }
}

/** Takes back the last pick; the same placement when there is none. */
export function undoChoice(placement: Placement): Placement {
  const last = placement.history[placement.history.length - 1]
  if (last === undefined) return placement
  return { lo: last[0], hi: last[1], history: placement.history.slice(0, -1) }
}

/** ⌈log₂(n + 1)⌉ for the n places still open: the most picks this placement can still take. */
export function picksLeftAtMost(placement: Placement): number {
  return 32 - Math.clz32(Math.max(0, placement.hi - placement.lo))
}

/**
 * The rows the next pair could show whichever way the pick goes (at most two), so their
 * pictures can start downloading while the current pair is on screen.
 */
export function nextPivotIndexes(placement: Placement): number[] {
  if (isPlaced(placement)) return []
  const pivot = pivotIndex(placement.lo, placement.hi)
  const next: number[] = []
  if (pivot > placement.lo) next.push(pivotIndex(placement.lo, pivot))
  if (placement.hi > pivot + 1) next.push(pivotIndex(pivot + 1, placement.hi))
  return next
}
