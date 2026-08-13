import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import type { SeriesEntryDto } from '../api/types.ts'
import { pickDisplayTitle } from '../utils/anime.ts'
import './SeriesTimeline.css'

type SeriesTimelineProps = {
  /** Main-line entries in watch order. */
  entries: SeriesEntryDto[]
  /** The hide-scores toggle — the MAL side is omitted entirely while on, not blurred. */
  hidden: boolean
  longestGapDays: number | null
  longestGapFromAnimeId: number | null
  longestGapToAnimeId: number | null
}

const MIN_BLOCK_WIDTH = 48
const MIN_GAP_WIDTH = 8
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

// "4y 2mo" / "6mo" / "18d" — words for the longest-gap marker. The days value
// itself always comes from stats.longestGapDays (design decision 6); this
// only formats it.
function formatGapWords(days: number): string {
  if (days >= 365) {
    const years = Math.floor(days / 365)
    const months = Math.round((days % 365) / 30)
    return months > 0 ? `${years}y ${months}mo` : `${years}y`
  }
  if (days >= 30) return `${Math.round(days / 30)}mo`
  return `${days}d`
}

function scoreBarHeight(score: number | null | undefined): { height: string } {
  return { height: `${Math.max(0, Math.min(100, ((score ?? 0) / 10) * 100))}%` }
}

// Franchise chronology (redesign-series-page design.md decision 6): a single
// flex row alternating gap spacers (flex-grow: gapDays) and entry blocks
// (flex-grow: max(1, durationDays)), so a long wait between seasons reads as
// visible empty space rather than only as a number. Flex proportions rather
// than absolute positioning — a min-width floor on a short block just steals
// a little proportion from the gaps, and nothing can ever overlap.
export function SeriesTimeline({
  entries,
  hidden,
  longestGapDays,
  longestGapFromAnimeId,
  longestGapToAnimeId,
}: SeriesTimelineProps) {
  const rankByAnimeId = new Map(entries.map((entry, index) => [entry.animeId, index + 1]))
  const dated = entries.filter((e) => e.airedFrom !== null)
  const undated = entries.filter((e) => e.airedFrom === null)
  const todayDay = Math.floor(Date.now() / DAY_MS)

  // No main-line entry has a date at all: fall back to equal-width blocks in
  // watch order, which is exactly the old score-comparison strip.
  if (dated.length === 0) {
    return (
      <div className="series-timeline">
        {hidden && <p className="series-timeline__hidden-note">MAL scores are hidden while the toggle is on.</p>}
        <div className="series-timeline__scroll">
          <div className="series-timeline__row">
            {entries.map((entry) => (
              <TimelineBlock key={entry.animeId} entry={entry} rank={rankByAnimeId.get(entry.animeId)!} hidden={hidden} grow={1} />
            ))}
          </div>
        </div>
      </div>
    )
  }

  const firstYear = new Date(dated[0].airedFrom!).getFullYear()
  const lastYear = new Date(dated[dated.length - 1].airedFrom!).getFullYear()

  const segments: ReactNode[] = []
  dated.forEach((entry, index) => {
    if (index > 0) {
      const prev = dated[index - 1]
      const gapDays = Math.max(0, dayNumber(entry.airedFrom!) - entryEndDay(prev, todayDay))
      const isLongestGap =
        longestGapDays !== null && longestGapFromAnimeId === prev.animeId && longestGapToAnimeId === entry.animeId

      segments.push(
        <div
          key={`gap-${prev.animeId}-${entry.animeId}`}
          className="series-timeline__gap"
          style={{ flexGrow: Math.max(1, gapDays), minWidth: MIN_GAP_WIDTH }}
        >
          {isLongestGap && (
            <span className="series-timeline__gap-marker">
              {formatGapWords(longestGapDays!)} between{' '}
              <Link to={`/anime/${prev.animeId}`}>{pickDisplayTitle(prev.title, prev.englishTitle)}</Link> and{' '}
              <Link to={`/anime/${entry.animeId}`}>{pickDisplayTitle(entry.title, entry.englishTitle)}</Link>
            </span>
          )}
        </div>,
      )
    }

    const durationDays = Math.max(0, entryEndDay(entry, todayDay) - dayNumber(entry.airedFrom!))
    segments.push(
      <TimelineBlock
        key={entry.animeId}
        entry={entry}
        rank={rankByAnimeId.get(entry.animeId)!}
        hidden={hidden}
        grow={Math.max(1, durationDays)}
      />,
    )
  })

  return (
    <div className="series-timeline">
      {hidden && <p className="series-timeline__hidden-note">MAL scores are hidden while the toggle is on.</p>}
      <div className="series-timeline__axis">
        <span>{firstYear}</span>
        <span>{lastYear}</span>
      </div>
      <div className="series-timeline__scroll">
        <div className="series-timeline__row">{segments}</div>
        {undated.length > 0 && (
          <div className="series-timeline__row series-timeline__row--undated">
            <span className="series-timeline__undated-label">No air date</span>
            {undated.map((entry) => (
              <TimelineBlock key={entry.animeId} entry={entry} rank={rankByAnimeId.get(entry.animeId)!} hidden={hidden} grow={1} />
            ))}
          </div>
        )}
      </div>
    </div>
  )
}

function TimelineBlock({
  entry,
  rank,
  hidden,
  grow,
}: {
  entry: SeriesEntryDto
  rank: number
  hidden: boolean
  grow: number
}) {
  const displayTitle = pickDisplayTitle(entry.title, entry.englishTitle)
  const total = entry.totalEpisodes
  const watched = entry.entry?.episodesWatched ?? 0
  const airing = entry.airingStatus === 'currently_airing'
  const watchedPct = total ? Math.min(100, (watched / total) * 100) : 0
  const airedPct = total && entry.airedEpisodes !== null ? Math.min(100, (entry.airedEpisodes / total) * 100) : 0
  const year = entry.airedFrom ? entry.airedFrom.slice(0, 4) : '—'

  return (
    <div className="series-timeline__block" style={{ flexGrow: grow, minWidth: MIN_BLOCK_WIDTH }}>
      <div className="series-timeline__scores">
        {!hidden && (
          <div
            className="series-timeline__score-bar series-timeline__score-bar--mal"
            style={scoreBarHeight(entry.malScore)}
            title={entry.malScore != null ? `MAL ${entry.malScore.toFixed(2)}` : 'No MAL score'}
          />
        )}
        <div
          className="series-timeline__score-bar series-timeline__score-bar--mine"
          style={scoreBarHeight(entry.entry?.myScore)}
          title={entry.entry?.myScore ? `Me ${entry.entry.myScore}` : 'No score'}
        />
      </div>
      <div className="series-timeline__fill-track">
        {airing && <div className="series-timeline__fill series-timeline__fill--aired" style={{ width: `${airedPct}%` }} />}
        <div className="series-timeline__fill series-timeline__fill--watched" style={{ width: `${watchedPct}%` }} />
      </div>
      <Link to={`/anime/${entry.animeId}`} className="series-timeline__label" title={displayTitle} aria-label={displayTitle}>
        #{rank} · {year}
      </Link>
    </div>
  )
}
