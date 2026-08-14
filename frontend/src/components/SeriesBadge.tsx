import './SeriesBadge.css'

// The "SERIES · N entries" line shown beneath a series result's title, in
// both the type-ahead dropdown row and the results-page card's meta slot
// (design.md decision 7) — entryCount covers every member, extras included
// (decision 8).
export function SeriesBadge({ entryCount }: { entryCount: number }) {
  return (
    <span className="series-badge">
      <span className="series-badge__pill">Series</span>
      {entryCount} {entryCount === 1 ? 'entry' : 'entries'}
    </span>
  )
}
