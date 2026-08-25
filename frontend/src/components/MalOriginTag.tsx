import type { ActivityChangeSource } from '../api/types.ts'
import './MalOriginTag.css'

// Shared marker for an activity row applied by a sync path, rather than by me
// in the app (record-mal-origin-activity design D7). The three sync origins
// share this one tag — which sync path applied a change is a debugging
// question, answerable from the log, not worth distinguishing here. A row
// with no `source` (an older payload) or `BetterMal` (my own editing) renders
// nothing, so the unmarked row stays the ordinary one.
export function MalOriginTag({ source }: { source?: ActivityChangeSource }) {
  if (!source || source === 'BetterMal') return null

  return (
    <span className="mal-origin-tag" title="Applied by a MyAnimeList sync">
      via MAL
    </span>
  )
}
