import type { TasteGenreDto, TasteStudioDto } from '../api/types.ts'
import type { RankingOverlayRow } from '../components/RankingOverlay.tsx'

// The genre and studio rows (add-genre-and-studio-stats). One mapping each,
// shared by the recap's lists and the profile's Top genres and Top studios, so a
// genre or studio reads the same on both pages. `basis` says which order the row
// is shown in, and so which figure it carries: the anime count under 'count'
// (Most watched), the scored count and the weighted score under 'score' (Highest
// rated). Both lead to my list, with the genre or studio ticked in its filter;
// the caller says which link, since the recap scopes it to the period and the
// profile to the anime I have watched.
export type TasteBasis = 'count' | 'score'

function tasteMeta(row: TasteGenreDto | TasteStudioDto, basis: TasteBasis): string {
  if (basis === 'count') return `${row.animeCount} anime`
  return `${row.scoredCount} scored · ${row.weightedScore === null ? '—' : row.weightedScore.toFixed(2)}`
}

export function describeGenreRanking(row: TasteGenreDto, basis: TasteBasis, to: string): RankingOverlayRow {
  return { key: row.name, label: row.name, meta: tasteMeta(row, basis), to, posters: row.posters }
}

export function describeStudioRanking(row: TasteStudioDto, basis: TasteBasis, to: string): RankingOverlayRow {
  return { key: row.name, label: row.name, meta: tasteMeta(row, basis), to, posters: row.posters }
}
