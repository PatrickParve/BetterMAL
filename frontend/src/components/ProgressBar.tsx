import { IncrementButton } from './IncrementButton.tsx'
import './ProgressBar.css'

type ProgressBarProps = {
  watched: number
  total: number | null
  /** When provided, renders a "+" button after the label that bumps episodes watched by one. */
  onIncrement?: () => void
  incrementPending?: boolean
  incrementLabel?: string
}

// Shared "watched/total" episode progress bar — shows `watched/?` when the
// total episode count isn't known yet. Reused across the dashboard, my list,
// and detail pages.
export function ProgressBar({ watched, total, onIncrement, incrementPending, incrementLabel }: ProgressBarProps) {
  const pct = total ? Math.min(100, (watched / total) * 100) : 0
  const atMax = total !== null && watched >= total

  return (
    <div className="progress-bar">
      <div className="progress-bar__track">
        <div className="progress-bar__fill" style={{ width: `${pct}%` }} />
      </div>
      <span className="progress-bar__label">
        {watched}/{total ?? '?'}
      </span>
      {onIncrement && (
        <IncrementButton
          onIncrement={onIncrement}
          disabled={incrementPending || atMax}
          label={incrementLabel ?? 'Increment episodes watched'}
        />
      )}
    </div>
  )
}
