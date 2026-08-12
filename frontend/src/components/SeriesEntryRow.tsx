import { Link } from 'react-router-dom'
import type { SeriesEntryDto } from '../api/types.ts'
import { mediaTypeLabel, pickDisplayTitle, STATUS_CLASS, STATUS_LABELS } from '../utils/anime.ts'
import { ScoreValue } from './ScoreValue.tsx'
import './SeriesEntryRow.css'

type SeriesEntryRowProps = {
  entry: SeriesEntryDto
  /** Watch-order number for main-line rows; omitted for More-section rows. */
  rank?: number
  onEdit: (entry: SeriesEntryDto) => void
}

// One series-page row — poster, title/type/year/episodes, MAL score, my
// score, my status, and an edit control that opens the app's shared entry
// editor. Same split-link shape as AnimeCard/MyListRow: the edit button sits
// outside the <Link> so it never triggers navigation, and the hover
// highlight matches the my-list/top-anime row treatment.
export function SeriesEntryRow({ entry, rank, onEdit }: SeriesEntryRowProps) {
  const displayTitle = pickDisplayTitle(entry.title, entry.englishTitle)
  const year = entry.airedFrom ? entry.airedFrom.slice(0, 4) : null
  const statusClass = entry.entry ? ` series-entry-row--${STATUS_CLASS[entry.entry.status]}` : ''

  return (
    <li className={`series-entry-row${statusClass}`}>
      {rank !== undefined && <span className="series-entry-row__rank">#{rank}</span>}
      <Link to={`/anime/${entry.animeId}`} className="series-entry-row__link">
        {entry.pictureUrl ? (
          <img src={entry.pictureUrl} alt="" className="series-entry-row__picture" />
        ) : (
          <div className="series-entry-row__picture series-entry-row__picture--placeholder" aria-hidden="true" />
        )}
        <span className="series-entry-row__info">
          <span className="series-entry-row__title" title={displayTitle}>
            {displayTitle}
          </span>
          <span className="series-entry-row__meta">
            {mediaTypeLabel(entry.mediaType)}
            {year && ` · ${year}`} · {entry.totalEpisodes ?? '?'} ep
          </span>
        </span>
      </Link>
      <span className="series-entry-row__mal-score">
        <ScoreValue value={entry.malScore} completed={entry.entry?.status === 'Completed'} />
      </span>
      <span className="series-entry-row__my-score">{entry.entry?.myScore ?? '—'}</span>
      <span className="series-entry-row__status">
        {entry.entry ? STATUS_LABELS[entry.entry.status] : 'Not in list'}
      </span>
      <button type="button" className="series-entry-row__edit" onClick={() => onEdit(entry)}>
        {entry.entry ? 'Edit' : 'Add'}
      </button>
    </li>
  )
}
