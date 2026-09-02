import type { CSSProperties } from 'react'
import { Link } from 'react-router-dom'
import type { RecapSeasonRankingDto, RecapYearRankingDto } from '../api/types.ts'
import { scoreTier, seasonLabel } from '../utils/anime.ts'
import type { RankingOverlayRow } from './RankingOverlay.tsx'
import './RankingSection.css'

// Reads scoreCounts at a score's own index (index 0 is score 1, index 9 is
// score 10 — polish-favourites-filters-and-browse-scroll design.md decision
// D5), so no caller open-codes the offset. Exported alongside the describe*
// functions since both the score filter and a row's meta under a selection
// (D7) need the same figure.
export function scoreCountAt(row: { scoreCounts: number[] }, score: number): number {
  return row.scoreCounts[score - 1]
}

// One mapping per ranking DTO, shared by its inline five-row list and its
// overlay (design.md decision 6/8) — the two representations of a ranking
// read from the same describe() call, so they cannot drift. Shared by
// RecapPage and ProfilePage (design.md decision 8/task 8.2) so a season's or
// year's row always renders identically on both pages. An optional
// `selectedScore` (polish-favourites-filters-and-browse-scroll design.md
// decision D7) states what the row was ranked on when a favourites score
// filter is active; RecapPage's four callers never pass one.
export function describeSeasonRanking(row: RecapSeasonRankingDto, selectedScore?: number): RankingOverlayRow {
  return {
    key: `${row.year}-${row.season}`,
    label: `${seasonLabel(row.season)} ${row.year}`,
    meta:
      selectedScore === undefined
        ? `${row.scoredCount} scored · ${row.weightedScore.toFixed(2)}`
        : `${scoreCountAt(row, selectedScore)} × ${selectedScore} · ${row.scoredCount} scored`,
    to: `/recap?mode=season&year=${row.year}&season=${row.season}`,
    posters: row.topPosters,
  }
}

export function describeYearRanking(row: RecapYearRankingDto, selectedScore?: number): RankingOverlayRow {
  return {
    key: String(row.year),
    label: String(row.year),
    meta:
      selectedScore === undefined
        ? `${row.scoredCount} scored · ${row.weightedScore.toFixed(2)}`
        : `${scoreCountAt(row, selectedScore)} × ${selectedScore} · ${row.scoredCount} scored`,
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

// Optional score filter row (polish-favourites-filters-and-browse-scroll
// design.md decision D7) — only the profile page's two favourites rankings
// pass this; RecapPage's four rankings pass nothing and render unchanged.
type ScoreFilter = {
  scores: number[]
  selected: number | null
  onSelect: (score: number | null) => void
}

// Each button's tier class comes from scoreTier(score) (design.md decision
// D8) — never a second 10->1 mapping — so a score is guaranteed the same
// colour here as in the recap page's rating distribution. All carries no
// tier class, so it inherits the section's own family--year/family--season.
function renderScoreFilter(noun: string, scoreFilter: ScoreFilter) {
  const { scores, selected, onSelect } = scoreFilter
  return (
    <div className="ranking-score-filter" role="group" aria-label={`Rank ${noun} by score`}>
      <button
        type="button"
        className={
          selected === null
            ? 'ranking-score-filter__button ranking-score-filter__button--selected'
            : 'ranking-score-filter__button'
        }
        aria-pressed={selected === null}
        onClick={() => onSelect(null)}
      >
        All
      </button>
      <span className="ranking-score-filter__label">With most:</span>
      {scores.map((score) => (
        <button
          key={score}
          type="button"
          className={
            (selected === score
              ? 'ranking-score-filter__button ranking-score-filter__button--selected'
              : 'ranking-score-filter__button') + ` ranking-score-filter__button--tier-${scoreTier(score)}`
          }
          aria-pressed={selected === score}
          onClick={() => onSelect(score)}
        >
          {score}
        </button>
      ))}
    </div>
  )
}

type RankingSectionProps = {
  title: string
  noun: string
  rows: RankingOverlayRow[]
  onSeeAll: (overlay: { title: string; rows: RankingOverlayRow[]; family?: 'year' | 'season' }) => void
  style?: CSSProperties
  family?: 'year' | 'season'
  scoreFilter?: ScoreFilter
}

// One rendering for every ranking a page shows — capped at
// VISIBLE_RANK_COUNT with a "See all" control that opens the shared
// RankingOverlay for the rest (design.md decision 8, tasks.md 8.2-8.3). The
// caller owns the single-slot overlay state (RecapPage and ProfilePage each
// show at most one overlay at a time) and passes it in via onSeeAll. `style`
// is RecapPage's grid-placement hook (design.md decision 8) for a multi-year
// recap's row-aligned rankings grid — unused (and harmless) elsewhere.
// `family` bands the title and colours the section's rows per the
// section-colour-language capability; the class sits on the section rather
// than the title alone (design.md decision 1) so every row inherits it too.
// The caller decides the family rather than it being inferred from `title`,
// so a reworded heading can never silently lose its colour. `scoreFilter` is
// optional (design.md decision D7): rendered between the title and the rows
// only when given, so RecapPage's four rankings are untouched.
export function RankingSection({ title, noun, rows, onSeeAll, style, family, scoreFilter }: RankingSectionProps) {
  const visible = rows.slice(0, VISIBLE_RANK_COUNT)
  return (
    <section className={family ? `recap-page__section family--${family}` : 'recap-page__section'} style={style}>
      <h2 className={family ? 'section-band' : undefined}>{title}</h2>
      {scoreFilter && renderScoreFilter(noun, scoreFilter)}
      {renderRankingRows(visible)}
      {rows.length > VISIBLE_RANK_COUNT && (
        <button type="button" className="recap-page__see-all-ranks" onClick={() => onSeeAll({ title, rows, family })}>
          See all {rows.length} {noun}
        </button>
      )}
    </section>
  )
}
