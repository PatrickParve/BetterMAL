import { Link } from 'react-router-dom'
import type { SeriesEntryDto } from '../api/types.ts'
import { mediaTypeLabel, pickDisplayTitle, STATUS_CLASS, STATUS_LABELS } from '../utils/anime.ts'
import { ScoreValue } from './ScoreValue.tsx'
import { airedFigureLabel, watchedFigureLabel } from './SeriesEntryRow.tsx'
import './SeriesExtraTile.css'

type SeriesExtraTileProps = {
  entry: SeriesEntryDto
  onEdit: (entry: SeriesEntryDto) => void
}

// The More-section counterpart to SeriesEntryRow: a poster tile rather than a
// row, carrying the same facts (redesign-series-page design.md decision 5) —
// picture, title, year, episode count, MAL score, my score, my status — plus
// the same aired/watched wording helpers so a tile and a row never phrase the
// same fact differently.
export function SeriesExtraTile({ entry, onEdit }: SeriesExtraTileProps) {
  const displayTitle = pickDisplayTitle(entry.title, entry.englishTitle)
  const year = entry.airedFrom ? entry.airedFrom.slice(0, 4) : null
  const statusClass = entry.entry ? ` series-extra-tile--${STATUS_CLASS[entry.entry.status]}` : ''
  const aired = airedFigureLabel(entry)
  const watched = watchedFigureLabel(entry)

  return (
    <li className={`series-extra-tile${statusClass}`}>
      <Link to={`/anime/${entry.animeId}`} className="series-extra-tile__link">
        {entry.pictureUrl ? (
          <img src={entry.pictureUrl} alt="" className="series-extra-tile__picture" />
        ) : (
          <div className="series-extra-tile__picture series-extra-tile__picture--placeholder" aria-hidden="true" />
        )}
        <span className="series-extra-tile__body">
          <span className="series-extra-tile__title" title={displayTitle}>
            {displayTitle}
          </span>
          <span className="series-extra-tile__meta">
            {mediaTypeLabel(entry.mediaType)}
            {year && ` · ${year}`} · {entry.totalEpisodes ?? '?'} ep
          </span>
          {aired && <span className="series-extra-tile__aired">{aired}</span>}
        </span>
      </Link>
      <div className="series-extra-tile__chips">
        <span className="series-extra-tile__chip series-extra-tile__chip--mal">
          <ScoreValue value={entry.malScore} completed={entry.entry?.status === 'Completed'} />
        </span>
        <span className="series-extra-tile__chip series-extra-tile__chip--mine">{entry.entry?.myScore ?? '—'}</span>
      </div>
      <div className="series-extra-tile__footer">
        <span className="series-extra-tile__status">
          {entry.entry ? STATUS_LABELS[entry.entry.status] : 'Not in list'}
          {watched && ` · ${watched}`}
        </span>
        <button type="button" className="series-extra-tile__edit" onClick={() => onEdit(entry)}>
          {entry.entry ? 'Edit' : 'Add'}
        </button>
      </div>
    </li>
  )
}
