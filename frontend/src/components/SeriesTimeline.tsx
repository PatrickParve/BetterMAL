import type { CSSProperties } from 'react'
import { Link } from 'react-router-dom'
import type { SeriesEntryDto, SeriesSlotDto } from '../api/types.ts'
import { isScoreRevealableStatus, mediaTypeLabel, pickDisplayTitle, STATUS_LABELS } from '../utils/anime.ts'
import { PosterPicture } from './PosterPicture.tsx'
import { ScoreChip } from './ScoreChip.tsx'
import { ScoreValue } from './ScoreValue.tsx'
import { watchedFigureLabel } from './SeriesEntryRow.tsx'
import './SeriesTimeline.css'

// A slotted position's picker inputs (series-versions "Alternative versions on
// a main line share one watch-order position"): options is every alternative
// of the slot, resolved from allEntries since only the picked one is ever
// present in the visible entries array itself. activeAnimeId is the resolved
// pick, needed here because the picker-owning column (design decision 8)
// need not be the active alternative's own card.
type SlotPicker = {
  options: SeriesEntryDto[]
  activeAnimeId: number
  onPick: (animeId: number) => void
}

type SeriesTimelineProps = {
  /** Visible main-line entries in watch order — trunk plus, per slot, the
   * picked alternative and its branch (series-versions capability). */
  entries: SeriesEntryDto[]
  /** Every main-line entry, every alternative included — resolves a slotted
   * position's picker options, which aren't all present in `entries`. */
  allEntries: SeriesEntryDto[]
  slots: SeriesSlotDto[]
  /** Each slot's resolved pick (SeriesPage's resolveSeriesPick) — locates the
   * picker-owning column (series-versions design decision 8: the first entry
   * of `entries`, already in watch order, whose branchHeadAnimeId is the
   * slot's resolved pick) and which option in it reads as active. */
  resolvedPick: Record<number, number>
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
export function SeriesTimeline({ entries, allEntries, slots, resolvedPick, onPick, onEdit }: SeriesTimelineProps) {
  const entryById = new Map(allEntries.map((entry) => [entry.animeId, entry]))
  const hasSlots = slots.length > 0

  // The picker-owning column per slot (series-versions design decision 8):
  // the first entry of `entries` — already in watch order — whose
  // branchHeadAnimeId is the slot's resolved pick. SeriesVersionSlots puts an
  // alternative in its own branch, so one predicate covers both the case
  // where the route starts at the alternative itself and the case where a
  // branch entry (a prologue) precedes it in story order.
  const pickerByColumn = new Map<number, SlotPicker>()
  for (const slot of slots) {
    const activeAnimeId = resolvedPick[slot.slotKey]
    const owner = entries.find((entry) => entry.branchHeadAnimeId === activeAnimeId)
    if (!owner) continue
    pickerByColumn.set(owner.animeId, {
      options: slot.alternativeAnimeIds
        .map((id) => entryById.get(id))
        .filter((option): option is SeriesEntryDto => option != null),
      activeAnimeId,
      onPick: (animeId) => onPick(slot.slotKey, animeId),
    })
  }

  // The tallest stack of buttons any slot in this series needs — every
  // column's rail is this many rows tall (SeriesTimeline.css), whether or
  // not it owns a picker, so every card's top edge lines up regardless of
  // which column the picker lands in.
  const maxOptionCount = hasSlots ? Math.max(...slots.map((slot) => slot.alternativeAnimeIds.length)) : 0

  return (
    <div className="series-timeline">
      <div className={`series-timeline__scroll${hasSlots ? ' series-timeline__scroll--sloted' : ''}`}>
        <div className="series-timeline__row" style={hasSlots ? ({ '--picker-rows': maxOptionCount } as CSSProperties) : undefined}>
          {entries.map((entry) => (
            <div className="series-timeline__col" key={entry.animeId}>
              {hasSlots && (
                <div className="series-timeline__picker-rail">
                  {pickerByColumn.has(entry.animeId) && <SlotPickerButtons picker={pickerByColumn.get(entry.animeId)!} />}
                </div>
              )}
              <TimelineCard entry={entry} onEdit={onEdit} />
            </div>
          ))}
        </div>
      </div>
    </div>
  )
}

// Stacked one per row, each the full width of the card it sits above
// (series-versions "The picker SHALL be rendered above the first card of the
// route it selects") — every alternative gets its own line rather than
// splitting one line N ways, which is what lets the rail's height (and so
// its top edge) be computed from option count alone (SeriesTimeline.css).
function SlotPickerButtons({ picker }: { picker: SlotPicker }) {
  const optionTitles = picker.options.map((option) => pickDisplayTitle(option.title, option.englishTitle))
  return (
    <div className="series-page__slot-picker" role="group" aria-label={`Choose version: ${optionTitles.join(' or ')}`}>
      {picker.options.map((option, i) => {
        const active = option.animeId === picker.activeAnimeId
        return (
          <button
            key={option.animeId}
            type="button"
            className={`series-page__slot-picker-button${active ? ' series-page__slot-picker-button--active' : ''}`}
            aria-pressed={active}
            title={optionTitles[i]}
            onClick={() => picker.onPick(option.animeId)}
          >
            {optionTitles[i]}
          </button>
        )
      })}
    </div>
  )
}

function TimelineCard({
  entry,
  onEdit,
}: {
  entry: SeriesEntryDto
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

  return (
    <div
      className={`series-timeline__card${undated ? ' series-timeline__card--undated' : ''}`}
    >
      <Link to={`/anime/${entry.animeId}`} className="series-timeline__card-link">
        <PosterPicture src={entry.pictureUrl} className="series-timeline__card-picture" />
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
