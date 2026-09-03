import type { CSSProperties } from 'react'
import { Link } from 'react-router-dom'
import type { RecapRankingPosterDto, RecapRankingScorePostersDto, RecapSeasonRankingDto, RecapYearRankingDto } from '../api/types.ts'
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

// The favourites score filter (design.md decision D6): the backend already
// returns each ranking in full rank order — weighted score, then scored
// count, then the histogram from 10 down, then newest first — so a
// 10-down-to-1 offered list and a stable sort by "count of the selected
// score, descending" resolve every tie in exactly that same order, with no
// second copy of CompareGroups on the client. This relies on
// Array.prototype.sort's stability (guaranteed since ES2019); if it is ever
// replaced by a hand-rolled comparator, ties will stop falling back to the
// backend's own order and may come out arbitrary instead. Shared by
// RecapPage and ProfilePage (design.md D4) so there is one implementation of
// the filter, not a copy per page.
export function offeredScores(rows: { scoreCounts: number[] }[]): number[] {
  const scores: number[] = []
  for (let score = 10; score >= 1; score--) {
    if (rows.some((row) => scoreCountAt(row, score) > 0)) scores.push(score)
  }
  return scores
}

export function rankByScoreCount<T extends { scoreCounts: number[] }>(rows: T[], score: number): T[] {
  return rows.filter((row) => scoreCountAt(row, score) > 0).sort((a, b) => scoreCountAt(b, score) - scoreCountAt(a, score))
}

// Picks a row's posters by my ranking, not by title (design.md D1) — the
// group's three best anime, either overall or at a selected score. Without a
// score, the posters are the first three read from the top of
// postersByScore, which is exactly the group's best three overall because
// the buckets are already score-ordered and each holds its own score's best
// — there is no separate "All" list that could disagree (design.md D2).
// With a score, that score's own bucket — empty only when the group holds
// none of it, which cannot happen for a row the filter kept.
function postersFor(
  row: { postersByScore: RecapRankingScorePostersDto[] },
  selectedScore?: number,
): RecapRankingPosterDto[] {
  if (selectedScore !== undefined) {
    return row.postersByScore.find((bucket) => bucket.score === selectedScore)?.posters ?? []
  }
  const posters: RecapRankingPosterDto[] = []
  for (const bucket of row.postersByScore) {
    posters.push(...bucket.posters)
    if (posters.length >= 3) break
  }
  return posters.slice(0, 3)
}

// One mapping per ranking DTO, shared by its inline five-row list and its
// overlay (design.md decision 6/8) — the two representations of a ranking
// read from the same describe() call, so they cannot drift. Shared by
// RecapPage and ProfilePage (design.md decision 8/task 8.2) so a season's or
// year's row always renders identically on both pages. An optional
// `selectedScore` (polish-favourites-filters-and-browse-scroll design.md
// decision D7) states what the row was ranked on when a favourites score
// filter is active. Never pass either function to `.map` point-free —
// `Array.prototype.map` hands each row's index as the second argument, which
// this parameter would silently accept as `selectedScore` and mislabel every
// row (refine-ranking-posters-and-score-filters design.md "A live defect").
// Every call site must be an explicit arrow.
export function describeSeasonRanking(row: RecapSeasonRankingDto, selectedScore?: number): RankingOverlayRow {
  return {
    key: `${row.year}-${row.season}`,
    label: `${seasonLabel(row.season)} ${row.year}`,
    meta:
      selectedScore === undefined
        ? `${row.scoredCount} scored · ${row.weightedScore.toFixed(2)}`
        : `${scoreCountAt(row, selectedScore)} × ${selectedScore} · ${row.scoredCount} scored`,
    to: `/recap?mode=season&year=${row.year}&season=${row.season}`,
    posters: postersFor(row, selectedScore),
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
    posters: postersFor(row, selectedScore),
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
// only when given, so RecapPage's four rankings are untouched. When given,
// the "See all" overlay's title also gains the "— with most Ns" suffix
// (design.md D4) — composed here, since this is the one place that already
// holds both `title` and `scoreFilter.selected`, so both callers' `onSeeAll`
// reduce to their plain overlay setter.
export function RankingSection({ title, noun, rows, onSeeAll, style, family, scoreFilter }: RankingSectionProps) {
  const visible = rows.slice(0, VISIBLE_RANK_COUNT)
  const overlayTitle =
    scoreFilter && scoreFilter.selected !== null ? `${title} — with most ${scoreFilter.selected}s` : title
  return (
    <section className={family ? `recap-page__section family--${family}` : 'recap-page__section'} style={style}>
      <h2 className={family ? 'section-band' : undefined}>{title}</h2>
      {scoreFilter && renderScoreFilter(noun, scoreFilter)}
      {renderRankingRows(visible)}
      {rows.length > VISIBLE_RANK_COUNT && (
        <button
          type="button"
          className="recap-page__see-all-ranks"
          onClick={() => onSeeAll({ title: overlayTitle, rows, family })}
        >
          See all {rows.length} {noun}
        </button>
      )}
    </section>
  )
}
