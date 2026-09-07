import type { SeriesListItemDto } from '../api/types.ts'
import { formatEpisodeTotal, formatYearSpan } from '../utils/anime.ts'
import { AnimeCard } from './AnimeCard.tsx'
import { ScoreChip } from './ScoreChip.tsx'
import { ScoreValue } from './ScoreValue.tsx'
import { SeriesBadge } from './SeriesBadge.tsx'
import { SeriesCompletionBadge } from './SeriesCompletionBadge.tsx'
import { SeriesStatusPill } from './SeriesStatusPill.tsx'
import './SeriesCard.css'

function formatMineAverage(value: number | null): string {
  return value === null ? 'No score' : value.toFixed(2)
}

// The Series page's card (add-series-browser design.md D10): AnimeCard
// composed with a series meta block as children, linking to the series page
// rather than an anime detail page — the same override the search page's
// series results already use — so it inherits the fluid grid's sizing,
// hover treatment, title truncation, and click target for free. Every
// element here is passive and inside the card's link
// (navigation-and-search's clickable-cards requirement); ScoreValue's reveal
// button already stops its own propagation.
export function SeriesCard({ item }: { item: SeriesListItemDto }) {
  return (
    <AnimeCard
      animeId={item.seriesId}
      title={item.title}
      englishTitle={item.englishTitle}
      pictureUrl={item.pictureUrl}
      to={`/series/${item.seriesId}`}
      className="anime-card--fluid"
    >
      <div className="series-card__meta">
        <div className="series-card__badges">
          <SeriesStatusPill status={item.status} />
          <SeriesCompletionBadge badge={item.progressBadge} behindEpisodes={item.behindEpisodes} />
        </div>
        <div className="series-card__scores">
          <ScoreChip role="mal" size="compact">
            <ScoreValue value={item.malMain.value} placeholder="No score" completed={item.malRevealed} />
          </ScoreChip>
          <ScoreChip role="mine" size="compact">{formatMineAverage(item.mineMain.value)}</ScoreChip>
        </div>
        <div className="series-card__line">
          <span>{formatYearSpan(item.firstYear, item.lastYear)}</span>
          <span>{formatEpisodeTotal(item.mainLineEpisodeTotal, item.hasUnknownEpisodeCounts)}</span>
        </div>
        <SeriesBadge entryCount={item.entryCount} />
      </div>
    </AnimeCard>
  )
}
