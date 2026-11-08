import { useRef } from 'react'
import {
  AIRING_FILTER_OPTIONS,
  GENRE_FILTER_OPTIONS,
  MEDIA_TYPE_FILTER_OPTIONS,
  SCORE_FILTER_OPTIONS,
  STUDIO_FILTER_MAX_CHARS,
  STUDIO_FILTER_OPTIONS,
  type SortDirection,
  type SortKey,
} from '../utils/anime.ts'
import { FilterMultiSelect, type FilterMultiSelectOption } from './FilterMultiSelect.tsx'
import { StableLabel } from './StableLabel.tsx'
import './MyListControls.css'

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

export type MyListFiltersProps = {
  query: string
  onQueryChange: (value: string) => void
  // The five multi-selects' ticked values. `[]` is nothing ticked, which is
  // no restriction (page-header-design: the shared multi-select applies no
  // restriction until an option is ticked). Score ticks are 'rated', 'unrated'
  // or the plain number string ("8"), not the `score-8` form the recap
  // handoff's `focus` token uses; MyListPage's parseFocus converts between the two.
  typeFilters: string[]
  onTypeFiltersChange: (value: string[]) => void
  airingFilters: string[]
  onAiringFiltersChange: (value: string[]) => void
  genreFilters: string[]
  onGenreFiltersChange: (value: string[]) => void
  studioFilters: string[]
  onStudioFiltersChange: (value: string[]) => void
  scoreFilters: string[]
  onScoreFiltersChange: (value: string[]) => void
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
  // The genre and studio boards' values, alphabetical, with the dead ends marked
  // `disabled` (utils/boardFilter.ts).
  genreOptions: FilterMultiSelectOption[]
  studioOptions: FilterMultiSelectOption[]
  // The score choices that would change the list (MyListPage's one-pass option
  // memo), in presentation order — not "the score values at least one entry
  // has", since Rated/Unrated and a value are each dropped when choosing
  // them wouldn't narrow, or would empty, what's currently listed (design D2).
  scoreOptions: FilterMultiSelectOption[]
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
export function MyListControls({
  filters,
  sort,
  typeOptions,
  airingOptions,
  genreOptions,
  studioOptions,
  scoreOptions,
}: MyListControlsProps) {
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
          selected={filters.typeFilters}
          onChange={filters.onTypeFiltersChange}
        />
        <FilterMultiSelect
          label="Airing"
          options={airingOptions}
          widthOptions={AIRING_FILTER_OPTIONS}
          selected={filters.airingFilters}
          onChange={filters.onAiringFiltersChange}
        />
        {/* A board of about seventy genres in columns, not a list. One genre
            on it is still a choice (every genre on it narrows the list when
            ticked on its own), so the control is unavailable only with none. */}
        <FilterMultiSelect
          label="Genre"
          options={genreOptions}
          widthOptions={GENRE_FILTER_OPTIONS}
          selected={filters.genreFilters}
          onChange={filters.onGenreFiltersChange}
          layout="board"
          singleOptionIsChoice
        />
        {/* The same board as Genre, over the studios MyAnimeList credits the entries
            with. Its trigger is sized for a name at its clipping limit rather than
            for a closed set of names (STUDIO_FILTER_OPTIONS). */}
        <FilterMultiSelect
          label="Studio"
          options={studioOptions}
          widthOptions={STUDIO_FILTER_OPTIONS}
          selected={filters.studioFilters}
          onChange={filters.onStudioFiltersChange}
          layout="board"
          singleOptionIsChoice
          maxLabelChars={STUDIO_FILTER_MAX_CHARS}
        />
        {/* Several scores can be ticked: an entry has one score, so it passes when it
            matches any ticked choice. */}
        <FilterMultiSelect
          label="Score"
          options={scoreOptions}
          widthOptions={SCORE_FILTER_OPTIONS}
          selected={filters.scoreFilters}
          onChange={filters.onScoreFiltersChange}
        />
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
