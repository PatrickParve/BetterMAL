import { useRef } from 'react'
import { AIRING_STATUS_LABELS, type AiringStatus, type SortDirection, type SortKey } from '../utils/anime.ts'
import { FilterMultiSelect, type FilterMultiSelectOption } from './FilterMultiSelect.tsx'
import { StableLabel } from './StableLabel.tsx'
import './MyListControls.css'

// Widened (design.md decision 5 of polish-rewatch-more-and-filters) to carry
// a specific score value alongside the three original options — stored as
// the plain number string ("8"), not the `score-8` form the recap handoff's
// `focus` token uses; MyListPage's parseFocus converts between the two.
export type ScoreFilter = 'any' | 'rated' | 'unrated' | `${number}`

// Every non-airing sort key, in the order the primary and tiebreaker selects
// present them, split around where the Airing status optgroup goes (design
// D4) — right where the plain "Airing status" option used to sit.
const SORT_OPTIONS_BEFORE_AIRING: { value: SortKey; label: string }[] = [
  { value: 'alphabetical', label: 'Alphabetical' },
  { value: 'myScore', label: 'My score' },
  { value: 'malScore', label: 'MAL score' },
  { value: 'popularity', label: 'Popularity' },
  { value: 'episodesWatched', label: 'Episodes watched' },
  { value: 'progress', label: 'Progress' },
  { value: 'totalEpisodes', label: 'Total episodes' },
]
const SORT_OPTIONS_AFTER_AIRING: { value: SortKey; label: string }[] = [
  { value: 'type', label: 'Type' },
  { value: 'startDate', label: 'Start date' },
  { value: 'finishDate', label: 'Finish date' },
]

// The three statuses that can come first, in the cycle order
// compareByAiringStatus rotates through (utils/anime.ts AIRING_STATUS_CYCLE).
const AIRING_CHOICES: AiringStatus[] = ['finished_airing', 'currently_airing', 'not_yet_aired']

// A sort select's value encodes both the key and, for Airing status, which
// status comes first — so the primary and tiebreaker selects each stay a
// single native <select> with one selected <option>, rather than a select
// plus a conditional second control (design D4).
export type SortChoice = Exclude<SortKey, 'airingStatus'> | `airingStatus:${AiringStatus}`

export function toSortChoice(key: SortKey, first: AiringStatus): SortChoice {
  return key === 'airingStatus' ? (`airingStatus:${first}` as SortChoice) : (key as SortChoice)
}

export function fromSortChoice(choice: SortChoice): { key: SortKey; first: AiringStatus | null } {
  if (choice.startsWith('airingStatus:')) {
    return { key: 'airingStatus', first: choice.slice('airingStatus:'.length) as AiringStatus }
  }
  return { key: choice as SortKey, first: null }
}

// One <option>/<optgroup> run for a sort select: every non-airing key
// (minus `excludeKey`, so a select never offers its own current value as a
// tiebreaker target) with the Airing status choices inserted in their slot,
// omitted entirely when `includeAiringGroup` is false (design D4's "the
// tiebreaker offers none of the three Airing status choices" while Airing
// status is primary).
function renderSortOptions(excludeKey: SortKey | null, includeAiringGroup: boolean) {
  const before = SORT_OPTIONS_BEFORE_AIRING.filter((option) => option.value !== excludeKey)
  const after = SORT_OPTIONS_AFTER_AIRING.filter((option) => option.value !== excludeKey)
  return (
    <>
      {before.map((option) => (
        <option key={option.value} value={option.value}>
          {option.label}
        </option>
      ))}
      {includeAiringGroup && (
        <optgroup label="Airing status">
          {AIRING_CHOICES.map((status) => (
            <option key={status} value={`airingStatus:${status}`}>
              {AIRING_STATUS_LABELS[status]} first
            </option>
          ))}
        </optgroup>
      )}
      {after.map((option) => (
        <option key={option.value} value={option.value}>
          {option.label}
        </option>
      ))}
    </>
  )
}

// Direction button text, per key (design D5). Airing status isn't here — its
// direction is unavailable; MyListControls substitutes "Set by first status"
// for it directly rather than looking it up here.
const DIRECTION_LABELS: Partial<Record<SortKey, { natural: string; reversed: string }>> = {
  myScore: { natural: 'Highest first', reversed: 'Lowest first' },
  malScore: { natural: 'Highest first', reversed: 'Lowest first' },
  progress: { natural: 'Highest first', reversed: 'Lowest first' },
  episodesWatched: { natural: 'Most first', reversed: 'Fewest first' },
  totalEpisodes: { natural: 'Most first', reversed: 'Fewest first' },
  popularity: { natural: 'Most popular first', reversed: 'Least popular first' },
  alphabetical: { natural: 'A–Z', reversed: 'Z–A' },
  type: { natural: 'A–Z', reversed: 'Z–A' },
  startDate: { natural: 'Newest first', reversed: 'Oldest first' },
  finishDate: { natural: 'Newest first', reversed: 'Oldest first' },
}

const DIRECTION_UNAVAILABLE_LABEL = 'Set by first status'

// Every label the direction button could ever show, deduped — several keys
// share the same pair of labels (StableLabel needs one entry per distinct
// string, not one per key).
const DIRECTION_CANDIDATES = Array.from(
  new Set([
    ...Object.values(DIRECTION_LABELS).flatMap((pair) => [pair.natural, pair.reversed]),
    DIRECTION_UNAVAILABLE_LABEL,
  ]),
)

function directionLabel(key: SortKey, direction: SortDirection): string {
  if (key === 'airingStatus') return DIRECTION_UNAVAILABLE_LABEL
  const pair = DIRECTION_LABELS[key]
  if (!pair) return DIRECTION_UNAVAILABLE_LABEL
  return direction === 'natural' ? pair.natural : pair.reversed
}

export type MyListFiltersProps = {
  query: string
  onQueryChange: (value: string) => void
  typeFilter: string[] | null
  onTypeFilterChange: (value: string[] | null) => void
  airingFilter: string[] | null
  onAiringFilterChange: (value: string[] | null) => void
  scoreFilter: ScoreFilter
  onScoreFilterChange: (value: ScoreFilter) => void
}

export type MyListSortProps = {
  sort: SortKey
  sortDirection: SortDirection
  sortThen: SortKey | null
  groupByStatus: boolean
  airingStatusFirst: AiringStatus
  onSortChange: (choice: SortChoice) => void
  onThenChange: (choice: SortChoice | null) => void
  onDirectionToggle: () => void
  onGroupByStatusChange: (value: boolean) => void
}

type MyListControlsProps = {
  filters: MyListFiltersProps
  sort: MyListSortProps
  typeOptions: FilterMultiSelectOption[]
  airingOptions: FilterMultiSelectOption[]
  // Only the score values at least one list entry actually has (MyListPage's
  // filterOptions) — so the select never offers a score nothing was ever
  // rated, matching the Type/Airing options beside it.
  scoreOptions: number[]
}

function FindGlyph() {
  return (
    <svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true">
      <circle cx="10.5" cy="10.5" r="6.5" />
      <line x1="15.5" y1="15.5" x2="21" y2="21" />
    </svg>
  )
}

// Two labelled groups — Filter and Sort — laid out as a two-column grid of
// label + wrapping controls row (design D2). Purely presentational: every
// value and every setter/handler comes from MyListPage, which keeps the
// restorable state and the sort-choice decoding (design D11).
export function MyListControls({ filters, sort, typeOptions, airingOptions, scoreOptions }: MyListControlsProps) {
  const findInputRef = useRef<HTMLInputElement>(null)

  const primaryChoice = toSortChoice(sort.sort, sort.airingStatusFirst)
  const thenChoice = sort.sortThen === null ? '' : toSortChoice(sort.sortThen, sort.airingStatusFirst)
  const isAiringPrimary = sort.sort === 'airingStatus'

  return (
    <div className="my-list-controls">
      <span id="my-list-controls-filter-label" className="my-list-controls__label my-list-controls__label--filter">
        Filter
      </span>
      <div
        className="my-list-controls__row my-list-controls__row--filter"
        role="group"
        aria-labelledby="my-list-controls-filter-label"
      >
        <div className={`my-list-controls__find${filters.query !== '' ? ' my-list-controls--active' : ''}`}>
          <FindGlyph />
          <input
            ref={findInputRef}
            type="text"
            className="my-list-controls__find-input"
            placeholder="Find in list…"
            value={filters.query}
            onChange={(event) => filters.onQueryChange(event.target.value)}
            aria-label="Find in list"
          />
          {filters.query !== '' && (
            <button
              type="button"
              className="my-list-controls__find-clear"
              aria-label="Clear find in list"
              onClick={() => {
                filters.onQueryChange('')
                findInputRef.current?.focus()
              }}
            >
              &times;
            </button>
          )}
        </div>
        <FilterMultiSelect label="Type" options={typeOptions} selected={filters.typeFilter} onChange={filters.onTypeFilterChange} />
        <FilterMultiSelect
          label="Airing"
          options={airingOptions}
          selected={filters.airingFilter}
          onChange={filters.onAiringFilterChange}
        />
        <select
          className={`my-list-controls__select${filters.scoreFilter !== 'any' ? ' my-list-controls--active' : ''}`}
          value={filters.scoreFilter}
          onChange={(event) => filters.onScoreFilterChange(event.target.value as ScoreFilter)}
          aria-label="Filter by score"
        >
          <option value="any">Score: Any</option>
          <option value="rated">Score: Rated</option>
          {scoreOptions.map((value) => (
            <option key={value} value={value}>
              Score: {value}
            </option>
          ))}
          <option value="unrated">Score: Unrated</option>
        </select>
      </div>

      <span id="my-list-controls-sort-label" className="my-list-controls__label my-list-controls__label--sort">
        Sort
      </span>
      <div className="my-list-controls__row my-list-controls__row--sort" role="group" aria-labelledby="my-list-controls-sort-label">
        <select
          className="my-list-controls__select"
          value={primaryChoice}
          onChange={(event) => sort.onSortChange(event.target.value as SortChoice)}
          aria-label="Sort by"
        >
          {renderSortOptions(null, true)}
        </select>
        <button
          type="button"
          className="my-list-controls__direction"
          aria-label={`Sort direction: ${directionLabel(sort.sort, sort.sortDirection)}`}
          disabled={isAiringPrimary}
          title={isAiringPrimary ? 'The order is set by which status comes first, above.' : undefined}
          aria-description={isAiringPrimary ? 'The order is set by which status comes first, above.' : undefined}
          onClick={sort.onDirectionToggle}
        >
          <StableLabel current={directionLabel(sort.sort, sort.sortDirection)} candidates={DIRECTION_CANDIDATES} />
        </button>
        <span className="my-list-controls__then-word">then</span>
        <select
          className="my-list-controls__select"
          value={thenChoice}
          onChange={(event) => sort.onThenChange(event.target.value === '' ? null : (event.target.value as SortChoice))}
          aria-label="Then by"
        >
          <option value="">— none —</option>
          {renderSortOptions(sort.sort, !isAiringPrimary)}
        </select>
        <div className="my-list-controls__segmented" role="group" aria-label="Grouping">
          <button
            type="button"
            className={`my-list-controls__segmented-button${sort.groupByStatus ? ' my-list-controls__segmented-button--selected' : ''}`}
            aria-pressed={sort.groupByStatus}
            onClick={() => sort.onGroupByStatusChange(true)}
          >
            By status
          </button>
          <button
            type="button"
            className={`my-list-controls__segmented-button${!sort.groupByStatus ? ' my-list-controls__segmented-button--selected' : ''}`}
            aria-pressed={!sort.groupByStatus}
            onClick={() => sort.onGroupByStatusChange(false)}
          >
            Single list
          </button>
        </div>
      </div>
    </div>
  )
}
