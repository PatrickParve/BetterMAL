import type { AnimeThemeDto } from '../../api/types.ts'
import { useRestorableState } from '../../hooks/useRestorableState.ts'
import type { ThemeGroup } from './AnimeThemesSection.tsx'
import { searchWords, themeMatches } from './themeSearch.ts'

/** One group as the box shows it. */
export type ThemesViewGroup = {
  group: ThemeGroup
  open: boolean
  /** The songs the search keeps, all of them with no search: what the heading counts. */
  matching: AnimeThemeDto[]
  /** The rows on screen: the matching songs while the group is open, none while it is closed. */
  rows: AnimeThemeDto[]
}

export type ThemesView = {
  /** The groups to show. A search leaves out the groups it has no match in. */
  groups: ThemesViewGroup[]
  query: string
  setQuery: (query: string) => void
  /** Whether a search is in force: a query with at least one word, so spaces alone are no search. */
  searching: boolean
  /** A search is in force and no song matches it. */
  noMatch: boolean
  /** Opens a closed group and closes an open one, and changes no other group. */
  toggle: (animeId: number) => void
  /** "Expand all" or "Collapse all"; "Expand" or "Collapse" with a single group shown. */
  allLabel: string
  /** Opens every group shown, or closes them all once every one is open. */
  toggleAll: () => void
}

// A copy of `set` with one id taken out if it was there and put in if it was not.
function flipped(set: ReadonlySet<number>, animeId: number): Set<number> {
  const next = new Set(set)
  if (!next.delete(animeId)) next.add(animeId)
  return next
}

// A copy of `set` with every id put in, or every id taken out.
function withIds(set: ReadonlySet<number>, ids: readonly number[], present: boolean): Set<number> {
  const next = new Set(set)
  for (const id of ids) {
    if (present) next.add(id)
    else next.delete(id)
  }
  return next
}

// The state behind the series page's openings and endings box (series-page "The series
// page lists the main line's openings and endings", "The series page's openings and
// endings can be searched" and "The openings and endings box's view state is restored
// with the page"). The box owns it because the box must see every row to stop a song
// whose row goes: the groups arrive whole and this decides what shows.
//
// Three restorable values, under keys that clash with none of the page's own. Snapshots
// are per history entry, so two series never share them:
//   themesOpenGroups      the anime ids of the open groups. A missing id is a closed
//                         group, so a fresh visit is all closed without seeding anything,
//                         and an id the page no longer shows is ignored.
//   themesQuery           the text in the search field.
//   themesClosedInSearch  the anime ids closed while a search is in force.
//
// A search is a view of its own and gives the earlier one back. While a query has a
// word, only the groups with a matching song are shown, each open unless its id is in
// themesClosedInSearch, and the headings and the all-control act on that set, never on
// themesOpenGroups. So the first letter doesn't open the groups that were closed, what
// I close stays closed while I keep typing, and when the query has no word again the set
// is emptied in the same update and the groups follow themesOpenGroups, each as it was.
//
// With `enabled` off (the anime page) every group is open with all its rows, there is
// no search, and nothing is written.
export function useThemesView(groups: readonly ThemeGroup[], enabled: boolean): ThemesView {
  const [openGroups, setOpenGroups] = useRestorableState<Set<number>>('themesOpenGroups', new Set())
  const [storedQuery, setStoredQuery] = useRestorableState('themesQuery', '')
  const [closedInSearch, setClosedInSearch] = useRestorableState<Set<number>>('themesClosedInSearch', new Set())

  const query = enabled ? storedQuery : ''
  const words = searchWords(query)
  const searching = words.length > 0

  const shown: ThemesViewGroup[] = []
  for (const group of groups) {
    const matching = searching ? group.themes.filter((theme) => themeMatches(theme, words)) : group.themes
    if (matching.length === 0) continue
    const open = !enabled || (searching ? !closedInSearch.has(group.animeId) : openGroups.has(group.animeId))
    shown.push({ group, open, matching, rows: open ? matching : [] })
  }

  const everyOpen = shown.length > 0 && shown.every((entry) => entry.open)
  const allLabel = (everyOpen ? 'Collapse' : 'Expand') + (shown.length === 1 ? '' : ' all')

  function setQuery(next: string) {
    if (!enabled) return
    setStoredQuery(next)
    if (searchWords(next).length === 0) setClosedInSearch((prev) => (prev.size === 0 ? prev : new Set()))
  }

  function toggle(animeId: number) {
    if (!enabled) return
    if (searching) setClosedInSearch((prev) => flipped(prev, animeId))
    else setOpenGroups((prev) => flipped(prev, animeId))
  }

  function toggleAll() {
    if (!enabled) return
    const ids = shown.map((entry) => entry.group.animeId)
    // Closing puts the ids into themesClosedInSearch and opening takes them out; with
    // no search it is the other way round, since there a group is open when it is in
    // themesOpenGroups.
    if (searching) setClosedInSearch((prev) => withIds(prev, ids, everyOpen))
    else setOpenGroups((prev) => withIds(prev, ids, !everyOpen))
  }

  return { groups: shown, query, setQuery, searching, noMatch: searching && shown.length === 0, toggle, allLabel, toggleAll }
}
