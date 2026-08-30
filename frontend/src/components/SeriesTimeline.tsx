import { Link } from 'react-router-dom'
import type { SeriesEntryDto, SeriesSlotDto } from '../api/types.ts'
import { useLandscapePicture } from '../hooks/useLandscapePicture.ts'
import { isScoreRevealableStatus, mediaTypeLabel, pickDisplayTitle, STATUS_LABELS } from '../utils/anime.ts'
import { ScoreChip } from './ScoreChip.tsx'
import { ScoreValue } from './ScoreValue.tsx'
import { watchedFigureLabel } from './SeriesEntryRow.tsx'
import './SeriesTimeline.css'

// A slotted position's picker inputs (series-versions "Alternative versions on
// a main line share one watch-order position"): options is every alternative
// of the entry's own slot, resolved from allEntries since only the picked one
// is ever present in the visible entries array itself.
type SlotPicker = {
  options: SeriesEntryDto[]
  onPick: (animeId: number) => void
}

type SeriesTimelineProps = {
  /** Visible main-line entries in watch order — trunk plus, per slot, the
   * picked alternative and its branch (series-versions capability). Numbered
   * from 1 over exactly this array, so switching a pick never skips a number. */
  entries: SeriesEntryDto[]
  /** Every main-line entry, every alternative included — resolves a slotted
   * position's picker options, which aren't all present in `entries`. */
  allEntries: SeriesEntryDto[]
  slots: SeriesSlotDto[]
  onPick: (slotKey: number, animeId: number) => void
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
// the main line; there is no separate non-chronological list elsewhere —
// which is also why a version slot's picker lives here rather than in a
// second, non-chronological rendering of the main line
// (rebuild-series-by-story-component task 8.3).
export function SeriesTimeline({ entries, allEntries, slots, onPick, onEdit }: SeriesTimelineProps) {
  const slotByKey = new Map(slots.map((slot) => [slot.slotKey, slot]))
  const entryById = new Map(allEntries.map((entry) => [entry.animeId, entry]))

  return (
    <div className="series-timeline">
      <div className="series-timeline__scroll">
        <div className="series-timeline__row">
          {entries.map((entry, index) => {
            const slot = entry.versionSlotKey !== null ? slotByKey.get(entry.versionSlotKey) : undefined
            const picker: SlotPicker | undefined = slot && {
              options: slot.alternativeAnimeIds
                .map((id) => entryById.get(id))
                .filter((option): option is SeriesEntryDto => option != null),
              onPick: (animeId) => onPick(slot.slotKey, animeId),
            }
            return (
              <TimelineCard key={entry.animeId} entry={entry} number={index + 1} picker={picker} onEdit={onEdit} />
            )
          })}
        </div>
      </div>
    </div>
  )
}

function TimelineCard({
  entry,
  number,
  picker,
  onEdit,
}: {
  entry: SeriesEntryDto
  number: number
  picker: SlotPicker | undefined
  onEdit: (entry: SeriesEntryDto) => void
}) {
  const displayTitle = pickDisplayTitle(entry.title, entry.englishTitle)
  const total = entry.totalEpisodes
  const watchedEpisodes = entry.entry?.episodesWatched ?? 0
  const airing = entry.airingStatus === 'currently_airing'
  const undated = entry.airedFrom === null
  const watchedPct = total ? Math.min(100, (watchedEpisodes / total) * 100) : 0
  const airedPct = total && entry.airedEpisodes !== null ? Math.min(100, (entry.airedEpisodes / total) * 100) : 0
  const airRange = undated ? null : formatAirRange(entry)
  const watched = watchedFigureLabel(entry)
  const completed = isScoreRevealableStatus(entry.entry?.status)
  const [pictureRef, isLandscape] = useLandscapePicture(entry.pictureUrl)

  return (
    <div
      className={`series-timeline__card${undated ? ' series-timeline__card--undated' : ''}${isLandscape ? ' series-timeline__card--landscape' : ''}`}
    >
      <div className="series-timeline__card-header">
        <span className="series-timeline__card-number">{number}</span>
      </div>
      {picker && (
        <div className="series-page__slot-picker" role="group" aria-label={`Choose version for watch order position ${number}`}>
          {picker.options.map((option) => {
            const optionTitle = pickDisplayTitle(option.title, option.englishTitle)
            const active = option.animeId === entry.animeId
            return (
              <button
                key={option.animeId}
                type="button"
                className={`series-page__slot-picker-button${active ? ' series-page__slot-picker-button--active' : ''}`}
                aria-pressed={active}
                title={optionTitle}
                onClick={() => picker.onPick(option.animeId)}
              >
                {optionTitle}
              </button>
            )
          })}
        </div>
      )}
      <Link to={`/anime/${entry.animeId}`} className="series-timeline__card-link">
        {entry.pictureUrl ? (
          <img ref={pictureRef} src={entry.pictureUrl} alt="" className="series-timeline__card-picture" />
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
        <ScoreChip role="mal" size="compact">
          <ScoreValue value={entry.malScore} placeholder="No score" completed={completed} />
        </ScoreChip>
        <ScoreChip role="mine" size="compact">{entry.entry?.myScore ?? '—'}</ScoreChip>
      </div>
      <div className="series-timeline__card-footer">
        <span className="series-timeline__card-status">
          {entry.entry ? STATUS_LABELS[entry.entry.status] : 'Not in list'}
          {watched && <span className="series-timeline__card-status-progress"> · {watched}</span>}
        </span>
        {!!entry.entry?.rewatchCount && (
          <span className="series-rewatch-badge" title={`Rewatched ${entry.entry.rewatchCount} times`}>
            ↻ {entry.entry.rewatchCount}
          </span>
        )}
        <button type="button" className="series-timeline__card-edit" onClick={() => onEdit(entry)}>
          {entry.entry ? 'Edit' : 'Add'}
        </button>
      </div>
    </div>
  )
}
