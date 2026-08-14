import type { ReactNode } from 'react'
import './ScoreChip.css'

type ScoreChipProps = {
  role: 'mal' | 'mine'
  label?: string
  size?: 'default' | 'compact'
  children: ReactNode
}

// Shared chip form for a standalone score figure (design.md decision 8): a
// tinted, coloured block carrying a role's value and, at the default size, an
// optional label above it. The compact size drops the border and label for
// the tile-card density (SeriesTimeline/SeriesExtraTile), filling its share
// of a chip row instead. Replaces the three near-identical chip
// implementations that grew independently on the series page
// (series-page__score-chip*, series-timeline__card-chip*,
// series-extra-tile__chip*).
//
// A `role="mal"` chip's children should always be a <ScoreValue> element
// (design.md decision 10) — ScoreChip only supplies the colour and form, so
// hide/reveal/completed behaviour keeps working unmodified; the blurred
// placeholder inherits the chip's MAL colour via CSS rather than this
// component's own logic.
export function ScoreChip({ role, label, size = 'default', children }: ScoreChipProps) {
  return (
    <div className={`score-chip score-chip--${role} score-chip--${size}`}>
      {label && <span className="score-chip__label">{label}</span>}
      <span className="score-chip__value">{children}</span>
    </div>
  )
}
