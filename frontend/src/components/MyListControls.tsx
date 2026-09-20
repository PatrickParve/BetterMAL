import { useRef } from 'react'
import { AIRING_FILTER_OPTIONS, MEDIA_TYPE_FILTER_OPTIONS, type SortDirection, type SortKey } from '../utils/anime.ts'
import { FilterMultiSelect, type FilterMultiSelectOption } from './FilterMultiSelect.tsx'
import { StableLabel } from './StableLabel.tsx'
import './MyListControls.css'

// Widened (design.md decision 5 of polish-rewatch-more-and-filters) to carry
// a specific score value alongside the three original options — stored as
// the plain number string ("8"), not the `score-8` form the recap handoff's
// `focus` token uses; MyListPage's parseFocus converts between the two.
export type ScoreFilter = 'any' | 'rated' | 'unrated' | `${number}`

// Every sort key, in the order the primary and tiebreaker selects present them.
const SORT_OPTIONS: { value: SortKey; label: string }[] = [
  { value: 'alphabetical', label: 'Alphabetical' },
  { value: 'myScore', label: 'My score' },
  { value: 'malScore', label: 'MAL score' },
  { value: 'popularity', label: 'Popularity' },
  { value: 'episodesWatched', label: 'Episodes watched' },
  { value: 'progress', label: 'Progress' },
  { value: 'totalEpisodes', label: 'Total episodes' },
  { value: 'type', label: 'Type' },
  { value: 'startDate', label: 'Start date' },
  { value: 'finishDate', label: 'Finish date' },
]

// One <option> run for a sort select: every key minus `excludeKey`, so a
// select never offers its own current value as a tiebreaker target.
function renderSortOptions(excludeKey: SortKey | null) {
  return SORT_OPTIONS.filter((option) => option.value !== excludeKey).map((option) => (
    <option key={option.value} value={option.value}>
      {option.label}
    </option>
  ))
}

// Direction button text, per key (design D5) — every key has a direction.
const DIRECTION_LABELS: Record<SortKey, { natural: string; reversed: string }> = {
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

// Every label the direction button could ever show, deduped — several keys
// share the same pair of labels (StableLabel needs one entry per distinct
// string, not one per key).
const DIRECTION_CANDIDATES = Array.from(
  new Set(Object.values(DIRECTION_LABELS).flatMap((pair) => [pair.natural, pair.reversed])),
)

function directionLabel(key: SortKey, direction: SortDirection): string {
  const pair = DIRECTION_LABELS[key]
  return direction === 'natural' ? pair.natural : pair.reversed
}

function scoreOptionLabel(value: ScoreFilter): string {
  if (value === 'any') return 'Score: Any'
  if (value === 'rated') return 'Score: Rated'
  if (value === 'unrated') return 'Score: Unrated'
  return `Score: ${value}`
}

// Every value the score select could ever hold, in presentation order —
// used only to size the hidden twin select beside the live one (design D7,
// tasks.md 4.5), independent of what the list currently narrows down to.
const SCORE_FILTER_WIDTH_VALUES: ScoreFilter[] = [
  'any',
  'rated',
  ...Array.from({ length: 10 }, (_, i) => String(10 - i) as ScoreFilter),
  'unrated',
]

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
  onSortChange: (key: SortKey) => void
  onThenChange: (key: SortKey | null) => void
  onDirectionToggle: () => void
  onGroupByStatusChange: (value: boolean) => void
}

type MyListControlsProps = {
  filters: MyListFiltersProps
  sort: MyListSortProps
  typeOptions: FilterMultiSelectOption[]
  airingOptions: FilterMultiSelectOption[]
  // The choices that would change the list (MyListPage's one-pass option
  // memo), in presentation order — not "the score values at least one entry
  // has", since Rated/Unrated and a value are each dropped when choosing
  // them wouldn't narrow, or would empty, what's currently listed (design D2).
  scoreOptions: ScoreFilter[]
  // Non-null exactly when the score control has nothing left to choose
  // between (design D5) — the value its disabled label should name.
  scoreDisabledLabel: string | null
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
export function MyListControls({ filters, sort, typeOptions, airingOptions, scoreOptions, scoreDisabledLabel }: MyListControlsProps) {
  const findInputRef = useRef<HTMLInputElement>(null)

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
        <FilterMultiSelect
          label="Type"
          options={typeOptions}
          widthOptions={MEDIA_TYPE_FILTER_OPTIONS}
          selected={filters.typeFilter}
          onChange={filters.onTypeFilterChange}
        />
        <FilterMultiSelect
          label="Airing"
          options={airingOptions}
          widthOptions={AIRING_FILTER_OPTIONS}
          selected={filters.airingFilter}
          onChange={filters.onAiringFilterChange}
        />
        {/* The single-area grid mirrors StableLabel's cell technique (see
            StableLabel.css) — a native select can't use StableLabel itself
            (a hidden <span> can't account for the browser's own dropdown
            arrow), so the hidden twin select beside the live one reserves
            that width directly (design D7). */}
        <div className="my-list-controls__score">
          <select
            className={`my-list-controls__select${filters.scoreFilter !== 'any' ? ' my-list-controls--active' : ''}`}
            value={filters.scoreFilter}
            onChange={(event) => filters.onScoreFilterChange(event.target.value as ScoreFilter)}
            disabled={scoreDisabledLabel !== null}
            aria-label="Filter by score"
          >
            {scoreDisabledLabel !== null ? (
              <option value="any">Score: {scoreDisabledLabel}</option>
            ) : (
              scoreOptions.map((value) => (
                <option key={value} value={value}>
                  {scoreOptionLabel(value)}
                </option>
              ))
            )}
          </select>
          <select
            className="my-list-controls__select my-list-controls__select--sizer"
            aria-hidden="true"
            tabIndex={-1}
          >
            {SCORE_FILTER_WIDTH_VALUES.map((value) => (
              <option key={value} value={value}>
                {scoreOptionLabel(value)}
              </option>
            ))}
          </select>
        </div>
      </div>

      <span id="my-list-controls-sort-label" className="my-list-controls__label my-list-controls__label--sort">
        Sort
      </span>
      <div className="my-list-controls__row my-list-controls__row--sort" role="group" aria-labelledby="my-list-controls-sort-label">
        <select
          className="my-list-controls__select"
          value={sort.sort}
          onChange={(event) => sort.onSortChange(event.target.value as SortKey)}
          aria-label="Sort by"
        >
          {renderSortOptions(null)}
        </select>
        <button
          type="button"
          className="my-list-controls__direction"
          aria-label={`Sort direction: ${directionLabel(sort.sort, sort.sortDirection)}`}
          onClick={sort.onDirectionToggle}
        >
          <StableLabel current={directionLabel(sort.sort, sort.sortDirection)} candidates={DIRECTION_CANDIDATES} />
        </button>
        <span className="my-list-controls__then-word">then</span>
        <select
          className="my-list-controls__select"
          value={sort.sortThen ?? ''}
          onChange={(event) => sort.onThenChange(event.target.value === '' ? null : (event.target.value as SortKey))}
          aria-label="Then by"
        >
          <option value="">— none —</option>
          {renderSortOptions(sort.sort)}
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
