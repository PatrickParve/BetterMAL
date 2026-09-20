import type { SeriesStatus } from '../api/types.ts'
import './SeriesStatusPill.css'

const SERIES_STATUS_CLASS: Record<SeriesStatus, string> = {
  Airing: 'airing',
  Ongoing: 'ongoing',
  Finished: 'finished',
}

// The three-colour status pill, shared by the series page's header and the
// Series page's cards (add-series-browser design.md D5/6.2) so the two
// surfaces can never visually disagree on what a status means.
export function SeriesStatusPill({ status }: { status: SeriesStatus }) {
  return <span className={`series-status-pill series-status-pill--${SERIES_STATUS_CLASS[status]}`}>{status}</span>
}
