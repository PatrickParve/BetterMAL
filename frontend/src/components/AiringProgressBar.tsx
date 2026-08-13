import './AiringProgressBar.css'

type AiringProgressBarProps = {
  aired: number | null
  watched: number
  total: number | null
  finished: boolean
  /** 'inline' (default) keeps the bar's own `aired/total` label, matching the
   * home page's usage. 'none' suppresses it for a caller that draws its own
   * named readout beside the bar (the series page). */
  labelMode?: 'inline' | 'none'
}

function pct(value: number, total: number): number {
  return Math.min(100, Math.max(0, (value / total) * 100))
}

// When the total episode count is unknown there is no real proportion to
// show, so the blue fill becomes a fixed marker instead: a full track once
// MAL reports the run as finished (it has broadcast everything it's going
// to, even though nobody published how many episodes that took), a fixed
// half track while it's still going and at least an aired estimate exists
// ("progress so far, end unknown"), or empty when nothing is known at all.
function blueFillPct(total: number | null, finished: boolean, aired: number | null): number {
  if (total !== null) return aired !== null ? pct(aired, total) : 0
  if (finished) return 100
  return aired !== null ? 50 : 0
}

// Home-only airing-progress bar: the blue fill is broadcast progress
// (aired/total), with the viewer's watched progress layered on top in the
// site's purple accent colour. Distinct from ProgressBar (watched/total),
// which every other view keeps.
export function AiringProgressBar({ aired, watched, total, finished, labelMode = 'inline' }: AiringProgressBarProps) {
  const airedPct = blueFillPct(total, finished, aired)

  // With a known total, purple is the ordinary watched/total proportion.
  // With an unknown total, purple is scaled *within* the blue extent against
  // however much has aired (falling back to the watched count itself when
  // even that's unknown), so "caught up on everything aired" always covers
  // the whole blue extent and purple can never overshoot it — including in
  // the empty-blue case, where the extent it scales into is itself zero.
  const watchedPct =
    watched <= 0
      ? 0
      : total !== null
        ? pct(watched, total)
        : (watched / Math.max(aired ?? 0, watched)) * airedPct

  const label = `${aired ?? '—'}/${total ?? '?'}`
  const description =
    total !== null
      ? `${aired ?? 'an unknown number of'} of ${total} episodes aired, ${watched} watched`
      : finished
        ? `Finished airing, episode count unpublished (${aired ?? 'an unknown number of'} known aired), ${watched} watched`
        : aired !== null
          ? `${aired} episodes aired so far of an unknown total, ${watched} watched`
          : `Aired episode count unknown, ${watched} watched`

  return (
    <div className="airing-progress-bar">
      <div className="airing-progress-bar__track" title={description} aria-label={description}>
        <div className="airing-progress-bar__aired-fill" style={{ width: `${airedPct}%` }} />
        {watched > 0 && (
          <div className="airing-progress-bar__watched-fill" style={{ width: `${watchedPct}%` }} />
        )}
      </div>
      {labelMode === 'inline' && <span className="airing-progress-bar__label">{label}</span>}
    </div>
  )
}
