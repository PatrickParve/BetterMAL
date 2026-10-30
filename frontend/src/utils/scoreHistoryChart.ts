import type { ScoreHistoryMarkerDto, ScoreHistoryPointDto } from '../api/types.ts'

// The geometry behind the score history graph (add-score-history-graph design.md
// D9): scales, ticks, date labels, year marks and the marker rail as plain
// functions with no React and no DOM, so ScoreHistoryChart only draws what they
// return. The frontend has no test runner, so these are checked in headless
// Chrome (tasks.md 8.3).

const MS_PER_DAY = 86_400_000

// A score history's dates are calendar dates (yyyy-MM-dd), not instants. They
// are split by hand and counted in days from a fixed UTC origin, never passed
// to `new Date(string)`, so the viewer's timezone cannot move a point across
// midnight (design.md D6).
export function parseIsoDate(iso: string): { year: number; month: number; day: number } {
  const [year, month, day] = iso.split('-').map(Number)
  return { year, month, day }
}

function civilDay(year: number, month: number, day: number): number {
  return Date.UTC(year, month - 1, day) / MS_PER_DAY
}

/** Whole days from a fixed origin: the horizontal axis is proportional to this. */
export function dayNumber(iso: string): number {
  const { year, month, day } = parseIsoDate(iso)
  return civilDay(year, month, day)
}

function pad2(value: number): string {
  return String(value).padStart(2, '0')
}

/** `dd.mm`, the axis's and the list's date. A year appears on its own. */
export function formatDayMonth(iso: string): string {
  const { month, day } = parseIsoDate(iso)
  return `${pad2(day)}.${pad2(month)}`
}

/** `dd.mm.yyyy`, for the tooltip, where no year mark may be near. */
export function formatFullDate(iso: string): string {
  const { year } = parseIsoDate(iso)
  return `${formatDayMonth(iso)}.${year}`
}

export function formatScore(score: number): string {
  return score.toFixed(2)
}

// U+2212, as the design draws it, so a move reads the same as a figure in a
// table rather than a hyphen.
export function formatMove(move: number): string {
  return `${move < 0 ? '−' : '+'}${Math.abs(move).toFixed(2)}`
}

export type Scale = (value: number) => number

/**
 * The horizontal scale: proportional to time, from the first point to the
 * last, so the axis ends at the last change rather than at today and a month
 * of silence looks longer than a day (design.md D10). A history of one day has
 * no span to be proportional to and sits in the middle.
 */
export function timeScale(startDay: number, endDay: number, width: number): Scale {
  const span = endDay - startDay
  return (day) => (span <= 0 ? width / 2 : ((day - startDay) / span) * width)
}

// A move of 0.01 must not fill the graph and look like a collapse, so the
// vertical axis never spans less than this (design.md D10).
export const MIN_SCORE_SPAN = 0.2

// Breathing room above and below the scores, as a fraction of the range.
const SCORE_PADDING = 0.1

export type ScoreScale = {
  min: number
  max: number
  /** Top of the plot is `max`. */
  y: Scale
}

/** The vertical scale: around the scores rather than from zero, never narrower than MIN_SCORE_SPAN. */
export function scoreScale(scores: number[], height: number): ScoreScale {
  let min = Math.min(...scores)
  let max = Math.max(...scores)
  if (max - min < MIN_SCORE_SPAN) {
    const middle = (min + max) / 2
    min = middle - MIN_SCORE_SPAN / 2
    max = middle + MIN_SCORE_SPAN / 2
  }
  const padding = (max - min) * SCORE_PADDING
  min -= padding
  max += padding
  return { min, max, y: (score) => height - ((score - min) / (max - min)) * height }
}

const TICK_STEPS = [0.05, 0.1, 0.2, 0.5, 1, 2]
const MIN_TICKS = 3
const MAX_TICKS = 5

function ticksAt(min: number, max: number, step: number): number[] {
  // Multiples of the step counted as integers, so 0.05 steps never drift.
  const first = Math.ceil(min / step - 1e-9)
  const last = Math.floor(max / step + 1e-9)
  const ticks: number[] = []
  for (let k = first; k <= last; k++) {
    const value = Math.round(k * step * 100) / 100
    if (value >= 0 && value <= 10) ticks.push(value)
  }
  return ticks
}

/**
 * The gridline values: the smallest of 0.05, 0.1, 0.2, 0.5 or 1 that leaves
 * three to five of them in the range (design.md D10). The final 2 is only a
 * safety for a range spanning most of the scale, which scores seldom do.
 */
export function chooseTicks(min: number, max: number): number[] {
  let best: number[] = []
  for (const step of TICK_STEPS) {
    const ticks = ticksAt(min, max, step)
    if (ticks.length <= MAX_TICKS && ticks.length >= MIN_TICKS) return ticks
    if (ticks.length <= MAX_TICKS && ticks.length > best.length) best = ticks
  }
  return best
}

/**
 * Which date labels to draw: the indices of `xs` (left to right) kept so no two
 * labels touch. The first and last always stay, and the others are taken
 * greedily from the left, which keeps as many as can fit.
 */
export function thinLabels(xs: number[], labelWidth: number, gap: number): number[] {
  const count = xs.length
  if (count <= 1) return count === 0 ? [] : [0]

  const needed = labelWidth + gap
  const kept = [0]
  for (let i = 1; i < count - 1; i++) {
    if (xs[i] - xs[kept[kept.length - 1]] >= needed && xs[count - 1] - xs[i] >= needed) kept.push(i)
  }
  kept.push(count - 1)
  return kept
}

export type YearMark = {
  year: number
  /** Where the mark sits, as a day number: the first point for the first year, 1 January for each later one. */
  day: number
}

/**
 * One mark per year that holds a point, in order (design.md D10). The first
 * year marks the start of the axis; each later year marks where it begins,
 * which always falls inside the axis because a point of that year follows it.
 * A year holding no point has none. `dates` need not be sorted.
 */
export function yearMarks(dates: string[]): YearMark[] {
  if (dates.length === 0) return []

  const startDay = Math.min(...dates.map(dayNumber))
  const years = [...new Set(dates.map((date) => parseIsoDate(date).year))].sort((a, b) => a - b)
  return years.map((year, index) => ({
    year,
    day: index === 0 ? startDay : civilDay(year, 1, 1),
  }))
}

export type YearLabelPlacement = {
  year: number
  x: number
  /** `start` writes the year to the right of its line, `end` to the left. */
  anchor: 'start' | 'end'
}

/**
 * Places each year's text beside its line. The first year's mark can sit
 * within a label's width of the next year's, when the history starts days
 * before a new year, so it is written to the left of the axis's start instead
 * of running into the next one. Later years are a whole year apart.
 */
export function placeYearLabels(marks: YearMark[], x: Scale, labelWidth: number, gap: number): YearLabelPlacement[] {
  return marks.map((mark, index) => {
    const position = x(mark.day)
    const next = marks[index + 1]
    const crowded = index === 0 && next !== undefined && x(next.day) - position < labelWidth + 2 * gap
    return { year: mark.year, x: position, anchor: crowded ? 'end' : 'start' }
  })
}

export type RailPlacement = {
  /** The picture's left edge, within the plot. */
  left: number
  /** 0 is the row nearest the plot. */
  row: number
}

/**
 * Where each marker's picture goes in the rail above the plot (design.md D11).
 * A picture stays over its own date, moved sideways only as far as needed to
 * stay inside the plot. Markers are taken left to right, each into the first
 * row where it clears the previous picture, or into a new row above, so no two
 * pictures overlap however many there are and two on one date stack. `xs` is
 * each marker's date position, and the result follows the same order.
 */
export function placeRail(xs: number[], plotWidth: number, boxWidth: number, gap: number): RailPlacement[] {
  const order = xs.map((_, index) => index).sort((a, b) => xs[a] - xs[b] || a - b)
  const rowRight: number[] = []
  const placements: RailPlacement[] = new Array(xs.length)

  for (const index of order) {
    const left = Math.min(Math.max(xs[index] - boxWidth / 2, 0), Math.max(plotWidth - boxWidth, 0))
    let row = rowRight.findIndex((right) => left >= right + gap)
    if (row === -1) row = rowRight.length
    rowRight[row] = left + boxWidth
    placements[index] = { left, row }
  }
  return placements
}

/** Whether a dot per point fits: below 12px a point on average, a long airing run stays a line. */
export function dotsFit(pointCount: number, plotWidth: number): boolean {
  return pointCount > 0 && plotWidth / pointCount >= 12
}

/** The index of the entry of `xs` nearest `x`; what the crosshair snaps to. */
export function nearestIndex(xs: number[], x: number): number {
  let best = 0
  for (let i = 1; i < xs.length; i++) {
    if (Math.abs(xs[i] - x) < Math.abs(xs[best] - x)) best = i
  }
  return best
}

export function linePath(xs: number[], ys: number[]): string {
  return xs.map((x, i) => `${i === 0 ? 'M' : 'L'}${x.toFixed(1)},${ys[i].toFixed(1)}`).join(' ')
}

export type ChangeRow = {
  date: string
  score: number
  /** The move from the point before, null for the first point. */
  move: number | null
  /** The entries whose first episode aired on this date. */
  markers: ScoreHistoryMarkerDto[]
}

export type ChangeYear = {
  year: number
  rows: ChangeRow[]
}

/**
 * The changes list: every point, newest first, under a heading for each year
 * that holds one. Points arrive oldest first, as the read gives them.
 */
export function changesByYear(points: ScoreHistoryPointDto[], markers: ScoreHistoryMarkerDto[]): ChangeYear[] {
  const years: ChangeYear[] = []
  for (let i = points.length - 1; i >= 0; i--) {
    const point = points[i]
    const year = parseIsoDate(point.date).year
    const row: ChangeRow = {
      date: point.date,
      score: point.score,
      move: i === 0 ? null : Math.round((point.score - points[i - 1].score) * 100) / 100,
      markers: markers.filter((marker) => marker.date === point.date),
    }
    const last = years[years.length - 1]
    if (last?.year === year) last.rows.push(row)
    else years.push({ year, rows: [row] })
  }
  return years
}

/** The one-line summary the graph's `role="img"` carries; the list below is the full alternative. */
export function describeHistory(points: ScoreHistoryPointDto[]): string {
  if (points.length === 0) return 'Score history'
  const first = points[0]
  const last = points[points.length - 1]
  return `Score history, ${points.length} points: ${formatScore(first.score)} on ${formatFullDate(first.date)} to ${formatScore(last.score)} on ${formatFullDate(last.date)}.`
}
