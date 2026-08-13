import { Link } from 'react-router-dom'
import type { SeriesEntryDto } from '../api/types.ts'
import { mediaTypeLabel, pickDisplayTitle, STATUS_CLASS, STATUS_LABELS } from '../utils/anime.ts'
import { ScoreValue } from './ScoreValue.tsx'
import { watchedFigureLabel } from './SeriesEntryRow.tsx'
import './SeriesTimeline.css'

type SeriesTimelineProps = {
  /** Main-line entries in watch order. */
  entries: SeriesEntryDto[]
  onEdit: (entry: SeriesEntryDto) => void
}

const DAY_MS = 86_400_000

function dayNumber(iso: string): number {
  return Math.floor(new Date(iso).getTime() / DAY_MS)
}

// The end of an entry's run for duration/gap math: its aired-to date, today
// while it's still airing, or its own start for a single-date/movie entry.
function entryEndDay(entry: SeriesEntryDto, todayDay: number): number {
  if (entry.airedTo) return dayNumber(entry.airedTo)
  if (entry.airingStatus === 'currently_airing') return todayDay
  return dayNumber(entry.airedFrom!)
}

type Segment = {
  key: string
  /** Real day range this card spans — null for an undated card, which has
   * no date to place on the axis. */
  startDay: number | null
  endDay: number | null
  entry: SeriesEntryDto
  undated?: boolean
}

// One card per entry, in watch order — an entry with no air date yet (e.g.
// an announced-but-unscheduled next season) becomes its own card right
// where it belongs in the sequence. Every card is the same fixed size and
// evenly spaced: a card's own width no longer scales with how long that
// entry took to air (a variable-width, aspect-ratio-locked poster read as
// "some posters are just bigger than others" rather than as duration).
// Chronology is instead carried entirely by the year ruler above the row.
function buildSegments(entries: SeriesEntryDto[], todayDay: number): Segment[] {
  return entries.map((entry) => {
    if (entry.airedFrom === null) {
      return { key: String(entry.animeId), startDay: null, endDay: null, entry, undated: true }
    }

    const startDay = dayNumber(entry.airedFrom)
    const endDay = entryEndDay(entry, todayDay)
    return { key: String(entry.animeId), startDay, endDay, entry }
  })
}

type YearMark = { year: number; fraction: number }

// A year label is roughly this wide, plus a little breathing room — below
// this pixel gap from the previously placed label, skip it rather than let
// two labels visually collide (happens easily inside one long-running entry
// that alone spans several calendar years).
const MIN_LABEL_GAP_PX = 36

// Matches the fixed width/margin set on .series-timeline__card in the CSS —
// duplicated here (rather than measured) so the year ruler's math can place
// labels without a DOM read, and it will exactly match real layout since
// every card renders at this same fixed size.
const CARD_WIDTH = 168
const CARD_MARGIN = 6
const CARD_STRIDE = CARD_WIDTH + CARD_MARGIN * 2

// Only a year some entry actually aired in gets a label — a year that falls
// entirely between two entries, with nothing airing, is skipped rather than
// implying activity that didn't happen. Each year is attributed to exactly
// one card: the first (earliest, chronologically) card whose own span
// covers it, so a boundary that falls right as one season ends and the next
// begins lands on the earlier card rather than being duplicated or stranded
// in the space between them.
function assignYearMarks(segments: Segment[]): Map<number, YearMark[]> {
  const marks = new Map<number, YearMark[]>()
  const assignedYears = new Set<number>()
  let lastPx = -Infinity
  let cumulativePx = 0

  segments.forEach((seg, index) => {
    if (seg.startDay === null || seg.endDay === null) {
      cumulativePx += CARD_STRIDE
      return
    }

    const span = Math.max(1, seg.endDay - seg.startDay)
    const startYear = new Date(seg.startDay * DAY_MS).getUTCFullYear()
    const endYear = new Date(seg.endDay * DAY_MS).getUTCFullYear()

    const list: YearMark[] = []
    for (let y = startYear; y <= endYear; y++) {
      if (assignedYears.has(y)) continue
      assignedYears.add(y)

      const boundaryDay = dayNumber(`${y}-01-01`)
      const fraction = Math.max(0, Math.min(1, (boundaryDay - seg.startDay) / span))
      const px = cumulativePx + fraction * CARD_WIDTH
      if (px - lastPx < MIN_LABEL_GAP_PX) continue

      lastPx = px
      list.push({ year: y, fraction })
    }
    if (list.length > 0) marks.set(index, list)
    cumulativePx += CARD_STRIDE
  })

  return marks
}

// Franchise chronology as a single main-line list (design.md decision 3 of
// series-page-improvements): a flex row of fixed-size, evenly-spaced cards
// in watch order — including an unreleased upcoming entry right where it
// belongs. A year ruler shares the exact same per-card width so it scrolls
// in lockstep with the cards beneath it, labelling only years something
// actually aired in. This is the page's sole presentation of the main line;
// there is no separate non-chronological list elsewhere.
export function SeriesTimeline({ entries, onEdit }: SeriesTimelineProps) {
  const todayDay = Math.floor(Date.now() / DAY_MS)
  const segments = buildSegments(entries, todayDay)
  const yearMarks = assignYearMarks(segments)

  return (
    <div className="series-timeline">
      <div className="series-timeline__scroll">
        {yearMarks.size > 0 && (
          <div className="series-timeline__axis">
            {segments.map((seg, index) => (
              <div key={`axis-${seg.key}`} className="series-timeline__axis-segment">
                {(yearMarks.get(index) ?? []).map((mark) => (
                  <span key={mark.year} className="series-timeline__axis-year" style={{ left: `${mark.fraction * 100}%` }}>
                    {mark.year}
                  </span>
                ))}
              </div>
            ))}
          </div>
        )}
        <div className="series-timeline__row">
          {segments.map((seg) => (
            <TimelineCard key={seg.key} entry={seg.entry} undated={!!seg.undated} onEdit={onEdit} />
          ))}
        </div>
      </div>
    </div>
  )
}

function TimelineCard({
  entry,
  undated = false,
  onEdit,
}: {
  entry: SeriesEntryDto
  undated?: boolean
  onEdit: (entry: SeriesEntryDto) => void
}) {
  const displayTitle = pickDisplayTitle(entry.title, entry.englishTitle)
  const total = entry.totalEpisodes
  const watchedEpisodes = entry.entry?.episodesWatched ?? 0
  const airing = entry.airingStatus === 'currently_airing'
  const watchedPct = total ? Math.min(100, (watchedEpisodes / total) * 100) : 0
  const airedPct = total && entry.airedEpisodes !== null ? Math.min(100, (entry.airedEpisodes / total) * 100) : 0
  const year = entry.airedFrom ? entry.airedFrom.slice(0, 4) : null
  const statusClass = entry.entry ? ` series-timeline__card--${STATUS_CLASS[entry.entry.status]}` : ''
  const watched = watchedFigureLabel(entry)
  const completed = entry.entry?.status === 'Completed'

  return (
    <div className={`series-timeline__card${statusClass}${undated ? ' series-timeline__card--undated' : ''}`}>
      <Link to={`/anime/${entry.animeId}`} className="series-timeline__card-link">
        {entry.pictureUrl ? (
          <img src={entry.pictureUrl} alt="" className="series-timeline__card-picture" />
        ) : (
          <div className="series-timeline__card-picture series-timeline__card-picture--placeholder" aria-hidden="true" />
        )}
        {/* The fill-track below already shows aired-vs-watched progress
            graphically, so "airing" is signalled here rather than by also
            spelling out an aired-of-total count in the meta line — that kept
            pushing the line onto a second row and, since cards otherwise all
            have identical heights, left an uneven gap above the footer on
            every other card in the row. */}
        {airing && <span className="series-timeline__airing-badge">Airing</span>}
        <span className="series-timeline__card-body">
          <span className="series-timeline__card-title" title={displayTitle}>
            {displayTitle}
          </span>
          <span className="series-timeline__card-meta">
            {mediaTypeLabel(entry.mediaType)} ·{' '}
            {year ?? <span className="series-timeline__no-date-tag">No air date</span>} · {total ?? '?'} ep
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
