import './JobProgressTrack.css'

// The shared background-job progress bar (navbar-settings-status-indicator
// design.md D10) — the track and fill SettingsPage's JobProgress used to own
// outright, now used identically by the Settings page and the navbar's
// Settings gear so the two bars can never drift apart. `total === null`
// draws a continuously moving fill rather than an empty or zero-filled one,
// for a job that doesn't yet know how much work there is — callers must only
// pass `total: null` while that's actually true, not for a job that ended
// without ever learning one. `valueText` present renders a real
// `role="progressbar"` with the same ARIA `JobProgress` has always carried
// (`aria-valuemin`/`now`/`max` omitted while indeterminate, as the spec for
// an indeterminate progressbar asks, with the wording in `aria-valuetext`
// instead); absent, the track is `aria-hidden` — the navbar's decorative
// sliver, whose facts ride on the link's own accessible name instead
// (design D11). `className` is how a caller turns this into that sliver.
export function JobProgressTrack({
  done,
  total,
  valueText,
  className,
}: {
  done: number
  total: number | null
  valueText?: string
  className?: string
}) {
  const indeterminate = total === null
  const pct = total && total > 0 ? Math.min(100, Math.round((done / total) * 100)) : 0
  const decorative = valueText === undefined

  return (
    <div
      className={className ? `job-progress-track ${className}` : 'job-progress-track'}
      role={decorative ? undefined : 'progressbar'}
      aria-hidden={decorative ? 'true' : undefined}
      aria-valuemin={!decorative && !indeterminate ? 0 : undefined}
      aria-valuenow={!decorative && !indeterminate ? done : undefined}
      aria-valuemax={!decorative && !indeterminate ? (total ?? done) : undefined}
      aria-valuetext={decorative ? undefined : valueText}
    >
      <div
        className={
          indeterminate ? 'job-progress-track__fill job-progress-track__fill--indeterminate' : 'job-progress-track__fill'
        }
        style={indeterminate ? undefined : { width: `${pct}%` }}
      />
    </div>
  )
}
