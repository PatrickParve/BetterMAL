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
// count. The box leads nowhere, its title is plain, and a split with nothing to
// show is not drawn.
export function TasteSplit({
  title,
  categories,
  labelOf = (label) => label,
}: {
  title: string
  categories: TasteSplitDto[]
  labelOf?: (label: string) => string
}) {
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
    }
  })
  const summary = entries.map((entry) => `${entry.label} ${entry.percent}`).join(', ')

  return (
    <section className="profile-box taste-split">
      <h2>{title}</h2>
      <div className="taste-split__bar" role="img" aria-label={`${title}: ${summary}`}>
        {entries.map((entry) => (
          <span
            key={entry.key}
            className={`taste-split__segment taste-split__segment--${entry.tone}`}
            style={{ width: `${(entry.count / total) * 100}%` }}
            title={countTitle(entry.count)}
          />
        ))}
      </div>
      <ul className="taste-split__legend">
        {entries.map((entry) => (
          <li key={entry.key} className="taste-split__legend-entry" title={countTitle(entry.count)}>
            <span className={`taste-split__swatch taste-split__segment--${entry.tone}`} aria-hidden="true" />
            <span className="taste-split__legend-label">{entry.label}</span>
            <span className="taste-split__legend-percent">{entry.percent}</span>
          </li>
        ))}
      </ul>
    </section>
  )
}
