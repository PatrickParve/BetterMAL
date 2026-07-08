import './ProgressBar.css'

type ProgressBarProps = {
  watched: number
  total: number | null
}

// Shared "watched/total" episode progress bar — shows `watched/?` when the
// total episode count isn't known yet. Reused across the dashboard, my list,
// and detail pages.
export function ProgressBar({ watched, total }: ProgressBarProps) {
  const pct = total ? Math.min(100, (watched / total) * 100) : 0

  return (
    <div className="progress-bar">
      <div className="progress-bar__track">
        <div className="progress-bar__fill" style={{ width: `${pct}%` }} />
      </div>
      <span className="progress-bar__label">
        {watched}/{total ?? '?'}
      </span>
    </div>
  )
}
