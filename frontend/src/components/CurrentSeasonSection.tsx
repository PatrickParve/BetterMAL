import { useMemo, useState } from 'react'
import { AnimeCard } from './AnimeCard.tsx'
import { ProgressBar } from './ProgressBar.tsx'
import { ScoreValue } from './ScoreValue.tsx'
import type { CurrentSeasonItemDto } from '../api/types.ts'
import './CurrentSeasonSection.css'

type SortKey = 'popularity' | 'malScore' | 'alphabetical'

const SORT_OPTIONS: { value: SortKey; label: string }[] = [
  { value: 'popularity', label: 'Popularity' },
  { value: 'malScore', label: 'MAL score' },
  { value: 'alphabetical', label: 'Alphabetical' },
]

function sortItems(items: CurrentSeasonItemDto[], sort: SortKey): CurrentSeasonItemDto[] {
  const sorted = [...items]
  switch (sort) {
    case 'popularity':
      sorted.sort((a, b) => (a.popularityRank ?? Number.MAX_SAFE_INTEGER) - (b.popularityRank ?? Number.MAX_SAFE_INTEGER))
      break
    case 'malScore':
      sorted.sort((a, b) => (b.malScore ?? -1) - (a.malScore ?? -1))
      break
    case 'alphabetical':
      sorted.sort((a, b) => a.title.localeCompare(b.title))
      break
  }
  return sorted
}

type CurrentSeasonSectionProps = {
  items: CurrentSeasonItemDto[]
}

// "Current season" section: my-list anime airing this season, sortable by
// popularity, MAL score, or alphabetically, each with an episode progress bar.
export function CurrentSeasonSection({ items }: CurrentSeasonSectionProps) {
  const [sort, setSort] = useState<SortKey>('popularity')
  const sortedItems = useMemo(() => sortItems(items, sort), [items, sort])

  return (
    <section className="dashboard-section">
      <div className="current-season__header">
        <h2>Current season</h2>
        <select
          className="current-season__sort"
          value={sort}
          onChange={(event) => setSort(event.target.value as SortKey)}
          aria-label="Sort current season"
        >
          {SORT_OPTIONS.map((option) => (
            <option key={option.value} value={option.value}>
              {option.label}
            </option>
          ))}
        </select>
      </div>
      {sortedItems.length === 0 ? (
        <p className="current-season__empty">Nothing from my list is airing this season.</p>
      ) : (
        <div className="current-season__grid">
          {sortedItems.map((item) => (
            <AnimeCard key={item.animeId} animeId={item.animeId} title={item.title} pictureUrl={item.pictureUrl}>
              <ProgressBar watched={item.episodesWatched} total={item.totalEpisodes} />
              <ScoreValue value={item.malScore} />
            </AnimeCard>
          ))}
        </div>
      )}
    </section>
  )
}
