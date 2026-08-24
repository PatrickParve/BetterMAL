import type { SeriesProgressBadge } from '../api/types.ts'
import './SeriesCompletionBadge.css'

const BADGE_CLASS: Record<Exclude<SeriesProgressBadge, 'None'>, string> = {
  Completed: 'completed',
  CaughtUp: 'caught-up',
  Behind: 'behind',
  Dropped: 'dropped',
  Unwatched: 'unwatched',
}

function badgeLabel(badge: Exclude<SeriesProgressBadge, 'None'>, behindEpisodes: number | null): string {
  if (badge === 'Completed') return 'Completed'
  if (badge === 'CaughtUp') return 'Caught up'
  if (badge === 'Dropped') return 'Dropped'
  if (badge === 'Unwatched') return 'Unwatched'
  return `${behindEpisodes ?? 0} behind`
}

// The five-colour personal-progress badge, shared by the series page's
// header and the Series page's cards (add-series-browser design.md D5/6.2;
// Dropped/Unwatched added by polish-series-badges-and-filters design.md D1)
// so a card and the page it links to can never visually disagree.
// behindEpisodes is only read when badge is 'Behind'; renders nothing for
// 'None'.
export function SeriesCompletionBadge({
  badge,
  behindEpisodes,
}: {
  badge: SeriesProgressBadge
  behindEpisodes: number | null
}) {
  if (badge === 'None') return null
  return (
    <span className={`series-completion-badge series-completion-badge--${BADGE_CLASS[badge]}`}>
      {badgeLabel(badge, behindEpisodes)}
    </span>
  )
}
