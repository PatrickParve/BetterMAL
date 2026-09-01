import { Link } from 'react-router-dom'
import type { SeriesEntryDto } from '../api/types.ts'
import { isScoreRevealableStatus, mediaTypeLabel, pickDisplayTitle, STATUS_CLASS, STATUS_LABELS } from '../utils/anime.ts'
import { ScoreValue } from './ScoreValue.tsx'
import './SeriesEntryRow.css'

type SeriesEntryRowProps = {
  entry: SeriesEntryDto
  onEdit: (entry: SeriesEntryDto) => void
}

// Worded so it can never be read as the entry's own total (redesign-series-page
// design.md decision 7) — the ambiguity being fixed is exactly that a bare
// `x/y` doesn't say which axis it's on. Shared with SeriesExtraTile so a row
// and a tile never word the same fact differently.
export function airedFigureLabel(entry: SeriesEntryDto): string | null {
  if (entry.airingStatus !== 'currently_airing' || entry.airedEpisodes === null) return null
  return `${entry.airedEpisodes} of ${entry.totalEpisodes ?? '?'} aired`
}

// Only for an entry I've actually started (watched at least one episode) and
// not yet completed — a PlanToWatch entry with 0 episodes watched isn't "my
// position", it's just not started.
export function watchedFigureLabel(entry: SeriesEntryDto): string | null {
  const userEntry = entry.entry
  if (!userEntry || userEntry.status === 'Completed' || userEntry.episodesWatched <= 0) return null
  return `${userEntry.episodesWatched}/${entry.totalEpisodes ?? '?'}`
}

// One series-page row — poster, title/type/year/episodes, MAL score, my
// score, my status, and an edit control that opens the app's shared entry
// editor. Same split-link shape as AnimeCard/MyListRow: the edit button sits
// outside the <Link> so it never triggers navigation, and the hover
// highlight matches the my-list/top-anime row treatment.
export function SeriesEntryRow({ entry, onEdit }: SeriesEntryRowProps) {
  const displayTitle = pickDisplayTitle(entry.title, entry.englishTitle)
  const year = entry.airedFrom ? entry.airedFrom.slice(0, 4) : null
  const statusClass = entry.entry ? ` series-entry-row--${STATUS_CLASS[entry.entry.status]}` : ''
  const aired = airedFigureLabel(entry)
  const watched = watchedFigureLabel(entry)

  return (
    <li className={`series-entry-row${statusClass}`}>
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
            {aired && ` · ${aired}`}
          </span>
        </span>
      </Link>
      <span className="series-entry-row__mal-score score--mal">
        <ScoreValue value={entry.malScore} completed={isScoreRevealableStatus(entry.entry?.status)} />
      </span>
      <span className="series-entry-row__my-score score--mine">{entry.entry?.myScore ?? '—'}</span>
      <span className="series-entry-row__status">
        {entry.entry ? STATUS_LABELS[entry.entry.status] : 'Not in list'}
        {watched && <span className="series-entry-row__status-progress"> · {watched}</span>}
        {!!entry.entry?.rewatchCount && (
          <span className="series-rewatch-badge" title={`Rewatched ${entry.entry.rewatchCount} times`}>
            ↻ {entry.entry.rewatchCount}
          </span>
        )}
      </span>
      <button type="button" className="series-entry-row__edit" onClick={() => onEdit(entry)}>
        {entry.entry ? 'Edit' : 'Add'}
      </button>
    </li>
  )
}
