import type { FilterMultiSelectOption } from '../components/FilterMultiSelect.tsx'

// The my-list genre filter's rules, kept apart from MyListPage so they can be
// run on their own (`node --experimental-strip-types`): nothing here imports
// at runtime, and the syntax is erasable (design D4 of
// add-genre-filter-and-polish-lists).

// An entry carries every ticked genre. True for nothing ticked, and false for
// an entry with no genres under any tick.
export function passesGenres(item: { genres: readonly string[] }, ticked: readonly string[]): boolean {
  return ticked.every((genre) => item.genres.includes(genre))
}

export type GenreBoardInput = {
  // How many of the base entries carry each genre. The base is the entries
  // every other control leaves listed — and not the genre filter's own ticks,
  // so ticking never changes what is counted here.
  counts: ReadonlyMap<string, number>
  // How many entries the base holds.
  baseTotal: number
  // The genres carried by at least one entry still shown: the base narrowed
  // by the ticks too.
  matchedGenres: ReadonlySet<string>
  ticked: readonly string[]
}

// The board's options, alphabetical. A genre is on it when some but not all
// of the base carry it — ticking one nobody carries returns nothing, and
// ticking one everybody carries changes nothing — plus every ticked genre,
// even one the base no longer carries, so a restriction in force can always
// be lifted. Because the base ignores the ticks, the board's contents and
// order never change while genres are ticked; only which are greyed out does.
// A genre is greyed out (disabled) when it is not ticked and no entry still
// shown carries it, which makes it a dead end: ticked alongside the current
// ticks it would leave nothing. With nothing ticked every shown entry is the
// base, so nothing is greyed out.
export function buildGenreOptions({ counts, baseTotal, matchedGenres, ticked }: GenreBoardInput): FilterMultiSelectOption[] {
  const tickedSet = new Set(ticked)
  const genres: string[] = []
  for (const [genre, count] of counts) {
    if ((count > 0 && count < baseTotal) || tickedSet.has(genre)) genres.push(genre)
  }
  for (const genre of tickedSet) {
    if (!counts.has(genre)) genres.push(genre)
  }
  genres.sort((a, b) => a.localeCompare(b, undefined, { sensitivity: 'base' }))
  return genres.map((genre) => ({
    value: genre,
    label: genre,
    disabled: !tickedSet.has(genre) && !matchedGenres.has(genre),
  }))
}
