import { RECAP_SEASONS, type RecapSeasonName } from '../api/types.ts'
import { seasonPointIndex, shiftSeason } from './anime.ts'

export type SeasonTarget = { year: number; season: RecapSeasonName }

// The first year in MyAnimeList's season archive — matches the backend's
// SeasonCalendar.EarliestArchiveYear, the lower end of the range the API
// accepts. The floor both the season and year pages' arrows and dropdowns use.
export const EARLIEST_YEAR = 1917

// The client-computed ceiling used only as a fallback: while GET
// /api/season/bounds has not yet resolved this session, or when it fails.
// Mirrors the backend's SeasonHorizon.FutureSeasonWindow, MAL's published
// forward window (current season +2 returned 200, +3 returned 404, probed
// 2026-08-18).
export const FUTURE_SEASON_WINDOW = 2

// One past FUTURE_SEASON_WINDOW — the one season a Season or Year page visit's
// horizon probe ever asks about (mirrors the backend's HorizonProbe.Target).
const PROBE_OFFSET = FUTURE_SEASON_WINDOW + 1

export function isSeasonName(value: string | null): value is RecapSeasonName {
  return value !== null && (RECAP_SEASONS as readonly string[]).includes(value)
}

export function currentSeasonTarget(): SeasonTarget {
  const now = new Date()
  return { year: now.getFullYear(), season: RECAP_SEASONS[Math.floor(now.getMonth() / 3)] }
}

// The season the horizon probe targets for a given current season — fixed
// relative to the current season, not to wherever the ceiling now sits, so a
// successful probe ends the probing instead of moving the target one further
// out. Exported so each guard can recognise the one season, and the one year,
// that earn an on-demand check when addressed directly by URL rather than
// being refused outright (design D10a, tasks 5.3a/6.2a).
export function probeTarget(current: SeasonTarget): SeasonTarget {
  return shiftSeason(current.year, current.season, PROBE_OFFSET)
}

// The addressable ceiling *is* the navigable ceiling GET /api/season/bounds
// returns — a URL reaches exactly what the arrows and dropdown offer, with no
// second, wider ceiling (design D3). A season is addressable when it falls, in
// season order, between winter of the archive's earliest year and that
// ceiling.
export function isAddressableSeason(target: SeasonTarget, ceiling: SeasonTarget): boolean {
  const index = seasonPointIndex(target.year, target.season)
  return index >= seasonPointIndex(EARLIEST_YEAR, 'winter') && index <= seasonPointIndex(ceiling.year, ceiling.season)
}

// The year counterpart of isAddressableSeason: a year is addressable when any
// of its seasons is, so the range is simply [EARLIEST_YEAR, the ceiling's
// year] — the same ceiling the season guard uses, not a second, wider one.
export function isAddressableYear(year: number, ceilingYear: number): boolean {
  return year >= EARLIEST_YEAR && year <= ceilingYear
}

// The dropdown's whole option list, built from the addressable range's own
// two ends and nothing else — never widened against a URL-supplied year, so
// no part of rendering the page allocates per-year work proportional to a
// hostile or malformed value (design D1). Descending, matching the season and
// year dropdowns' existing order.
export function yearsInRange(floor: number, ceiling: number): number[] {
  const length = Math.max(0, ceiling - floor + 1)
  return Array.from({ length }, (_, i) => ceiling - i)
}
