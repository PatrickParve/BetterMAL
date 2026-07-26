import './AiringProgressBar.css'

type AiringProgressBarProps = {
  aired: number | null
  watched: number
  total: number | null
}

function pct(value: number, total: number | null): number {
  if (!total) return 0
  return Math.min(100, Math.max(0, (value / total) * 100))
}

// Home-only airing-progress bar: the accent fill is broadcast progress
// (aired/total), with the viewer's watched progress layered on top in green.
// Distinct from ProgressBar (watched/total), which every other view keeps.
export function AiringProgressBar({ aired, watched, total }: AiringProgressBarProps) {
  const airedPct = aired !== null ? pct(aired, total) : 0
  const watchedPct = pct(watched, total)
  const label = `${aired ?? '?'}/${total ?? '?'}`
  const description = `${aired ?? '?'} of ${total ?? '?'} episodes aired, ${watched} watched`

  return (
    <div className="airing-progress-bar">
      <div className="airing-progress-bar__track" title={description} aria-label={description}>
        <div className="airing-progress-bar__aired-fill" style={{ width: `${airedPct}%` }} />
        {watched > 0 && (
          <div className="airing-progress-bar__watched-fill" style={{ width: `${watchedPct}%` }} />
        )}
      </div>
      <span className="airing-progress-bar__label">{label}</span>
    </div>
  )
}
