import { Link } from 'react-router-dom'
import type { SeriesEntryDto } from '../api/types.ts'
import { mediaTypeLabel, pickDisplayTitle, STATUS_LABELS } from '../utils/anime.ts'
import { ScoreValue } from './ScoreValue.tsx'
import { watchedFigureLabel } from './SeriesEntryRow.tsx'
import './SeriesTimeline.css'

type SeriesTimelineProps = {
  /** Main-line entries in watch order. */
  entries: SeriesEntryDto[]
  onEdit: (entry: SeriesEntryDto) => void
}

const MONTH_ABBR = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec']

// A dated card's own air range, at month/year granularity: a shared year is
// dropped from the start ("Apr – Jun 2013"), a year that isn't shared is
// spelled out on both ends ("Oct 2013 – Mar 2014"), an entry confined to one
// month collapses to that single month/year rather than repeating it, and a
// still-airing entry returns its start month/year alone — the Airing label
// TimelineCard renders alongside it completes the line rather than inventing
// an end date.
function formatAirRange(entry: SeriesEntryDto): string {
  const start = new Date(entry.airedFrom!)
  const startMonth = MONTH_ABBR[start.getUTCMonth()]
  const startYear = start.getUTCFullYear()

  if (entry.airingStatus === 'currently_airing') {
    return `${startMonth} ${startYear}`
  }
  if (!entry.airedTo) {
    return `${startMonth} ${startYear}`
  }

  const end = new Date(entry.airedTo)
  const endMonth = MONTH_ABBR[end.getUTCMonth()]
  const endYear = end.getUTCFullYear()

  if (startYear === endYear && startMonth === endMonth) {
    return `${startMonth} ${startYear}`
  }
  if (startYear === endYear) {
    return `${startMonth} – ${endMonth} ${endYear}`
  }
  return `${startMonth} ${startYear} – ${endMonth} ${endYear}`
}

// Franchise chronology as a single main-line list (design.md decision 3 of
// series-page-improvements): a flex row of fixed-size, evenly-spaced cards
// in watch order — including an unreleased upcoming entry right where it
// belongs. Each card states its own air range in place of the year ruler
// that used to float above the row. This is the page's sole presentation of
// the main line; there is no separate non-chronological list elsewhere.
export function SeriesTimeline({ entries, onEdit }: SeriesTimelineProps) {
  return (
    <div className="series-timeline">
      <div className="series-timeline__scroll">
        <div className="series-timeline__row">
          {entries.map((entry) => (
            <TimelineCard key={entry.animeId} entry={entry} onEdit={onEdit} />
          ))}
        </div>
      </div>
    </div>
  )
}

function TimelineCard({ entry, onEdit }: { entry: SeriesEntryDto; onEdit: (entry: SeriesEntryDto) => void }) {
  const displayTitle = pickDisplayTitle(entry.title, entry.englishTitle)
  const total = entry.totalEpisodes
  const watchedEpisodes = entry.entry?.episodesWatched ?? 0
  const airing = entry.airingStatus === 'currently_airing'
  const undated = entry.airedFrom === null
  const watchedPct = total ? Math.min(100, (watchedEpisodes / total) * 100) : 0
  const airedPct = total && entry.airedEpisodes !== null ? Math.min(100, (entry.airedEpisodes / total) * 100) : 0
  const airRange = undated ? null : formatAirRange(entry)
  const watched = watchedFigureLabel(entry)
  const completed = entry.entry?.status === 'Completed'

  return (
    <div
      className={`series-timeline__card${undated ? ' series-timeline__card--undated' : ''}`}
    >
      <Link to={`/anime/${entry.animeId}`} className="series-timeline__card-link">
        {entry.pictureUrl ? (
          <img src={entry.pictureUrl} alt="" className="series-timeline__card-picture" />
        ) : (
          <div className="series-timeline__card-picture series-timeline__card-picture--placeholder" aria-hidden="true" />
        )}
        <span className="series-timeline__card-body">
          <span className="series-timeline__card-title" title={displayTitle}>
            {displayTitle}
          </span>
          <span className="series-timeline__card-meta">
            <span className="series-timeline__card-meta-line">
              {mediaTypeLabel(entry.mediaType)} · {total ?? '?'} ep
            </span>
            <span
              className="series-timeline__card-meta-line"
              title={airing ? `${airRange} · Currently airing` : (airRange ?? undefined)}
            >
              {undated ? (
                <span className="series-timeline__no-date-tag">No air date</span>
              ) : airing ? (
                <>
                  {airRange} · <span className="series-timeline__airing-tag">Airing</span>
                </>
              ) : (
                airRange
              )}
            </span>
          </span>
        </span>
      </Link>
      <div className="series-timeline__fill-track">
        {airing && <div className="series-timeline__fill series-timeline__fill--aired" style={{ width: `${airedPct}%` }} />}
        <div className="series-timeline__fill series-timeline__fill--watched" style={{ width: `${watchedPct}%` }} />
      </div>
      <div className="series-timeline__card-chips">
        <span className="series-timeline__card-chip series-timeline__card-chip--mal">
          <ScoreValue value={entry.malScore} placeholder="No score" completed={completed} />
        </span>
        <span className="series-timeline__card-chip series-timeline__card-chip--mine">{entry.entry?.myScore ?? '—'}</span>
      </div>
      <div className="series-timeline__card-footer">
        <span className="series-timeline__card-status">
          {entry.entry ? STATUS_LABELS[entry.entry.status] : 'Not in list'}
          {watched && <span className="series-timeline__card-status-progress"> · {watched}</span>}
          {!!entry.entry?.rewatchCount && (
            <span className="series-rewatch-badge" title={`Rewatched ${entry.entry.rewatchCount} times`}>
              ↻ {entry.entry.rewatchCount}
            </span>
          )}
        </span>
        <button type="button" className="series-timeline__card-edit" onClick={() => onEdit(entry)}>
          {entry.entry ? 'Edit' : 'Add'}
        </button>
      </div>
    </div>
  )
}
