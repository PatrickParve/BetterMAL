import type { CSSProperties } from 'react'
import { Link } from 'react-router-dom'
import type { RecapSeasonRankingDto, RecapYearRankingDto } from '../api/types.ts'
import { seasonLabel } from '../utils/anime.ts'
import type { RankingOverlayRow } from './RankingOverlay.tsx'
import './RankingSection.css'

// One mapping per ranking DTO, shared by its inline five-row list and its
// overlay (design.md decision 6/8) — the two representations of a ranking
// read from the same describe() call, so they cannot drift. Shared by
// RecapPage and ProfilePage (design.md decision 8/task 8.2) so a season's or
// year's row always renders identically on both pages.
export function describeSeasonRanking(row: RecapSeasonRankingDto): RankingOverlayRow {
  return {
    key: `${row.year}-${row.season}`,
    label: `${seasonLabel(row.season)} ${row.year}`,
    meta: `${row.scoredCount} scored · ${row.weightedScore.toFixed(2)}`,
    to: `/recap?mode=season&year=${row.year}&season=${row.season}`,
    posters: row.topPosters,
  }
}

export function describeYearRanking(row: RecapYearRankingDto): RankingOverlayRow {
  return {
    key: String(row.year),
    label: String(row.year),
    meta: `${row.scoredCount} scored · ${row.weightedScore.toFixed(2)}`,
    to: `/recap?mode=yearly&year=${row.year}&filter=aired`,
    posters: row.topPosters,
  }
}

// Applies uniformly to every ranking this component renders — season, year,
// and both of the recap page's time-watched rankings, plus the profile
// page's favourite seasons/years (design.md decision 6/8, tasks.md 7.3).
export const VISIBLE_RANK_COUNT = 5

function renderPosters(posters: RankingOverlayRow['posters']) {
  if (posters.length === 0) return null
  return (
    <span className="recap-ranking-row__posters">
      {posters.map((p) =>
        p.pictureUrl ? (
          <img key={p.animeId} src={p.pictureUrl} alt="" title={p.title} className="recap-ranking-row__poster" />
        ) : (
          <span key={p.animeId} className="recap-ranking-row__poster recap-ranking-row__poster--placeholder" title={p.title} />
        ),
      )}
    </span>
  )
}

function renderRankingRows(rows: RankingOverlayRow[]) {
  return (
    <ol className="recap-ranking-list">
      {rows.map((row, index) => (
        <li key={row.key} className="recap-ranking-row">
          <Link to={row.to} className="recap-ranking-row__link">
            <span className="recap-ranking-row__rank">#{index + 1}</span>
            <span className="recap-ranking-row__label">{row.label}</span>
            <span className="recap-ranking-row__meta">{row.meta}</span>
            {renderPosters(row.posters)}
          </Link>
        </li>
      ))}
    </ol>
  )
}

type RankingSectionProps = {
  title: string
  noun: string
  rows: RankingOverlayRow[]
  onSeeAll: (overlay: { title: string; rows: RankingOverlayRow[] }) => void
  style?: CSSProperties
}

// One rendering for every ranking a page shows — capped at
// VISIBLE_RANK_COUNT with a "See all" control that opens the shared
// RankingOverlay for the rest (design.md decision 8, tasks.md 8.2-8.3). The
// caller owns the single-slot overlay state (RecapPage and ProfilePage each
// show at most one overlay at a time) and passes it in via onSeeAll. `style`
// is RecapPage's grid-placement hook (design.md decision 8) for a multi-year
// recap's row-aligned rankings grid — unused (and harmless) elsewhere.
export function RankingSection({ title, noun, rows, onSeeAll, style }: RankingSectionProps) {
  const visible = rows.slice(0, VISIBLE_RANK_COUNT)
  return (
    <section className="recap-page__section" style={style}>
      <h2>{title}</h2>
      {renderRankingRows(visible)}
      {rows.length > VISIBLE_RANK_COUNT && (
        <button type="button" className="recap-page__see-all-ranks" onClick={() => onSeeAll({ title, rows })}>
          See all {rows.length} {noun}
        </button>
      )}
    </section>
  )
}
