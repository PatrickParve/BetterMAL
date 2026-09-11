import { Link } from 'react-router-dom'
import './RecapScopeChip.css'

type RecapScopeChipProps = {
  label: string
  href: string
  onDismiss: () => void
}

// The compact scope indicator (design D8 of
// redo-my-list-filtering-and-sorting): one line tall, a chip rather than a
// tab, at the start of the results line. MyListPage keeps recapScopeLabel()
// / recapSearchFromScope() / dismissRecapScope() and just feeds them in —
// this component owns only how the chip is drawn.
export function RecapScopeChip({ label, href, onDismiss }: RecapScopeChipProps) {
  return (
    <div className="recap-scope-chip">
      <span className="recap-scope-chip__text">Recap · {label}</span>
      <Link to={href} className="recap-scope-chip__link">
        View recap
      </Link>
      <button type="button" className="recap-scope-chip__dismiss" aria-label="Dismiss recap scope" onClick={onDismiss}>
        &times;
      </button>
    </div>
  )
}
