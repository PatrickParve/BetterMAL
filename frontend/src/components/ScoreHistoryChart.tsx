import { useLayoutEffect, useMemo, useRef, useState, type PointerEvent } from 'react'
import { Link } from 'react-router-dom'
import type { ScoreHistoryMarkerDto, ScoreHistoryPointDto } from '../api/types.ts'
import { pickDisplayTitle } from '../utils/anime.ts'
import {
  chooseTicks,
  dayNumber,
  describeHistory,
  dotsFit,
  formatDayMonth,
  formatFullDate,
  formatScore,
  linePath,
  nearestIndex,
  placeRail,
  placeYearLabels,
  scoreScale,
  thinLabels,
  timeScale,
  yearMarks,
} from '../utils/scoreHistoryChart.ts'
import { PosterPicture } from './PosterPicture.tsx'
import './ScoreHistoryChart.css'

type ScoreHistoryChartProps = {
  points: ScoreHistoryPointDto[]
  markers: ScoreHistoryMarkerDto[]
}

// The plot's height is fixed, so a taller screen changes nothing and the box
// never moves while its width is measured (add-score-history-graph design.md
// D10). Margins hold the axis labels: ticks on the left, the last score on the
// right, and below the plot a row of dates and a row of years.
const PLOT_HEIGHT = 180
const MARGIN = { top: 10, right: 46, bottom: 42, left: 40 }
const MIN_PLOT_WIDTH = 60
const DATE_ROW_Y = 17
const YEAR_ROW_Y = 35

// A date or a year at 11px, and the room kept between two of them.
const DATE_LABEL_WIDTH = 30
const YEAR_LABEL_WIDTH = 28
const LABEL_GAP = 8

const DOT_RADIUS = 4
const LAST_LABEL_OFFSET = 9

// A marker's picture is a fixed 52×72 poster box (design.md D11), so a
// picture arriving cannot move anything. Rows are one box high and a gap, and
// the gap under row 0 is where its leaders are seen.
const MARKER_W = 52
const MARKER_H = 72
const MARKER_GAP = 6
const ROW_PITCH = MARKER_H + MARKER_GAP

const TOOLTIP_HALF_WIDTH = 48
const TOOLTIP_HEIGHT = 46

// The score history graph (design.md D10, D11): a line through the points,
// drawn by hand in SVG. The geometry comes from utils/scoreHistoryChart.ts; this
// only measures its width, draws what that returns, and tracks the pointer.
// Colours come from --mal, --border, --text, --text-h and --bg alone, so both
// themes work with no rules of their own.
export function ScoreHistoryChart({ points, markers }: ScoreHistoryChartProps) {
  const containerRef = useRef<HTMLDivElement>(null)
  const [width, setWidth] = useState(0)
  const [hovered, setHovered] = useState<number | null>(null)

  // Measured before the first paint, so the graph is never drawn at a
  // placeholder width and then corrected.
  useLayoutEffect(() => {
    const node = containerRef.current
    if (!node) return
    setWidth(node.clientWidth)
    const observer = new ResizeObserver(() => setWidth(node.clientWidth))
    observer.observe(node)
    return () => observer.disconnect()
  }, [])

  const plotWidth = Math.max(width - MARGIN.left - MARGIN.right, MIN_PLOT_WIDTH)

  const layout = useMemo(() => {
    if (points.length === 0) return null

    const days = points.map((point) => dayNumber(point.date))
    const x = timeScale(days[0], days[days.length - 1], plotWidth)
    const y = scoreScale(
      points.map((point) => point.score),
      PLOT_HEIGHT,
    )
    const xs = days.map(x)
    const ys = points.map((point) => y.y(point.score))

    const labelIndices = thinLabels(xs, DATE_LABEL_WIDTH, LABEL_GAP)
    const years = placeYearLabels(yearMarks(points.map((point) => point.date)), x, YEAR_LABEL_WIDTH, LABEL_GAP)

    const markerXs = markers.map((marker) => x(dayNumber(marker.date)))
    const placements = placeRail(markerXs, plotWidth, MARKER_W, MARKER_GAP)
    const rows = placements.reduce((count, placement) => Math.max(count, placement.row + 1), 0)

    return { xs, ys, ticks: chooseTicks(y.min, y.max), y, labelIndices, years, markerXs, placements, railHeight: rows * ROW_PITCH }
  }, [points, markers, plotWidth])

  // Nothing is drawn before the width is known; the box keeps its height so
  // the overlay doesn't shift when it is.
  const railHeight = layout?.railHeight ?? 0
  const plotTop = railHeight + MARGIN.top
  const height = plotTop + PLOT_HEIGHT + MARGIN.bottom

  function handlePointer(event: PointerEvent<SVGRectElement>) {
    if (!layout) return
    const box = event.currentTarget.getBoundingClientRect()
    setHovered(nearestIndex(layout.xs, event.clientX - box.left))
  }

  const markerDates = new Set(markers.map((marker) => marker.date))

  return (
    <div ref={containerRef} className="score-history-chart" style={{ height }}>
      {width > 0 && layout && (
        <>
          <svg className="score-history-chart__svg" width={width} height={height} role="img" aria-label={describeHistory(points)}>
            <g transform={`translate(${MARGIN.left},${plotTop})`}>
              {layout.ticks.map((tick) => {
                const tickY = layout.y.y(tick)
                return (
                  <g key={tick}>
                    <line className="score-history-chart__grid" x1={0} x2={plotWidth} y1={tickY} y2={tickY} />
                    <text className="score-history-chart__tick" x={-8} y={tickY} dy="0.32em" textAnchor="end">
                      {formatScore(tick)}
                    </text>
                  </g>
                )
              })}

              {layout.years.slice(1).map((year) => (
                <line
                  key={year.year}
                  className="score-history-chart__year-line"
                  x1={year.x}
                  x2={year.x}
                  y1={0}
                  y2={PLOT_HEIGHT + YEAR_ROW_Y + 4}
                />
              ))}

              {markers.map((marker, index) => {
                const placement = layout.placements[index]
                const boxBottom = railHeight - (placement.row + 1) * ROW_PITCH + MARKER_H - plotTop
                const pointIndex = points.findIndex((point) => point.date === marker.date)
                return (
                  <line
                    key={marker.animeId}
                    className="score-history-chart__leader"
                    x1={layout.markerXs[index]}
                    x2={layout.markerXs[index]}
                    y1={boxBottom}
                    y2={pointIndex === -1 ? 0 : layout.ys[pointIndex]}
                  />
                )
              })}

              <path className="score-history-chart__line" d={linePath(layout.xs, layout.ys)} />

              {points.map((point, index) => {
                const isLast = index === points.length - 1
                if (!isLast && !markerDates.has(point.date) && !dotsFit(points.length, plotWidth)) return null
                return <circle key={point.date} className="score-history-chart__dot" cx={layout.xs[index]} cy={layout.ys[index]} r={DOT_RADIUS} />
              })}

              <text
                className="score-history-chart__last"
                x={layout.xs[points.length - 1] + LAST_LABEL_OFFSET}
                y={layout.ys[points.length - 1]}
                dy="0.32em"
              >
                {formatScore(points[points.length - 1].score)}
              </text>

              {layout.labelIndices.map((index) => (
                <text
                  key={points[index].date}
                  className="score-history-chart__date"
                  x={layout.xs[index]}
                  y={PLOT_HEIGHT + DATE_ROW_Y}
                  textAnchor="middle"
                >
                  {formatDayMonth(points[index].date)}
                </text>
              ))}

              {layout.years.map((year) => (
                <text
                  key={year.year}
                  className="score-history-chart__year"
                  x={year.x + (year.anchor === 'start' ? 4 : -4)}
                  y={PLOT_HEIGHT + YEAR_ROW_Y}
                  textAnchor={year.anchor}
                >
                  {year.year}
                </text>
              ))}

              {hovered !== null && (
                <g className="score-history-chart__crosshair">
                  <line x1={layout.xs[hovered]} x2={layout.xs[hovered]} y1={0} y2={PLOT_HEIGHT} />
                  <circle cx={layout.xs[hovered]} cy={layout.ys[hovered]} r={DOT_RADIUS + 2} />
                </g>
              )}

              <rect
                className="score-history-chart__hit"
                x={0}
                y={0}
                width={plotWidth}
                height={PLOT_HEIGHT}
                onPointerMove={handlePointer}
                onPointerDown={handlePointer}
                onPointerLeave={() => setHovered(null)}
              />
            </g>
          </svg>

          {markers.map((marker, index) => {
            const placement = layout.placements[index]
            const title = pickDisplayTitle(marker.title, marker.englishTitle)
            return (
              <Link
                key={marker.animeId}
                to={`/anime/${marker.animeId}`}
                className="score-history-chart__marker"
                style={{
                  left: MARGIN.left + placement.left,
                  top: railHeight - (placement.row + 1) * ROW_PITCH,
                }}
                title={title}
                aria-label={title}
              >
                <PosterPicture src={marker.pictureUrl} loading="lazy" className="score-history-chart__poster" />
              </Link>
            )
          })}

          {hovered !== null && (
            <ScoreHistoryTooltip
              point={points[hovered]}
              x={MARGIN.left + layout.xs[hovered]}
              y={plotTop + layout.ys[hovered]}
              width={width}
              minTop={railHeight}
            />
          )}
        </>
      )}
    </div>
  )
}

function ScoreHistoryTooltip({
  point,
  x,
  y,
  width,
  minTop,
}: {
  point: ScoreHistoryPointDto
  x: number
  y: number
  width: number
  minTop: number
}) {
  // Above the point, unless that would reach into the picture rail, then below.
  const below = y - TOOLTIP_HEIGHT - 12 < minTop
  const left = Math.min(Math.max(x, TOOLTIP_HALF_WIDTH), width - TOOLTIP_HALF_WIDTH)
  return (
    <div
      className="score-history-chart__tooltip"
      style={{ left, top: below ? y + 12 : y - 12, transform: `translate(-50%, ${below ? '0' : '-100%'})` }}
    >
      <span className="score-history-chart__tooltip-score">{formatScore(point.score)}</span>
      <span className="score-history-chart__tooltip-date">{formatFullDate(point.date)}</span>
    </div>
  )
}
