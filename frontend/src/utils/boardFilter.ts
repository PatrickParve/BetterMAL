import type { FilterMultiSelectOption } from '../components/FilterMultiSelect.tsx'

// The rules my list's two board filters share, the Genre filter and the Studio
// filter: an entry carries several values (genres, studios), and ticking several
// keeps the entries that carry every one of them. Kept apart from MyListPage so
// they can be run on their own (`node --experimental-strip-types`): nothing here
// imports at runtime, and the syntax is erasable (design D4 of
// add-genre-filter-and-polish-lists; the studio filter reuses it unchanged).

// An entry carries every ticked value. True for nothing ticked, and false for
// an entry that carries none under any tick.
export function passesTicked(carried: readonly string[], ticked: readonly string[]): boolean {
  return ticked.every((value) => carried.includes(value))
}

export type BoardInput = {
  // How many of the base entries carry each value. The base is the entries
  // every other control leaves listed — and not this filter's own ticks, so
  // ticking never changes what is counted here.
  counts: ReadonlyMap<string, number>
  // How many entries the base holds.
  baseTotal: number
  // The values carried by at least one entry still shown: the base narrowed
  // by the ticks too.
  matched: ReadonlySet<string>
  ticked: readonly string[]
}

// The board's options, alphabetical. A value is on it when some but not all
// of the base carry it — ticking one nobody carries returns nothing, and
// ticking one everybody carries changes nothing — plus every ticked value,
// even one the base no longer carries, so a restriction in force can always
// be lifted. Because the base ignores the ticks, the board's contents and
// order never change while values are ticked; only which are greyed out does.
// A value is greyed out (disabled) when it is not ticked and no entry still
// shown carries it, which makes it a dead end: ticked alongside the current
// ticks it would leave nothing. With nothing ticked every shown entry is the
// base, so nothing is greyed out.
export function buildBoardOptions({ counts, baseTotal, matched, ticked }: BoardInput): FilterMultiSelectOption[] {
  const tickedSet = new Set(ticked)
  const values: string[] = []
  for (const [value, count] of counts) {
    if ((count > 0 && count < baseTotal) || tickedSet.has(value)) values.push(value)
  }
  for (const value of tickedSet) {
    if (!counts.has(value)) values.push(value)
  }
  values.sort((a, b) => a.localeCompare(b, undefined, { sensitivity: 'base' }))
  return values.map((value) => ({
    value,
    label: value,
    disabled: !tickedSet.has(value) && !matched.has(value),
  }))
}
