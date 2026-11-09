import { useState } from 'react'
import { Link } from 'react-router-dom'
import type { TasteSplitDto } from '../../api/types.ts'
import './TasteSplit.css'

// A category's share of its split, as a whole percentage; anything under one
// percent reads "<1%" so a small category is still named (profile-stats
// "Demographic, source and era").
function percentLabel(count: number, total: number): string {
  const share = (count / total) * 100
  return share < 1 ? '<1%' : `${Math.round(share)}%`
}

function countTitle(count: number): string {
  return `${count} anime`
}

// One of the profile's three small split boxes (Demographic, Source, Era): a
// single bar divided into the split's categories, largest first from the left,
// each segment as wide as its share, with a legend beneath naming each category
// with its whole-percentage share. The segments are steps of the accent, strongest
// for the largest, and Other is the neutral border colour; each legend entry
// carries its segment's colour. Hovering a segment or its legend entry shows its
// count. Every category leads to my list (profile-stats "Demographic, source and
// era"): each legend entry is a link and a keyboard stop, and each segment is a link
// that follows by pointer only, since its legend entry is the stop. Hovering or
// focusing either highlights both, with no change of size. The box's title is plain
// and a split with nothing to show is not drawn.
export function TasteSplit({
  title,
  categories,
  linkOf,
  labelOf = (label) => label,
}: {
  title: string
  categories: TasteSplitDto[]
  // Where a category leads: my list with the values it covers ticked.
  linkOf: (category: TasteSplitDto) => string
  labelOf?: (label: string) => string
}) {
  const [hoveredKey, setHoveredKey] = useState<string | null>(null)
  const total = categories.reduce((sum, category) => sum + category.count, 0)
  if (total === 0) return null

  // "other" is the merged category, always last; the named ones take the accent
  // steps in the order they come, which is largest first.
  let step = 0
  const entries = categories.map((category) => {
    const isOther = category.label === 'other'
    return {
      key: category.label,
      label: isOther ? 'Other' : labelOf(category.label),
      count: category.count,
      percent: percentLabel(category.count, total),
      tone: isOther ? 'other' : String(step++),
      to: linkOf(category),
    }
  })

  // The pair of a segment and its legend entry share these, so either one lights
  // both.
  function hoverProps(key: string) {
    return {
      onPointerEnter: () => setHoveredKey(key),
      onPointerLeave: () => setHoveredKey(null),
      onFocus: () => setHoveredKey(key),
      onBlur: () => setHoveredKey(null),
    }
  }

  return (
    <section className="profile-box taste-split">
      <h2>{title}</h2>
      <div className="taste-split__bar">
        {entries.map((entry) => (
          <Link
            key={entry.key}
            to={entry.to}
            tabIndex={-1}
            aria-hidden="true"
            className={`taste-split__segment taste-split__segment--${entry.tone}${
              hoveredKey === entry.key ? ' taste-split__segment--hovered' : ''
            }`}
            style={{ width: `${(entry.count / total) * 100}%` }}
            title={countTitle(entry.count)}
            {...hoverProps(entry.key)}
          />
        ))}
      </div>
      <ul className="taste-split__legend">
        {entries.map((entry) => (
          <li key={entry.key}>
            <Link
              to={entry.to}
              className={`taste-split__legend-entry${hoveredKey === entry.key ? ' taste-split__legend-entry--hovered' : ''}`}
              title={countTitle(entry.count)}
              {...hoverProps(entry.key)}
            >
              <span className={`taste-split__swatch taste-split__segment--${entry.tone}`} aria-hidden="true" />
              <span className="taste-split__legend-label">{entry.label}</span>
              <span className="taste-split__legend-percent">{entry.percent}</span>
            </Link>
          </li>
        ))}
      </ul>
    </section>
  )
}
