import { Link } from 'react-router-dom'
import type { SeriesEntryDto } from '../api/types.ts'
import { useLandscapePicture } from '../hooks/useLandscapePicture.ts'
import { isScoreRevealableStatus, mediaTypeLabel, pickDisplayTitle, STATUS_CLASS, STATUS_LABELS } from '../utils/anime.ts'
import { ScoreChip } from './ScoreChip.tsx'
import { ScoreValue } from './ScoreValue.tsx'
import { airedFigureLabel, watchedFigureLabel } from './SeriesEntryRow.tsx'
import './SeriesExtraTile.css'

type SeriesExtraTileProps = {
  entry: SeriesEntryDto
  onEdit: (entry: SeriesEntryDto) => void
}

// A tile's link target is decided by the server, not inferred from its
// display group (rebuild-series-by-story-component design.md D7, series-page
// spec "A More tile's link target is decided by the series"): opensOwnSeries
// is true only for a version neighbour that carries story relations of its
// own — a telling with a franchise of its own, addressed by its own anime id
// so the series read endpoint can build it on the visit itself. Every other
// tile, including a folded-in version neighbour with no story relations of
// its own, opens the anime's detail page — replacing the old relationGroup
// inference, which sent every Alternative version/setting tile to the series
// page unconditionally and could point back at the page it was rendered on.
function tileHref(entry: SeriesEntryDto): string {
  return entry.opensOwnSeries ? `/series/${entry.animeId}` : `/anime/${entry.animeId}`
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
  const [pictureRef, isLandscape] = useLandscapePicture(entry.pictureUrl)

  return (
    <li className={`series-extra-tile${statusClass}${isLandscape ? ' series-extra-tile--landscape' : ''}`}>
      <Link to={tileHref(entry)} className="series-extra-tile__link">
        <span className="series-extra-tile__picture-frame">
          {entry.pictureUrl ? (
            <img ref={pictureRef} src={entry.pictureUrl} alt="" className="series-extra-tile__picture" />
          ) : (
            <div className="series-extra-tile__picture series-extra-tile__picture--placeholder" aria-hidden="true" />
          )}
        </span>
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
        <ScoreChip role="mal" size="compact">
          <ScoreValue value={entry.malScore} completed={isScoreRevealableStatus(entry.entry?.status)} />
        </ScoreChip>
        <ScoreChip role="mine" size="compact">{entry.entry?.myScore ?? '—'}</ScoreChip>
      </div>
      <div className="series-extra-tile__footer">
        <span className="series-extra-tile__status">
          {entry.entry ? STATUS_LABELS[entry.entry.status] : 'Not in list'}
          {watched && ` · ${watched}`}
        </span>
        {!!entry.entry?.rewatchCount && (
          <span className="series-rewatch-badge" title={`Rewatched ${entry.entry.rewatchCount} times`}>
            ↻ {entry.entry.rewatchCount}
          </span>
        )}
        <button type="button" className="series-extra-tile__edit" onClick={() => onEdit(entry)}>
          {entry.entry ? 'Edit' : 'Add'}
        </button>
      </div>
    </li>
  )
}
